using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using System.Linq.Expressions;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configurators;

public sealed class TableAuditConfiguration<TTableModel>
    where TTableModel : class
{
    #region Vars

    private readonly SimpleAuditContext _dbContext;
    private readonly Type _entityType = typeof(TTableModel);

    #endregion

    #region Private

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
        => _dbContext
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
            throw new InvalidOperationException($"The property {property.Name} is not mapped to any table column.");
        }
    }

    private List<IProperty> ValidateAndGetProperties(List<Expression<Func<TTableModel, object?>>> expressions)
    {
        if (expressions.Count == 0)
        {
            throw new InvalidOperationException("No columns to audit are provided.");
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
            throw new InvalidOperationException("Expression is not a valid member expression.");
        }

        var entityType = _dbContext.GetEntityType<TTableModel>();

        return entityType.FindProperty(member.Member.Name)
            ?? throw new InvalidOperationException($"The property {member.Member.Name} is not found in {_entityType.Name} table model.");
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

        throw new InvalidOperationException("Only simple member access expressions are accepted: " + expression.Body);
    }

    #endregion

    #region Internal

    private string? _tableAlias;

    internal TableAuditConfiguration(SimpleAuditContext dbContext, string? tableAlias)
    {
        _dbContext = dbContext;
        _tableAlias = tableAlias;

        if (!_dbContext.IsTableType<TTableModel>())
        {
            throw new InvalidOperationException($"The type {_entityType.Name} is not recognized as a table Model.");
        }

        if (!GetColumnsProperties().Any(p => p.IsPrimaryKey()))
        {
            throw new InvalidOperationException($"The table of model type {_entityType.Name} should have primary key to be auditable.");
        }

        if (_entityType == _dbContext.AuditTrailTableModelType)
        {
            throw new InvalidOperationException("The table that is dedicated for audit trail can't be audited.");
        }
    }

    #endregion

    #region Public

    public TableAuditConfiguration<TTableModel> StartAuditing()
    {
        _dbContext.AuditingIsEnabled = true;
        return this;
    }

    public TableAuditConfiguration<TTableModel> AuditAllColumns(AutoExcludeColumnType autoExclusions = AutoExcludeColumnType.None)
    {
        var propertiesNames = GetColumnsProperties()
            .Where(p => IsAutoAuditableProperty(p, autoExclusions))
            .Select(p => p.Name)
            .ToList();

        if (propertiesNames.Count == 0)
        {
            throw new InvalidOperationException($"The table model {_entityType.Name} has no auditable columns.");
        }

        _dbContext.AuditSettings.Set(_entityType, _tableAlias, propertiesNames);

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeTableFromAuditing()
    {
        _dbContext.AuditSettings.Remove(_entityType);
        return this;
    }

    public TableAuditConfiguration<TTableModel> AuditColumns(
        Expression<Func<TTableModel, object?>> includedColumn,
        params Expression<Func<TTableModel, object?>>[] moreIncludedColumns)
    {
        var includedColumns = moreIncludedColumns.ToList();
        includedColumns.Add(includedColumn);

        var propertiesNames = ValidateAndGetProperties(includedColumns)
            .Select(p => p.Name)
            .ToList();

        _dbContext
            .AuditSettings
            .Set(_entityType, _tableAlias, propertiesNames);

        return this;
    }

    /// <summary>
    /// Configure table's column for auditing, with column mapping call back.
    /// </summary>
    public TableAuditConfiguration<TTableModel> AuditColumn(
        Expression<Func<TTableModel, object?>> includedColumn,
        Func<object?, object?>? valueMapperCallBack = null,
        string? columnAlias = null)
    {
        var propertiesNames = ValidateAndGetProperties([includedColumn])
            .Select(p => p.Name)
            .ToList();

        _dbContext.AuditSettings.Set(
            _entityType,
            _tableAlias,
            propertiesNames[0],
            valueMapperCallBack,
            columnAlias);

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeColumnsFromAuditing(
        Expression<Func<TTableModel, object?>> excludedColumn,
        params Expression<Func<TTableModel, object?>>[] moreExcludedColumns)
    {
        var excludedColumns = moreExcludedColumns.ToList();
        excludedColumns.Add(excludedColumn);

        var propertiesNamesToExclude = ValidateAndGetProperties(excludedColumns)
            .Select(p => p.Name)
            .ToList();

        _dbContext
            .AuditSettings
            .Remove(_entityType, propertiesNamesToExclude);

        return this;
    }

    public TableAuditConfiguration<TAnotherEntity> ConfigureTableAudit<TAnotherEntity>(string? tableAlias = null)
        where TAnotherEntity : class
        => new(_dbContext, tableAlias);

    #endregion
}
