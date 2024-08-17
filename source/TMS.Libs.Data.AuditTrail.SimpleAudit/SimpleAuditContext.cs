using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit;

public abstract class SimpleAuditContext : DbContext
{
    #region Vars

    private bool _auditingIsEnabled;

    // we will keep a list of generate audit trails to clean DBContext in case of failure.
    private readonly List<object> _auditTrails = [];

    #endregion

    #region Private

    private bool ValidateIfAuditingConfigured()
    {
        if (this.AuditTrailTableModelType is null || this.AuditMappingCallBackAsync is null)
        {
            throw new InvalidOperationException("Auditing is not configured yet.");
        }

        return true;
    }

    private List<RowAuditInfo> GetRowsAuditInfo()
    {
        var rowsAuditInfo = new List<RowAuditInfo>();

        if (!this.AuditingIsEnabled || this.EntitiesAuditSettings.Count == 0)
        {
            return rowsAuditInfo;
        }

        this.ChangeTracker.DetectChanges();

        // check if there is anything that worth to audit
        var changedEntries = this.ChangeTracker
            .Entries()
            .Where(IsEntryHasWhatToAudit);

        foreach (var entityEntry in changedEntries)
        {
            // check if we have any settings for this entry
            var entitySettings = this.EntitiesAuditSettings
                .SingleOrDefault(s => s.EntityType == entityEntry.Entity.GetType());

            if (entitySettings is null) // no settings for this entity is set
            {
                continue;
            }

            // create row audit info, and loop over all changed columns
            var rowAuditInfo = new RowAuditInfo(
                    this.GetSQLTableName(entityEntry),
                    Mapping.ToModel(entityEntry.State),
                    this.GetTableModelType(entityEntry),
                    entityEntry);

            foreach (var propertyEntry in entityEntry.Properties)
            {
                // check if the property has settings, value mapper, and its value actually is changed
                if (entitySettings.AuditableProperties.TryGetValue(propertyEntry.Metadata.Name, out var valueMappingCallBack)
                    && IsPropertyValueChanged(entityEntry, propertyEntry))
                {
                    // create column audit info
                    var columnChanges = new ColumnAuditInfo(
                        this.GetSQLColumnName(entityEntry, propertyEntry),
                        propertyEntry.Metadata.Name,
                        propertyEntry.Metadata.ClrType
                        )
                    {
                        // EF assigns to Original value same value as new value on insertion
                        OldValue = entityEntry.State == EntityState.Added ? null : propertyEntry.OriginalValue,
                        NewValue = entityEntry.State == EntityState.Deleted ? null : propertyEntry.CurrentValue,
                    };

                    // if we have value mapper, apply it
                    if (valueMappingCallBack != null)
                    {
                        columnChanges.OldValue = valueMappingCallBack(columnChanges.OldValue);
                        columnChanges.NewValue = valueMappingCallBack(columnChanges.NewValue);
                    }

                    rowAuditInfo.InternalColumnsChanges.Add(columnChanges);
                }
            }

            // if no columns are worth tracking, then skip this entity
            if (rowAuditInfo.InternalColumnsChanges.Count > 0)
            {
                rowsAuditInfo.Add(rowAuditInfo);
            }
        }

        return rowsAuditInfo;
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

    private EntityAuditSettings? GetAuditSettings(EntityEntry entityEntry, PropertyEntry propertyEntry)
    =>
        this.EntitiesAuditSettings
        .SingleOrDefault(s => s.EntityType == entityEntry.Entity.GetType());

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

            var auditRecord = await this.AuditMappingCallBackAsync!(rowAuditInfo, customAuditInfo, cancellationToken);

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

            throw new InvalidOperationException("AuditMappingCallBackAsync should return a Task<TAuditTrailModel?>.");
        }

        var savedCount = await base.SaveChangesAsync(cancellationToken);

        if (savedCount != this._auditTrails.Count)
        {
            throw new InvalidOperationException("Something went wrong, not all audit trail records are saved.");
        }
    }

    #endregion

    #region Internal

    internal Func<RowAuditInfo, object?, CancellationToken, Task<object?>>? AuditMappingCallBackAsync { get; set; }

    internal List<EntityAuditSettings> EntitiesAuditSettings { get; } = [];


    #endregion

    #region Public

    protected SimpleAuditContext(DbContextOptions<SimpleAuditContext> options)
        : base(options)
    { }

    protected SimpleAuditContext(DbContextOptions options)
        : base(options)
    { }

    /// <summary>
    /// The type of the model which is used for audit trail.
    /// </summary>
    public Type? AuditTrailTableModelType { get; internal set; }

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
    /// <remarks>
    /// This can be used only after <paramref name="ConfigureAuditTrail"/> is being executed.
    /// </remarks>
    /// <returns>An <see cref="TableAuditConfiguration{TTableModel}"/> instance.</returns>
    public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>()
        where TTableModel : class
    {
        this.ValidateIfAuditingConfigured();

        return new(this);
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
                this.ValidateIfAuditingConfigured();
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

    public async Task<int> SaveChangesAsync()
        => await this.SaveChangesAsync(null, CancellationToken.None);

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
