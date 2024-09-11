using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Configurators;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit;

public abstract class SimpleAuditContext : DbContext
{
    #region Fields

    private bool _auditingIsEnabled;

    // We will keep a list of generated audit trails to clean DBContext in case of failure.
    private readonly HashSet<object> _auditTrailsCache = [];

    #endregion

    #region Private Functions

    private List<RowAuditInfo> GetRowsAuditInfo()
    {
        var rowsAuditInfos = new List<RowAuditInfo>();

        if (!AuditingIsEnabled)
        {
            return rowsAuditInfos;
        }

        ChangeTracker.DetectChanges();

        // Check if there is anything that is worth auditing
        var changedEntries = ChangeTracker
            .Entries()
            .Where(IsEntryHasWhatToAudit);

        foreach (var entityEntry in changedEntries)
        {
            // Check if we have any settings for this entry
            var entitySettings = AuditSettings.Get(entityEntry.Entity.GetType());

            if (entitySettings is null) // No settings for this entity are set
            {
                continue;
            }

            // Create row audit info
            var rowAuditInfo = Mapper.ToModel(entityEntry, entitySettings);

            var columnsAuditInfo = entityEntry
                .Properties
                .Select(pe => GetColumnAuditInfos(entitySettings, entityEntry, pe, rowAuditInfo))
                .Where(cai => cai != null)
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
        EntityAuditSettings entitySettings,
        EntityEntry entityEntry,
        PropertyEntry propertyEntry,
        RowAuditInfo rowAuditInfo)
    {
        var propertySettings = AuditSettings.Get(entitySettings, propertyEntry.Metadata.Name);

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

    private static bool IsEntryHasWhatToAudit(EntityEntry entityEntry)
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

    private void UpdateAuditInfoChangedValues(RowAuditInfo rowAuditInfo)
    {
        var columnsToSkip = new HashSet<ColumnAuditInfo>();

        foreach (var columnAuditInfo in rowAuditInfo.ColumnsAuditInfos)
        {
            columnAuditInfo.NewValue = rowAuditInfo.Action == AuditAction.Deleted ? null : columnAuditInfo.TrackingPropertyEntry.CurrentValue;

            // Apply value mappers if any
            if (columnAuditInfo.AuditSettings.ValueMapper != null)
            {
                columnAuditInfo.OldValue = columnAuditInfo.AuditSettings.ValueMapper(columnAuditInfo.OldValue);
                columnAuditInfo.NewValue = columnAuditInfo.AuditSettings.ValueMapper(columnAuditInfo.NewValue);
            }

            // If the value did not change, we exclude the column from auditing, however, if we deleting, we keep the column
            if (rowAuditInfo.Action != AuditAction.Deleted
                && Equals(columnAuditInfo.NewValue, columnAuditInfo.OldValue))
            {
                columnsToSkip.Add(columnAuditInfo);
            }
        }

        rowAuditInfo.ColumnsAuditInfos.RemoveAll(columnsToSkip.Contains);

        // Setup the reference key
        if (rowAuditInfo.ColumnsChanges.Count > 0)
        {
            var primaryKey = rowAuditInfo
                    .ColumnsChanges[0]
                    .TrackingPropertyEntry
                    .EntityEntry
                    .Properties
                    .First(p => p.Metadata.IsPrimaryKey());

            rowAuditInfo.PrimaryKeyValue = primaryKey.CurrentValue!;
        }
    }

    private async Task PerformAuditingAsync(
        List<RowAuditInfo> rowsAuditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        foreach (var rowAuditInfo in rowsAuditInfo)
        {
            var auditRecord = await AuditSettings
                .AuditMappingCallBackAsync!(rowAuditInfo, customAuditInfo, cancellationToken)
                .ConfigureAwait(CaptureContext);

            if (auditRecord != null)
            {
                if (auditRecord.GetType() == AuditTrailTableModelType)
                {
                    _auditTrailsCache.Add(auditRecord);

                    Add(auditRecord);

                    continue;
                }
            }
            else
            {
                continue;
            }

            throw new InvalidOperationException($"{nameof(AuditSettings.AuditMappingCallBackAsync)} should return a Task<TAuditTrailModel?>.");
        }

        var savedCount = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(CaptureContext);

        if (savedCount != _auditTrailsCache.Count)
        {
            throw new InvalidOperationException("Something went wrong, not all audit trail records are saved.");
        }
    }

    #endregion

    #region Public Functions

    public AuditSettings AuditSettings { get; private set; }

    /// <summary>
    /// Determines if the context should capture the context of the current change, usually should be True when used in UI contexts
    /// </summary>
    public bool CaptureContext { get; set; }

    #region Constructors

    protected SimpleAuditContext(DbContextOptions<SimpleAuditContext> options)
        : base(options)
        => AuditSettings = new AuditSettings(this);

    protected SimpleAuditContext(DbContextOptions options)
        : base(options)
        => AuditSettings = new AuditSettings(this);

    protected SimpleAuditContext()
        => AuditSettings = new AuditSettings(this);

    #endregion

    /// <summary>
    /// The type of the model which is used for audit trail.
    /// </summary>
    public Type? AuditTrailTableModelType
        => AuditSettings.AuditTrailTableModelType;

    /// <summary>
    /// Configures the auditing table, auditing mapping callback, and specifies which tables/columns should be audited.
    /// </summary>
    /// <typeparam name="TAuditTrailTableModel">The model type of the table to audit.</typeparam>
    /// <param name="auditMappingCallBackAsync">
    /// A callback that takes <see cref="RowAuditInfo"/> and custom audit information, as well as a <see cref="CancellationToken"/>,
    /// and returns a task that creates and returns the audit record of type <typeparamref name="TAuditTrailTableModel"/>.
    /// </param>
    /// <remarks>
    /// If the <paramref name="auditMappingCallBackAsync"/> returns null, auditing of the current change will be skipped.
    /// </remarks>
    /// <returns>An <see cref="AuditTrailConfiguration{TAuditTrailTableModel}"/> instance.</returns>
    public AuditTrailConfiguration<TAuditTrailTableModel> ConfigureAuditTrail<TAuditTrailTableModel>(
        Func<RowAuditInfo, object?, CancellationToken, Task<TAuditTrailTableModel?>> auditMappingCallBackAsync)
        where TAuditTrailTableModel : class
        => new(this, auditMappingCallBackAsync);

    /// <summary>
    /// Configure which columns should be audited of the table of <typeparamref name="TTableModel"/> model type.
    /// </summary>
    /// <typeparam name="TTableModel">The table model type to configure.</typeparam>
    /// <param name="tableAlias">
    /// Sets the wanted alias for the given table.
    /// </param>
    /// <remarks>
    /// This can be used only after <paramref name="ConfigureAuditTrail"/> is being executed.
    /// </remarks>
    /// <returns>An <see cref="TableAuditConfiguration{TTableModel}"/> instance.</returns>
    public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>(string? tableAlias = null)
        where TTableModel : class
    {
        AuditSettings.ValidateIfAuditingConfigured();

        return new TableAuditConfiguration<TTableModel>(this, tableAlias);
    }

    /// <summary>
    /// Determines if auditing of changes will be performed.
    /// </summary>
    public bool AuditingIsEnabled
    {
        get => _auditingIsEnabled;
        set
        {
            if (value) // Check only if we are enabling auditing
            {
                AuditSettings.ValidateIfAuditingConfigured();

                if (!AuditSettings.HasEntitiesSettings)
                {
                    throw new InvalidOperationException("No tables are set for auditing.");
                }
            }

            _auditingIsEnabled = value;
        }
    }

    #region Saving

    public override int SaveChanges()
        => SaveChangesAsync()
        .GetAwaiter()
        .GetResult();

    public int SaveChanges(object? customProperties)
        => SaveChangesAsync(customProperties, CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await SaveChangesAsync(null, cancellationToken)
        .ConfigureAwait(CaptureContext);

    public async Task<int> SaveChangesAsync(object? customAuditInfo)
        => await SaveChangesAsync(customAuditInfo, CancellationToken.None)
        .ConfigureAwait(CaptureContext);

    public async Task<int> SaveChangesAsync(object? customAuditInfo, CancellationToken cancellationToken)
    {
        var rowsAuditInfos = GetRowsAuditInfo();

        if (rowsAuditInfos.Count == 0) // No audit trail needed
        {
            return await base
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(CaptureContext);
        }

        using var transaction = await Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(CaptureContext);

        try
        {
            var result = await base
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(CaptureContext);

            // Update all values in audit info objects
            rowsAuditInfos.ForEach(UpdateAuditInfoChangedValues);

            // Filter out not changed
            rowsAuditInfos = rowsAuditInfos
                .Where(rai => rai.ColumnsChanges.Count > 0)
                .ToList();

            if (rowsAuditInfos.Count > 0) // Perform audit if needed
            {
                await PerformAuditingAsync(rowsAuditInfos, customAuditInfo, cancellationToken)
                    .ConfigureAwait(CaptureContext);
            }

            await transaction
                .CommitAsync(cancellationToken)
                .ConfigureAwait(CaptureContext);

            _auditTrailsCache.Clear();

            return result;
        }
        catch (Exception)
        {
            await transaction
                .RollbackAsync(cancellationToken)
                .ConfigureAwait(CaptureContext);

            // Clean up DB Context from the generated trails
            RemoveRange(_auditTrailsCache);
            _auditTrailsCache.Clear();

            throw;
        }
    }

    #endregion

    #endregion
}
