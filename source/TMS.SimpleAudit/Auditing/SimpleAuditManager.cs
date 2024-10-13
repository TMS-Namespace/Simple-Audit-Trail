using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.Help;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Auditing;

public sealed class SimpleAuditManager : IDisposable
{
    #region Private Variables

    private bool _auditingIsEnabled;

    // We will keep a list of generated audit trails to clean DBContext in case of failure.
    private readonly HashSet<object> _auditRecordsCache = [];


    private List<RowAuditInfo> _rowsAuditInfos = [];

    private bool _isAuditRecordsGenerated;

    #endregion

    #region Public Properties

    /// <summary>
    /// Determines if auditing of changes will be performed.
    /// </summary>
    public bool AuditingIsEnabled
    {
        get => _auditingIsEnabled;
        set
        {
            if (value != _auditingIsEnabled)
            {
                if (value) // Check only if we are enabling auditing
                {
                    AuditSettings.Validate();
                }

                if (!value && IsAuditingSessionStarted)
                {
                    throw new AuditingException("Auditing can't be stopped during an ongoing auditing session.");
                }

                _auditingIsEnabled = value;
            }
        }
    }

    /// <summary>
    /// New auditing session can't be started, before the previous one is finished.
    /// </summary>
    public bool IsAuditingSessionStarted { get; private set; }

    public AuditSettings AuditSettings { get; private init; }

    /// <summary>
    /// Determines if the context should capture the context of the current change, usually should be True when used in UI contexts
    /// </summary>
    public bool CaptureThreadContext { get; set; }

    public DbContext DataBaseContext { get; private set; }

    public bool HasAuditableChanges => _rowsAuditInfos.Any(rai => rai.ColumnsChanges.Count > 0);

    #endregion

    #region Public Constructors

    public SimpleAuditManager(DbContext dbContext)
    {
        DataBaseContext = dbContext;
        AuditSettings = new(this);
    }

    #endregion

    #region Internal Functions

    internal void ValidateIfCanReconfigure()
    {
        if (IsAuditingSessionStarted)
        {
            throw new AuditingException("Audit trail configuration is not allowed during an ongoing auditing session.");
        }
    }

    #endregion

    #region Private Functions

    private List<RowAuditInfo> AuditableChanges
        => _rowsAuditInfos
            .Where(rai => rai.ColumnsAuditInfos.Count > 0)
            .ToList();

    private Dictionary<EntityEntry, EntityState> _nonAuditChangesStatesBackup = [];

    private List<RowAuditInfo> PrepareRowAuditInfo()
    {
        var rowsAuditInfos = new List<RowAuditInfo>();

        if (!AuditingIsEnabled)
        {
            return rowsAuditInfos;
        }

        DataBaseContext.ChangeTracker.DetectChanges();

        // backup current changes state
        var changedEntities = DataBaseContext.ChangeTracker
            .Entries();

        _nonAuditChangesStatesBackup = changedEntities
            .Select(ee => new KeyValuePair<EntityEntry, EntityState>(ee, ee.State))
            .ToDictionary();

        // Check if there is anything that is worth auditing
        foreach (var entityEntry in changedEntities.Where(IsEntityHasAuditableChanges))
        {
            // Check if we have any settings for this entry
            var entitySettings = AuditSettings.GetEntityAuditSettings(entityEntry.Entity.GetType());

            if (entitySettings is null) // No settings for this entity are set
            {
                continue;
            }

            // check if we have a soft deletion
            var action = Mapper.ToModel(entityEntry.State);

            if (action == AuditAction.Modified &&
                entitySettings.IsSoftDeleted(entityEntry.Entity))
            {
                action = AuditAction.Deleted;
            }

            // Create row audit info
            var rowAuditInfo = Mapper.ToModel(entityEntry, entitySettings, action);

            var columnsAuditInfo = entityEntry
                .Properties
                .Select(pe => GetColumnAuditInfos(entitySettings, entityEntry, pe, rowAuditInfo))
                .Where(cai => cai != null) // some columns may have no audit info
                .ToList();

            // If no columns are worth tracking, then skip this entity
            if (columnsAuditInfo.Count > 0)
            {
                rowAuditInfo.ColumnsAuditInfos.AddRange(columnsAuditInfo!);
                rowsAuditInfos.Add(rowAuditInfo);
            }
        }

        return rowsAuditInfos;
    }

    private ColumnAuditInfo? GetColumnAuditInfos(
        IEntityAuditSettingsInternal entitySettings,
        EntityEntry entityEntry,
        PropertyEntry propertyEntry,
        RowAuditInfo rowAuditInfo)
    {
        var propertySettings = entitySettings.GetPropertySettings(propertyEntry.Metadata.Name);

        // Check if the property has settings, and its value actually is changed
        if (propertySettings is not null && IsPropertyValueChanged(entityEntry, propertyEntry))
        {
            // Create column audit info
            var columnChange = Mapper.ToModel(propertyEntry, propertySettings, rowAuditInfo);

            // EF assigns to Original value same value as new value on insertion
            columnChange.OldValue = entityEntry.State == EntityState.Added ? null : propertyEntry.OriginalValue;
            columnChange.NewValue = entityEntry.State == EntityState.Deleted ? null : propertyEntry.CurrentValue;

            return columnChange;
        }

        return null;
    }

