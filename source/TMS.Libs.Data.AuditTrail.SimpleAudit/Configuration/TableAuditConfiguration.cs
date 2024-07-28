using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using System.Linq.Expressions;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;

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
        var result = true;

        if (exclusions.HasFlag(AutoExcludeColumnType.PrimaryKey))
        {
            result = result && !property.IsPrimaryKey();
        }

        if (exclusions.HasFlag(AutoExcludeColumnType.ForeignKey))
        {
            result = result && !property.IsForeignKey();
        }

        if (exclusions.HasFlag(AutoExcludeColumnType.Virtual))
        {
            result = result && string.IsNullOrEmpty(property.GetComputedColumnSql());
        }

        return result;
    }

    /// <summary>
    /// Retrieves all properties that are mapped to SQL columns
    /// </summary>
    /// <returns></returns>
    private IEnumerable<IProperty> GetColumnsProperties()
        => _dbContext
        .GetEntityType<TTableModel>()
        .GetProperties()
        .Where(p => ValidateColumnProperty(p));


    /// <summary>
    /// checks if the property actually mapped to an SQL column.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    private static bool ValidateColumnProperty(IProperty property)
    => property.GetTableColumnMappings().Any();


    private List<IProperty> ValidateAndGetProperties(List<Expression<Func<TTableModel, object?>>> expressions)
    {
        if (!expressions.Any())
        {
            throw new InvalidOperationException("No columns to audit are provided.");
        }

        expressions.ToList().ForEach(e => ValidateSimpleMemberAccess(e));

        var properties = expressions.Select(GetPropertyFromExpression).ToList();

        properties.ForEach(p => ValidateColumnProperty(p));

        return properties;

    }

    private void SetAuditableEntities(List<string> propertiesNames)
    {
        if (_dbContext.AuditableEntities.TryGetValue(_entityType, out var value))
        {
            value.Clear();
            value.AddRange(propertiesNames);
        }
        else
        {
            _dbContext.AuditableEntities.Add(_entityType, propertiesNames);
        }
    }

    private IProperty GetPropertyFromExpression (Expression<Func<TTableModel, object?>> expression)
    {
        var member = expression.Body as MemberExpression
                    ?? (expression.Body as UnaryExpression)?.Operand as MemberExpression;

        if (member == null)
        {
            throw new ArgumentException("Expression is not a valid member expression.");
        }

        var entityType = _dbContext.GetEntityType<TTableModel>();

        return entityType.FindProperty(member.Member.Name)
            ?? throw new ArgumentException($"The property {member.Member.Name} is not found in {typeof(TTableModel).Name} table model.");
    }

    private static bool ValidateSimpleMemberAccess(Expression<Func<TTableModel, object?>> expression)
    {
        // for the case : x=> x.Prop where Prop is Object
        if (expression.Body is MemberExpression memberExpression && memberExpression.Expression is ParameterExpression)
        {
            return true;
        }

        // for the case : x=> x.Prop where Prop is not Object, where it will be explicitly converted to Object due to our expressions signature
        if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression operandMemberExpression && operandMemberExpression.Expression is ParameterExpression)
        {
            return true;
        }

        throw new ArgumentException("Only simple member access expressions are accepted: " + expression.Body);
    }

    #endregion

    #region Internal

    internal TableAuditConfiguration(SimpleAuditContext dbContext)
    {
        _dbContext = dbContext;

        if (!_dbContext.IsTableType<TTableModel>())
        {
            throw new InvalidOperationException($"The type {_entityType.Name} is not recognized as a table Model.");
        }

        if (!GetColumnsProperties().Any(p => p.IsPrimaryKey()))
        {
            throw new InvalidOperationException($"The table of model type {_entityType.Name} should have primary key to be auditable.");
        }

        if (typeof(TTableModel) == _dbContext.AuditTrailTableModelType)
        {
            throw new InvalidOperationException("The table that dedicated for audit trail, can't be audited.");
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
            throw new InvalidOperationException($"The table model {_entityType} has no auditable columns.");
        }

        SetAuditableEntities(propertiesNames);

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeTableFromAuditing()
    {
        var existed = _dbContext.AuditableEntities.Remove(typeof(TTableModel));

        if (!existed)
        {
            throw new InvalidOperationException($"The table model type {_entityType.Name} is not configured for audit yet to be excluded.");
        }

        return this;
    }


    public TableAuditConfiguration<TTableModel>
        AuditColumns(
        Expression<Func<TTableModel, object?>> includedColumn,
        params Expression<Func<TTableModel, object?>>[] moreIncludedColumns)
    {
        var includedColumns = moreIncludedColumns.ToList();
        includedColumns.Add(includedColumn);

        //var expressions = includedColumns.Compile()(default!)!;

        var propertiesNames = ValidateAndGetProperties(includedColumns)
                            .Select(p => p.Name)
                            .ToList();

        SetAuditableEntities(propertiesNames);

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeColumnsFromAuditing(
        Expression<Func<TTableModel, object?>> excludedColumn, 
        params Expression<Func<TTableModel, object?>>[] moreExcludedColumns)
    {
        var excludedColumns = moreExcludedColumns.ToList();
        excludedColumns.Add(excludedColumn);

        var propertiesNamesToExclude = ValidateAndGetProperties(excludedColumns)
                        .Select(p => p.Name);

        if (_dbContext.AuditableEntities.TryGetValue(_entityType, out var auditedPropertiesNames))
        {
            foreach (var propertyName in propertiesNamesToExclude)
            {
                if (!auditedPropertiesNames.Remove(propertyName))
                {
                    throw new InvalidOperationException($"The property {propertyName} is not yet set to be audited.");
                }
            }
        }
        else
        {
            throw new InvalidOperationException($"The table {_entityType.Name} is not yet configured for audit.");
        }

        return this;
    }


    public TableAuditConfiguration<TAnotherEntity> ConfigureTableAudit<TAnotherEntity>()
        where TAnotherEntity : class
        => new(_dbContext);

    #endregion
}

