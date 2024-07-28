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

    #endregion

    #region Private

    private bool ValidateIfAuditingConfigured()
    {
        if (AuditTrailTableModelType is null || AuditMappingCallBackAsync is null)
        {
            throw new InvalidOperationException("Auditing is not configured yet.");
        }

        return true;
    }

    private List<RowAuditInfo> GetRowsAuditInfo()
    {
        var rowsAuditInfo = new List<RowAuditInfo>();

        if (!AuditingIsEnabled || AuditableEntities.Count == 0)
        {
            return rowsAuditInfo;
        }

        ChangeTracker.DetectChanges();

        foreach (var entityEntry in ChangeTracker.Entries())
        {
            if (!IsAuditableEntity(entityEntry))
            {
                continue;
            }

            var rowAuditInfo = new RowAuditInfo(
                    this.GetSQLTableName(entityEntry),
                    Mapping.ToModel(entityEntry.State),
                    this.GetTableModelType(entityEntry))
            {
                TrackingEntityEntry = entityEntry
            };

            foreach (var propertyEntry in entityEntry.Properties)
            {
                if (IsAuditableProperty(entityEntry, propertyEntry) && IsPropertyValueChanged(entityEntry, propertyEntry))
                {
                    var columnChanges = new ColumnAuditInfo()
                    {
                        ColumnSQLName = this.GetSQLColumnName(entityEntry, propertyEntry),
                        // EF assigns to Original value same value as new value on insertion
                        OldValue = entityEntry.State == EntityState.Added ? null : propertyEntry.OriginalValue,
                        NewValue = entityEntry.State == EntityState.Deleted ? null : propertyEntry.CurrentValue
                    };

                    rowAuditInfo.ColumnsChanges.Add(columnChanges);
                }
            }

            // if no columns are changed, then skip this entity
            if (rowAuditInfo.ColumnsChanges.Count > 0)
            {
                rowsAuditInfo.Add(rowAuditInfo);
            }
        }

        return rowsAuditInfo;
    }

    private bool IsAuditableEntity(EntityEntry entityEntry)
        =>
            entityEntry.Entity != null
            && AuditableEntities.ContainsKey(entityEntry.Entity.GetType())
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

    private bool IsAuditableProperty(EntityEntry entityEntry, PropertyEntry propertyEntry)
    {
        if (AuditableEntities.TryGetValue(entityEntry.Entity.GetType(), out var auditedPropertiesNames))
        {
            return auditedPropertiesNames.Contains(propertyEntry.Metadata.Name);
            //var propertyName = propertyEntry.Metadata.Name;

            //return auditedPropertiesNames.Any(ce => IsMatchingExpression(ce, propertyName));
        }

        return false;
    }

    //private static bool IsMatchingExpression(LambdaExpression expression, string propertyName)
    //{
    //    if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression memberOperand)
    //    {
    //        return memberOperand.Member.Name == propertyName;
    //    }

    //    if (expression.Body is MemberExpression memberExpression)
    //    {
    //        return memberExpression.Member.Name == propertyName;
    //    }

    //    return false;
    //}

    private async Task PerformAuditingAsync(
        List<RowAuditInfo> rowsAuditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        if (rowsAuditInfo == null || rowsAuditInfo.Count == 0)
        {
            return;
        }

        var auditTrailRecords = 0;

        foreach (var rowAuditInfo in rowsAuditInfo)
        {
            // this is called after saving the outer changes, so if new rows inserted, we should now have the actual values for primary keys in case of auto-increment
            var primaryKey = rowAuditInfo
                        .TrackingEntityEntry!
                        .Properties
                        .First(p => p.Metadata.IsPrimaryKey());

            rowAuditInfo.PrimaryKeyValue = primaryKey.CurrentValue!;

            var auditRecordTask = AuditMappingCallBackAsync!.DynamicInvoke(rowAuditInfo, customAuditInfo, cancellationToken);

            if (auditRecordTask is Task task)
            {
                await task;

                if (task.GetType().IsGenericType && task.GetType().GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var resultProperty = task.GetType().GetProperty("Result");
                    var auditRecord = resultProperty?.GetValue(task);

                    if (auditRecord != null)
                    {
                        if (auditRecord.GetType() == AuditTrailTableModelType)
                        {
                            Add(auditRecord);
                            auditTrailRecords++;

                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            throw new InvalidOperationException("AuditMappingCallBackAsync should return a Task<TAuditTrailModel?>.");
        }

        var savedCount = await base.SaveChangesAsync(cancellationToken);

        if (savedCount != auditTrailRecords)
        {
            throw new InvalidOperationException("Not all audit trail records are saved.");
        }
    }

    #endregion

    #region Internal

    internal Delegate? AuditMappingCallBackAsync { get; set; }

    //internal Dictionary<Type, List<LambdaExpression>> AuditableEntities { get; } = [];

    internal Dictionary<Type, List<string>> AuditableEntities { get; } = [];


    #endregion

    #region Public

    public SimpleAuditContext(DbContextOptions<SimpleAuditContext> options) : base(options) { }

    public SimpleAuditContext(DbContextOptions options) : base(options) { }

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
        Func<RowAuditInfo, object, CancellationToken, Task<TAuditTrailTableModel?>> auditMappingCallBackAsync)
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
        ValidateIfAuditingConfigured();

        return new(this);
    }

    /// <summary>
    /// Determines if auditing of changes will be performed.
    /// </summary>
    public bool AuditingIsEnabled
    {
        get => _auditingIsEnabled;
        set
        {
            if (value) // check only if we enabling auditing
            {
                ValidateIfAuditingConfigured();
            }

            _auditingIsEnabled = value;
        }
    }

    #region Saving

    public override int SaveChanges()
        => SaveChangesAsync().GetAwaiter().GetResult();

    public int SaveChanges(dynamic? customProperties)
        => SaveChangesAsync(customProperties, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => await SaveChangesAsync(null, cancellationToken);

    public async Task<int> SaveChangesAsync()
        => await SaveChangesAsync(null, CancellationToken.None);

    public async Task<int> SaveChangesAsync(object? customAuditInfo)
        => await SaveChangesAsync(customAuditInfo, CancellationToken.None);

    public async Task<int> SaveChangesAsync(object? customAuditInfo, CancellationToken cancellationToken)
    {
        var rowsAuditInfo = GetRowsAuditInfo();

        if (rowsAuditInfo.Count == 0) // no audit trail needed
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);

            await PerformAuditingAsync(rowsAuditInfo, customAuditInfo, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    #endregion

    #endregion
}