    private static bool IsEntityHasAuditableChanges(EntityEntry entityEntry)
        =>
            entityEntry.Entity != null
            && entityEntry.State != EntityState.Unchanged
            && entityEntry.State != EntityState.Detached;

    private static bool IsPropertyValueChanged(EntityEntry entityEntry, PropertyEntry propertyEntry)
    {
        // If we are inserting, EF considers all columns as modified
        if (entityEntry.State == EntityState.Added)
        {
            return propertyEntry.CurrentValue is not null;
        }

        // If we are deleting, we consider all columns as modified, to keep a record of deleted information
        if (entityEntry.State == EntityState.Deleted)
        {
            return true;
        }

        return propertyEntry.IsModified;
    }

    private async Task UpdateRowAuditInfoAsync(RowAuditInfo rowAuditInfo, CancellationToken cancellationToken)
    {
        var columnsToSkip = new HashSet<ColumnAuditInfo>();

        foreach (var columnAuditInfo in rowAuditInfo.ColumnsAuditInfos)
        {
            columnAuditInfo.NewValue = rowAuditInfo.Action == AuditAction.Deleted ? null : columnAuditInfo.TrackingPropertyEntry.CurrentValue;

            columnAuditInfo.OldValue = await columnAuditInfo
                .PropertyAuditSettings
                .MapValueAsync(DataBaseContext, rowAuditInfo.RowModel, columnAuditInfo.OldValue, cancellationToken)
                .ConfigureAwait(CaptureThreadContext);

            columnAuditInfo.NewValue = await columnAuditInfo
                .PropertyAuditSettings
                .MapValueAsync(DataBaseContext, rowAuditInfo.RowModel, columnAuditInfo.NewValue, cancellationToken)
                .ConfigureAwait(CaptureThreadContext);

            // If the value did not change after mapping, we exclude the column from auditing, however, if we deleting, we keep all tracked columns
            if (rowAuditInfo.Action != AuditAction.Deleted && Equals(columnAuditInfo.NewValue, columnAuditInfo.OldValue))
            {
                columnsToSkip.Add(columnAuditInfo);
            }
        }

        rowAuditInfo.ColumnsAuditInfos.RemoveAll(columnsToSkip.Contains);

        // Setup the reference key
        if (rowAuditInfo.ColumnsAuditInfos.Count > 0)
        {
            rowAuditInfo.ReferenceKeyValue = rowAuditInfo.EntityAuditSettings.GetReferenceKeyValue(rowAuditInfo);
        }
    }

    private void ResetNonAuditChangesStates()
        => _nonAuditChangesStatesBackup
            .ToList()
            .ForEach(k => k.Key.State = k.Value);

    #endregion

    #region Public Functions

    /// <summary>
    /// Configures the auditing table, audit records generator, and specifies which tables/columns should be audited.
    /// </summary>
    /// <typeparam name="TAuditTrailTableModel">The model type of the table to audit.</typeparam>
    /// <param name="auditRecordGeneratorAsync">
    /// A callback that takes <see cref="RowAuditInfo"/> and custom audit information, as well as a <see cref="CancellationToken"/>,
    /// and returns a task that creates and returns the audit record of type <typeparamref name="TAuditTrailTableModel"/>.
    /// </param>
    /// <remarks>
    /// If the <paramref name="auditRecordGeneratorAsync"/> returns null, auditing of the current change will be skipped.
    /// </remarks>
    /// <returns>An <see cref="GroupAuditConfiguration{TAuditTrailTableModel}"/> instance.</returns>
    public GroupAuditConfiguration<TAuditTrailTableModel> ConfigureAuditTrailGroup<TAuditTrailTableModel>(
        Func<RowAuditInfo, object?, CancellationToken, Task<TAuditTrailTableModel?>> auditRecordGeneratorAsync)
        where TAuditTrailTableModel : class
    {
        ValidateIfCanReconfigure();

        return new(this, auditRecordGeneratorAsync);
    }

    /// <summary>
    /// Configures the auditing table, audit records generator callback, and specifies which tables/columns should be audited.
    /// </summary>
    /// <typeparam name="TAuditTrailTableModel">The model type of the table to audit.</typeparam>
    /// <param name="auditRecordsGeneratorAsync">
    /// A callback that takes <see cref="RowAuditInfo"/> and custom audit information, as well as a <see cref="CancellationToken"/>,
    /// and returns a task that creates and returns a list of audit records of type <typeparamref name="TAuditTrailTableModel"/>.
    /// </param>
    /// <remarks>
    /// If the <paramref name="auditRecordsGeneratorAsync"/> returns an empty list, auditing of the current change will be skipped.
    /// </remarks>
    /// <returns>An <see cref="GroupAuditConfiguration{TAuditTrailTableModel}"/> instance.</returns>
    public GroupAuditConfiguration<TAuditTrailTableModel> ConfigureAuditTrailGroup<TAuditTrailTableModel>(
        Func<RowAuditInfo, object?, CancellationToken, Task<List<TAuditTrailTableModel>>> auditRecordsGeneratorAsync)
        where TAuditTrailTableModel : class
    {
        ValidateIfCanReconfigure();

        return new(this, auditRecordsGeneratorAsync);
    }

