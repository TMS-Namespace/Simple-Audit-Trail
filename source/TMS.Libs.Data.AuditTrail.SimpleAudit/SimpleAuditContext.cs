using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Configurators;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit;

public abstract class SimpleAuditContext : DbContext
{
    #region Vars

    private bool _auditingIsEnabled;

    // we will keep a list of generate audit trails to clean DBContext in case of failure.
    private readonly List<object> _auditTrails = [];

    #endregion

    #region Private

    private List<RowAuditInfo> GetRowsAuditInfo()
    {
        var rowsAuditInfos = new List<RowAuditInfo>();

        if (!this.AuditingIsEnabled)
        {
            return rowsAuditInfos;
        }

        this.ChangeTracker.DetectChanges();

        // check if there is anything that worth to audit
        var changedEntries = this.ChangeTracker
            .Entries()
            .Where(IsEntryHasWhatToAudit);

        foreach (var entityEntry in changedEntries)
        {
            // check if we have any settings for this entry
            var entitySettings = this.AuditSettings.Get(entityEntry.Entity.GetType());

            if (entitySettings is null) // no settings for this entity is set
            {
                continue;
            }

            var columnsAuditInfo = entityEntry
                .Properties
                .Select(pe => GetColumnAuditInfos(entitySettings, entityEntry, pe))
                .Where(cai =>  cai != null);

            // if no columns are worth tracking, then skip this entity
            if (columnsAuditInfo is not null && columnsAuditInfo.Any())
            {
                // create row audit info
                var rowAuditInfo = Mapper.ToModel(entityEntry, entitySettings);

                rowAuditInfo.ColumnsAuditInfos.AddRange(columnsAuditInfo!);
                rowsAuditInfos.Add(rowAuditInfo);
            }
        }

        return rowsAuditInfos;
    }

    private ColumnAuditInfo? GetColumnAuditInfos(
        EntityAuditSettings entitySettings,
        EntityEntry entityEntry,
        PropertyEntry propertyEntry)
    {
        var propertySettings = this.AuditSettings.Get(entitySettings, propertyEntry.Metadata.Name);

        // check if the property has settings, and its value actually is changed
        if (propertySettings is not null && IsPropertyValueChanged(entityEntry, propertyEntry))
        {
            // create column audit info
            var columnChanges = Mapper.ToModel(propertyEntry, propertySettings);

            // EF assigns to Original value same value as new value on insertion
            columnChanges.OldValue = entityEntry.State == EntityState.Added ? null : propertyEntry.OriginalValue;
            columnChanges.NewValue = entityEntry.State == EntityState.Deleted ? null : propertyEntry.CurrentValue;

            // apply value mappers if any
            if (propertySettings.ValueMapper != null)
            {
                columnChanges.OldValue = propertySettings.ValueMapper(columnChanges.OldValue);
                columnChanges.NewValue = propertySettings.ValueMapper(columnChanges.NewValue);
            }

            return columnChanges;
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
        // if we inserting, EF considers all columns as modified
        if (entityEntry.State == EntityState.Added)
        {
            return propertyEntry.CurrentValue is not null;
        }

        // if we deleting, we considers all columns as modified, to keep record of deleted information
        if (entityEntry.State == EntityState.Deleted)
        {
            return true;
        }

        return propertyEntry.IsModified;
    }

    private async Task PerformAuditingAsync(
        List<RowAuditInfo> rowsAuditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        if (rowsAuditInfo == null || rowsAuditInfo.Count == 0)
        {
            return;
        }

        foreach (var rowAuditInfo in rowsAuditInfo)
        {
            // this is called after saving the outer changes, so if new rows inserted, we should now have the actual values for primary keys in case of auto-increment
            var primaryKey = rowAuditInfo
                    .TrackingEntityEntry
                    .Properties
                    .First(p => p.Metadata.IsPrimaryKey());

            rowAuditInfo.PrimaryKeyValue = primaryKey.CurrentValue!;

            var auditRecord = await this.AuditSettings
                .AuditMappingCallBackAsync!(rowAuditInfo, customAuditInfo, cancellationToken);

            if (auditRecord != null)
            {
                if (auditRecord.GetType() == this.AuditTrailTableModelType)
                {
                    this._auditTrails.Add(auditRecord);

                    this.Add(auditRecord);

                    continue;
                }
            }
            else
            {
                continue;
            }

            throw new InvalidOperationException($"{nameof(this.AuditSettings
                .AuditMappingCallBackAsync)} should return a Task<TAuditTrailModel?>.");
        }

        var savedCount = await base.SaveChangesAsync(cancellationToken);

        if (savedCount != this._auditTrails.Count)
        {
            throw new InvalidOperationException("Something went wrong, not all audit trail records are saved.");
        }
    }

    #endregion

    #region Public

    public AuditSettings AuditSettings { get; private set; }


    protected SimpleAuditContext(DbContextOptions<SimpleAuditContext> options)
        : base(options)
    => this.AuditSettings = new AuditSettings(this);

    protected SimpleAuditContext(DbContextOptions options)
        : base(options)
    => this.AuditSettings = new AuditSettings(this);

    /// <summary>
    /// The type of the model which is used for audit trail.
    /// </summary>
    public Type? AuditTrailTableModelType => this.AuditSettings.AuditTrailTableModelType;

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
        this.AuditSettings.ValidateIfAuditingConfigured();

        return new(this, tableAlias);
    }

    /// <summary>
    /// Determines if auditing of changes will be performed.
    /// </summary>
    public bool AuditingIsEnabled
    {
        get => this._auditingIsEnabled;
        set
        {
            if (value) // check only if we enabling auditing
            {
                this.AuditSettings.ValidateIfAuditingConfigured();

                if (!this.AuditSettings.HasEntitiesSettings)
                {
                    throw new InvalidOperationException("No tables are set for auditing.");
                }
            }

            this._auditingIsEnabled = value;
        }
    }

    #region Saving

    public override int SaveChanges()
        => this.SaveChangesAsync().GetAwaiter().GetResult();

    public int SaveChanges(object? customProperties)
        => this.SaveChangesAsync(customProperties, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await this.SaveChangesAsync(null, cancellationToken);

    public async Task<int> SaveChangesAsync(object? customAuditInfo)
        => await this.SaveChangesAsync(customAuditInfo, CancellationToken.None);

    public async Task<int> SaveChangesAsync(object? customAuditInfo, CancellationToken cancellationToken)
    {
        var rowsAuditInfo = this.GetRowsAuditInfo();

        if (rowsAuditInfo.Count == 0) // no audit trail needed
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        using var transaction = await this.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            await this.PerformAuditingAsync(rowsAuditInfo, customAuditInfo, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            this._auditTrails.Clear();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);

            // clean up DB Context from the generated trails
            this.RemoveRange(this._auditTrails);
            this._auditTrails.Clear();

            throw;
        }
    }

    #endregion

    #endregion
}
