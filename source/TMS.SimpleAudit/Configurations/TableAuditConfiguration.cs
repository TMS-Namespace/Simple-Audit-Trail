using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Help;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Configurations;

public sealed class TableAuditConfiguration<TGroupAuditModel, TTableModel>
    where TTableModel : class
    where TGroupAuditModel : class
{
    #region Private Vars

    private readonly SimpleAuditManager _manager;
    private readonly Type _tableModelType = typeof(TTableModel);

    private readonly string? _tableAlias;
    private readonly GroupAuditSettings<TGroupAuditModel> _groupAuditSettings;
    private readonly string _referenceColumnName;
    private readonly Func<TTableModel, bool>? _isSoftDeletedCallBack;

    #endregion

    #region Private Functions

    private static bool IsAutoAuditableProperty(IProperty property, AutoExcludeColumnType exclusions)
    {
        if (exclusions.HasFlag(AutoExcludeColumnType.PrimaryKey)
            && property.IsPrimaryKey())
        {
            return false;
        }

        if (exclusions.HasFlag(AutoExcludeColumnType.ForeignKey)
            && property.IsForeignKey())
        {
            return false;
        }

        if (exclusions.HasFlag(AutoExcludeColumnType.Virtual)
            && !string.IsNullOrEmpty(property.GetComputedColumnSql()))
        {
            return false;
        }

        return true;
    }

    private IEnumerable<IProperty> GetColumnsProperties()
        => _manager
            .DataBaseContext
            .GetEntityType<TTableModel>()
            .GetProperties()
            .Where(IsColumnProperty);

    // checks if the property actually mapped to an SQL column.
    private static bool IsColumnProperty(IProperty property)
        => property.GetTableColumnMappings().Any();

    private static void ValidateColumnProperty(IProperty property)
    {
        if (!IsColumnProperty(property))
        {
            throw new AuditConfigurationException($"The property {property.Name} is not mapped to any table column.");
        }
    }

    private List<IProperty> ValidateAndGetProperties(List<Expression<Func<TTableModel, object?>>> expressions)
    {
        if (expressions.Count == 0)
        {
            throw new AuditConfigurationException("No columns to audit are provided.");
        }

        expressions.ForEach(ValidateSimpleMemberAccess);

        var properties = expressions.Select(GetPropertyFromExpression).ToList();

        properties.ForEach(ValidateColumnProperty);

        return properties;
    }

    private IProperty GetPropertyFromExpression(Expression<Func<TTableModel, object?>> expression)
    {
        var member = expression.Body as MemberExpression
                    ?? (expression.Body as UnaryExpression)?.Operand as MemberExpression;

        if (member == null)
        {
            throw new AuditConfigurationException("Expression is not a valid member expression.");
        }

        var entityType = _manager.DataBaseContext.GetEntityType<TTableModel>();

        return entityType.FindProperty(member.Member.Name)
            ?? throw new AuditConfigurationException($"The property {member.Member.Name} is not found in {_tableModelType.Name} table model.");
    }

    private static void ValidateSimpleMemberAccess(Expression<Func<TTableModel, object?>> expression)
    {
        // for the case : x=> x.Prop where Prop is Object
        if (expression.Body is MemberExpression memberExpression
            && memberExpression.Expression is ParameterExpression)
        {
            return;
        }

        // for the case : x=> x.Prop where Prop is not Object, where it will be explicitly converted to Object due to our expressions signature
        if (expression.Body is UnaryExpression unaryExpression
            && unaryExpression.Operand is MemberExpression operandMemberExpression
            && operandMemberExpression.Expression is ParameterExpression)
        {
            return;
        }

        throw new AuditConfigurationException("Only simple member access expressions are accepted: " + expression.Body);
    }

    #endregion

    #region Internal Constructors

    internal TableAuditConfiguration(
        SimpleAuditManager manager,
        IGroupAuditSettingsInternal groupAuditSettings,
        string? tableAlias,
        Expression<Func<TTableModel, object?>>? referenceColumnExpression,
        Func<TTableModel, bool>? isSoftDeletedCallBack)
    {
        _manager = manager;
        _tableAlias = tableAlias;
        _groupAuditSettings = (GroupAuditSettings<TGroupAuditModel>)groupAuditSettings;
        _isSoftDeletedCallBack = isSoftDeletedCallBack;

        if (!_manager.DataBaseContext.IsTableType<TTableModel>())
        {
            throw new AuditConfigurationException($"The type {_tableModelType.Name} is not recognized as a table Model.");
        }

        if (referenceColumnExpression is null)
        {
            _referenceColumnName = GetColumnsProperties()
                .FirstOrDefault(p => p.IsPrimaryKey())
                ?.Name
                ?? throw new AuditConfigurationException($"The table of model type {_tableModelType.Name} should have primary key to be auditable, or specify a unique reference column manually.");
        }
        else
        {
            var property = ValidateAndGetProperties([referenceColumnExpression])[0];

            if(!property.GetContainingIndexes().Any(index => index.IsUnique))
            {
                throw new AuditConfigurationException($"The column {property.Name} of table model {_tableModelType} has no unique index, it can't be used as a reference column.");
            }

            _referenceColumnName = property.Name;
        }

        if (_manager.AuditSettings.GetGroupSettings(_tableModelType) is not null)
        {
            throw new AuditConfigurationException($"The table {_tableModelType.Name} that is dedicated for audit trail can't be audited.");
        }

        var tableAuditGroupSettings = _manager
            .AuditSettings
            .GetGroupSettingsByTableModelType(_tableModelType);

        if (tableAuditGroupSettings is not null && tableAuditGroupSettings != groupAuditSettings)
        {
            throw new AuditConfigurationException($"The table {_tableModelType.Name} auditing is already configured in another group.");
        }
    }

    // this one mainly to simplify dynamic creation of this class
    internal TableAuditConfiguration(SimpleAuditManager manager, IGroupAuditSettingsInternal groupAuditSettings)
        : this(manager, groupAuditSettings, null, null, null)
    { }

    #endregion

    #region Public Functions

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> StartAuditing()
    {
        _manager.AuditingIsEnabled = true;
        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> AuditAllColumns(AutoExcludeColumnType autoExclusions = AutoExcludeColumnType.None)
    {
        var propertiesNames = GetColumnsProperties()
            .Where(p => IsAutoAuditableProperty(p, autoExclusions))
            .Select(p => p.Name)
            .ToList();

        if (propertiesNames.Count == 0)
        {
            throw new AuditConfigurationException($"The table model {_tableModelType.Name} has no auditable columns.");
        }

        _groupAuditSettings
            .SetPropertyAuditSettings(
                _tableAlias,
                propertiesNames,
                _referenceColumnName,
                _isSoftDeletedCallBack);

        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> ExcludeTableFromAuditing()
    {
        ((IGroupAuditSettingsInternal)_groupAuditSettings)
            .RemoveEntityAuditSettings(_tableModelType);

        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> AuditColumns(
        Expression<Func<TTableModel, object?>> includedColumn,
        params Expression<Func<TTableModel, object?>>[] moreIncludedColumns)
    {
        var includedColumns = moreIncludedColumns.ToList();
        includedColumns.Add(includedColumn);

        var propertiesNames = ValidateAndGetProperties(includedColumns)
            .Select(p => p.Name)
            .ToList();

        _groupAuditSettings
            .SetPropertyAuditSettings(
                _tableAlias,
                propertiesNames,
                _referenceColumnName,
                _isSoftDeletedCallBack);

        return this;
    }

    /// <summary>
    /// Configure table's column for auditing, with column mapping call back.
    /// </summary>
    public TableAuditConfiguration<TGroupAuditModel, TTableModel> AuditColumn(
        Expression<Func<TTableModel, object?>> includedColumn,
        Func<object?, object?>? valueMapperCallBack = null,
        string? columnAlias = null)
    {
        var propertiesNames = ValidateAndGetProperties([includedColumn])
            .Select(p => p.Name)
            .ToList();

        _groupAuditSettings
            .SetPropertyAuditSettings(
                _tableAlias,
                propertiesNames[0],
                valueMapperCallBack,
                columnAlias,
                _referenceColumnName,
                _isSoftDeletedCallBack);

        return this;
    }

    /// <summary>
    /// Configure table's column for auditing, with column mapping call back that supplies also row model.
    /// </summary>
    public TableAuditConfiguration<TGroupAuditModel, TTableModel> AuditColumn(
        Expression<Func<TTableModel, object?>> includedColumn,
        Func<DbContext, object, object?, CancellationToken, Task<object?>>? valueMapperCallBackAsync,
        string? columnAlias = null)
    {
        var propertiesNames = ValidateAndGetProperties([includedColumn])
            .Select(p => p.Name)
            .ToList();

        _groupAuditSettings
            .SetPropertyAuditSettings(
                _tableAlias,
                propertiesNames[0],
                valueMapperCallBackAsync,
                columnAlias,
                _referenceColumnName,
                _isSoftDeletedCallBack);

        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> ExcludeColumnsFromAuditing(
        Expression<Func<TTableModel, object?>> excludedColumn,
        params Expression<Func<TTableModel, object?>>[] moreExcludedColumns)
    {
        var excludedColumns = moreExcludedColumns.ToList();
        excludedColumns.Add(excludedColumn);

        var propertiesNamesToExclude = ValidateAndGetProperties(excludedColumns)
            .Select(p => p.Name)
            .ToList();

        ((IGroupAuditSettingsInternal)_groupAuditSettings)
            .RemovePropertyAuditSettings(_tableModelType, propertiesNamesToExclude);

        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TAnotherEntity> ConfigureTableAudit<TAnotherEntity>(
        string? tableAlias = null,
        Expression<Func<TAnotherEntity, object?>>? referenceColumnExpression = null,
        Func<TAnotherEntity, bool>? isSoftDeleted = null)
        where TAnotherEntity : class
        => new(
            _manager,
            _groupAuditSettings,
            tableAlias,
            referenceColumnExpression,
            isSoftDeleted);

    #endregion

}