    public void OnRetryFail()
    {
        // at this point, entities state can be set to "Unchanged" by EF
        ResetNonAuditChangesStates();

        // remove all generated audit trail records, so they will not got saved in
        // the next retry start, when main data SaveChangesAsync will be called.
        _auditRecordsCache
            .ToList()
            .ForEach(ar => DataBaseContext.Remove(ar));
    }

    public void OnBeforeSaving()
    {
        if (IsAuditingSessionStarted)
        {
            throw new AuditingException("Can't start new audit session, since the previous session is unfinilized.");
        }

        IsAuditingSessionStarted = true;

        _rowsAuditInfos = PrepareRowAuditInfo();
    }

    public async Task OnAfterSaveAsync(
        Func<bool, CancellationToken, Task<int>> NoAuditSaveChangesAsync,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        if (!IsAuditingSessionStarted)
        {
            throw new AuditingException($"{nameof(OnAfterSaveAsync)} can't be called before {nameof(OnBeforeSaving)}.");
        }

        if (!HasAuditableChanges)
        {
            return;
        }

        DataBaseContext.ChangeTracker.AcceptAllChanges();

        // Update all values in audit info objects
        foreach (var rowAuditInfo in _rowsAuditInfos)
        {
            await UpdateRowAuditInfoAsync(rowAuditInfo, cancellationToken);
        }

        // on retry, we should have the records already generated, but probably
        // removed from DBContext because of failure, so we re-add them
        if (_isAuditRecordsGenerated)
        {
            foreach (var auditRecord in _auditRecordsCache)
            {
                await DataBaseContext
                    .AddAsync(auditRecord, cancellationToken);
            }
        }
        else
        {
            // after update we may have some audit info absolute
            foreach (var rowAuditInfo in AuditableChanges)
            {
                var auditRecords = await rowAuditInfo
                    .EntityAuditSettings
                    .GroupAuditSettings
                    .GenerateAuditRecordsAsync(rowAuditInfo, customAuditInfo, cancellationToken)
                    .ConfigureAwait(CaptureThreadContext);

                foreach (var auditRecord in auditRecords)
                {
                    await DataBaseContext
                        .AddAsync(auditRecord, cancellationToken);

                    _auditRecordsCache.Add(auditRecord);
                }
            }
        }

        if (_auditRecordsCache.Count > 0)
        {
            try
            {
                await NoAuditSaveChangesAsync(false, cancellationToken)
                    .ConfigureAwait(CaptureThreadContext);
            }
            catch (Exception ex)
            {
                throw new AuditingException(ex);
            }
        }

        _isAuditRecordsGenerated = true;
    }

    public void OnFinalize(bool withError)
    {
        if (!IsAuditingSessionStarted)
        {
            throw new AuditingException($"{nameof(OnFinalize)} can't be called before {nameof(OnBeforeSaving)}.");
        }

        if (withError)
        {
            if (_isAuditRecordsGenerated)
            {
                // Clean up DB Context from the generated trails
                DataBaseContext.RemoveRange(_auditRecordsCache);
                // Accept changes to remove the audit trails from the tracker
                DataBaseContext.ChangeTracker.AcceptAllChanges();
            }

            // revert non audit data changes to the original state
            ResetNonAuditChangesStates();
        }
        else
        {
            DataBaseContext.ChangeTracker.AcceptAllChanges();
        }

        _auditRecordsCache.Clear();
        _rowsAuditInfos.Clear();
        _nonAuditChangesStatesBackup.Clear();

        IsAuditingSessionStarted = false;
        _isAuditRecordsGenerated = false;
    }

    public async Task<bool?> CheckAuditSuccessAsync(CancellationToken cancellationToken)
    {
        var rowAuditInfo = AuditableChanges.LastOrDefault();

        if (rowAuditInfo == null)
        {
            return null;
        }

        var groupSettings = AuditSettings
            .GetGroupSettingsByTableModelType(rowAuditInfo.EntityAuditSettings.TableModelType)!;

        return await groupSettings
            .AuditRecordExistenceCheckAsync(DataBaseContext, rowAuditInfo, cancellationToken)
            .ConfigureAwait(CaptureThreadContext);
    }

    #endregion

    #region Disposing

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                DataBaseContext.Dispose();
            }

            _disposed = true;
        }
    }

    ~SimpleAuditManager() => Dispose(false);

    #endregion

}
