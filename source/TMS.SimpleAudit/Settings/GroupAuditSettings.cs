using Microsoft.EntityFrameworkCore;

using System.Collections.ObjectModel;
using System.Linq.Expressions;

using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.Help;
using TMS.SimpleAudit.InfoModels;

namespace TMS.SimpleAudit.Settings;

public sealed class GroupAuditSettings<TGroupAuditTrailModel> : IGroupAuditSettingsInternal, IGroupAuditSettings
    where TGroupAuditTrailModel : class
{
    #region Private Vars

    private readonly Dictionary<Type, IEntityAuditSettingsInternal> _entitiesSettings = [];
    private readonly SimpleAuditManager _manager;
    private readonly Type _auditTrailModelType;
    private Func<DbContext, TGroupAuditTrailModel, CancellationToken, Task<bool>> _auditRecordExistenceCheck;

    #endregion

    #region Private Functions

    private EntityAuditSettings<TTableModel> RequestEntityAuditSettings<TTableModel>(
        string? tableAlias,
        string referenceColumnName,
        Func<TTableModel, bool>? isSoftDeletedCallBack)
    where TTableModel : class
    {
        var tableModelType = typeof(TTableModel);

        if (!_entitiesSettings.TryGetValue(tableModelType, out var entitySettings))
        {
            entitySettings = new EntityAuditSettings<TTableModel>(
                _manager,
                tableModelType,
                this,
                _manager.DataBaseContext.GetSQLTableName(tableModelType));

            _entitiesSettings[tableModelType] = entitySettings;
        }

        var entitySettingsClass = (EntityAuditSettings<TTableModel>)entitySettings;

        entitySettingsClass.TableNameAlias = tableAlias;
        entitySettingsClass.ReferenceColumnName = referenceColumnName;
        entitySettingsClass.IsSoftDeletedCallBack = isSoftDeletedCallBack;

        return entitySettingsClass;
    }

    private Func<DbContext, TGroupAuditTrailModel, CancellationToken, Task<bool>> CreateAuditRecordExistenceCheck()
    {
        var entityType = _manager.DataBaseContext.Model.FindEntityType(typeof(TGroupAuditTrailModel));
        var properties = entityType!.GetProperties();

        // Define parameters for the expression
        var dbContextParam = Expression.Parameter(typeof(DbContext), "context");
        var entityParam = Expression.Parameter(typeof(TGroupAuditTrailModel), "entity");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        // Call DbContext.Set<TEntity>()
        var setMethod = typeof(DbContext)
            .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(typeof(TGroupAuditTrailModel));

        var dbSet = Expression.Call(dbContextParam, setMethod);

        // Define the parameter 'e' for the query (representing the entity in the queryable)
        var entityInDbParam = Expression.Parameter(typeof(TGroupAuditTrailModel), "e");

        // Start building the query (dbSet.AsQueryable())
        var queryExpr = dbSet;

        // Build expression to compare all properties
        Expression? predicateBody = null;

        // Build expression to compare all properties
        foreach (var property in properties)
        {
            // Get the type of the property
            var propertyType = property.ClrType;

            // Create expression for entity's property value
            var entityPropertyExpr = Expression.Property(entityParam, property.PropertyInfo!);

            // Create expression for database property using EF.Property with the correct type
            var dbPropertyExpr = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [propertyType],
                entityInDbParam,
                Expression.Constant(property.Name)
            );

            // Build comparison expression: dbProperty == entityProperty
            var comparisonExpr = Expression.Equal(dbPropertyExpr, entityPropertyExpr);

            // Combine expressions with AND
            predicateBody = predicateBody == null ? comparisonExpr : Expression.AndAlso(predicateBody, comparisonExpr);
        }

        // Build the full lambda expression for the predicate (e => comparisons)
        var predicateLambda = Expression.Lambda<Func<TGroupAuditTrailModel, bool>>(predicateBody!, entityInDbParam);

        // Apply the predicate to the query (queryExpr.Where(e => comparisons))
        queryExpr = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [typeof(TGroupAuditTrailModel)],
            queryExpr,
            predicateLambda
        );

        var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync)
                            && m.GetParameters().Length == 2)
            ?.MakeGenericMethod(typeof(TGroupAuditTrailModel));


        var anyAsyncExpr = Expression
            .Call(
                anyAsyncMethod!,
                queryExpr,
                cancellationTokenParam);

        // Compile the lambda expression
        var lambda = Expression
            .Lambda<Func<DbContext, TGroupAuditTrailModel, CancellationToken, Task<bool>>>(
                anyAsyncExpr,
                dbContextParam, entityParam, cancellationTokenParam);

        return lambda.Compile();
    }

    #endregion

    #region Internal Constructors

    internal GroupAuditSettings(SimpleAuditManager manager)
    {
        _manager = manager;
        _auditRecordExistenceCheck = CreateAuditRecordExistenceCheck();
        _auditTrailModelType = typeof(TGroupAuditTrailModel);
    }

    #endregion

    #region Internal Functions

    async Task<bool> IGroupAuditSettingsInternal.AuditRecordExistenceCheckAsync(
        DbContext dbContext,
        object auditModel,
        CancellationToken cancellationToken)
        => await _auditRecordExistenceCheck(dbContext, (TGroupAuditTrailModel)auditModel, cancellationToken)
            .ConfigureAwait(_manager.CaptureThreadContext);

    void IGroupAuditSettingsInternal.Validate()
    {
        if (_entitiesSettings.Count < 1)
        {
            throw new AuditConfigurationException($"The group for {_auditTrailModelType} has no configured table models.");
        }

        _entitiesSettings.Values.ToList().ForEach(es => es.Validate());
    }

    IEntityAuditSettingsInternal? IGroupAuditSettingsInternal.GetEntityAuditSettings(Type tableModelType)
        => _entitiesSettings
            .TryGetValue(tableModelType, out var settings) ? settings : null;

    /// <summary>
    /// Add or update table model and list of properties to Audit.
    /// </summary>
    internal void SetPropertyAuditSettings<TTableModel>(
        string? tableAlias,
        List<string> propertiesNames,
        string referenceColumnName,
        Func<TTableModel, bool>? isSoftDeletedCallBack)
    where TTableModel : class
    {
        var entitySettings = (IEntityAuditSettingsInternal)RequestEntityAuditSettings(tableAlias, referenceColumnName, isSoftDeletedCallBack);

        foreach (var propertyName in propertiesNames)
        {
            var propertySettings = entitySettings.RequestPropertyAuditSettings(propertyName);

            propertySettings.ValueMapper = null;
            propertySettings.ValueMapperAsync = null;
            propertySettings.ColumnNameAlias = null;
        }
    }

    /// <summary>
    /// Add or update the Audit settings for table model and singly property, with fine tunning.
    /// </summary>
    internal void SetPropertyAuditSettings<TTableModel>(
        string? tableAlias,
        string propertyName,
        Func<object?, object?>? valueMapper,
        string? columnAlias,
        string referenceColumnName,
        Func<TTableModel, bool>? isSoftDeletedCallBack)
    where TTableModel : class
    {
        var entitySettings = (IEntityAuditSettingsInternal)RequestEntityAuditSettings(tableAlias, referenceColumnName, isSoftDeletedCallBack);

        var propertySettings = entitySettings.RequestPropertyAuditSettings(propertyName);

        propertySettings.ValueMapperAsync = null;
        propertySettings.ValueMapper = valueMapper;
        propertySettings.ColumnNameAlias = columnAlias;
    }

    /// <summary>
    /// Add or update the Audit settings for table model and singly property, with fine tunning.
    /// </summary>
    internal void SetPropertyAuditSettings<TTableModel>(
        string? tableAlias,
        string propertyName,
        Func<DbContext, object, object?, CancellationToken, Task<object?>>? valueMapperAsync,
        string? columnAlias,
        string referenceColumnName,
        Func<TTableModel, bool>? isSoftDeletedCallBack)
    where TTableModel : class
    {
        var entitySettings = (IEntityAuditSettingsInternal)RequestEntityAuditSettings(tableAlias, referenceColumnName, isSoftDeletedCallBack);

        var propertySettings = entitySettings.RequestPropertyAuditSettings(propertyName);

        propertySettings.ValueMapper = null;
        propertySettings.ValueMapperAsync = valueMapperAsync;
        propertySettings.ColumnNameAlias = columnAlias;
    }

    /// <summary>
    /// Remove table model from Audit.
    /// </summary>
    void IGroupAuditSettingsInternal.RemoveEntityAuditSettings(Type tableModelType)
    {
        if (!_entitiesSettings.Remove(tableModelType))
        {
            throw new AuditConfigurationException($"The table {tableModelType.Name} is not yet configured for audit.");
        }

        if (_entitiesSettings.Count == 0)
        {
            throw new AuditConfigurationException("No tables left for auditing.");
        }
    }

    void IGroupAuditSettingsInternal.RemovePropertyAuditSettings(Type tableModelType, List<string> propertiesNames)
    {
        if (!_entitiesSettings.TryGetValue(tableModelType, out var entitySettings))
        {
            throw new AuditConfigurationException($"The table {tableModelType.Name} is not yet configured for audit to remove anything from.");
        }

        propertiesNames.ForEach(entitySettings.RemovePropertyAuditSettings);
    }

    async Task<IEnumerable<object>> IGroupAuditSettingsInternal.GenerateAuditRecordsAsync(
        RowAuditInfo rowAuditInfo, 
        object? customAuditInfo, 
        CancellationToken cancellationToken)
    {
        if(AuditRecordGeneratorAsync is not null)
        {
            var record = await AuditRecordGeneratorAsync(rowAuditInfo, customAuditInfo, cancellationToken);

            if (record is null)
            {
                return [];
            }

            return [record];
        }
        else
        {
            return (await AuditRecordsGeneratorAsync!(rowAuditInfo, customAuditInfo, cancellationToken)).Cast<object>();
        }
    }

    #endregion

    #region Internal Properties

    Type IGroupAuditSettingsInternal.AuditTrailModelType => _auditTrailModelType;

    #endregion

    #region Public Properties

    ReadOnlyCollection<IEntityAuditSettings> IGroupAuditSettings.EntityAuditSettings
        => new(_entitiesSettings.Values.Cast<IEntityAuditSettings>().ToList());

    Type IGroupAuditSettings.AuditTrailTableModelType => _auditTrailModelType;

    public Func<RowAuditInfo, object?, CancellationToken, Task<TGroupAuditTrailModel?>>? AuditRecordGeneratorAsync { get; internal set; }

    public Func<RowAuditInfo, object?, CancellationToken, Task<List<TGroupAuditTrailModel>>>? AuditRecordsGeneratorAsync { get; internal set; }


    #endregion

}
