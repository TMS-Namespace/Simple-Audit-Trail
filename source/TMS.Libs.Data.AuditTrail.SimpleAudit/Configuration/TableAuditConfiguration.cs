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

    // Retrieves all properties that are mapped to SQL columns
    private IEnumerable<IProperty> GetColumnsProperties()
        => this._dbContext
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
            throw new InvalidOperationException($"The property {property} is not mapped to any table column.");
        }
    }

    private List<IProperty> ValidateAndGetProperties(List<Expression<Func<TTableModel, object?>>> expressions)
    {
        if (expressions.Count == 0)
        {
            throw new InvalidOperationException("No columns to audit are provided.");
        }

        expressions.ToList().ForEach(e => ValidateSimpleMemberAccess(e));

        var properties = expressions.Select(this.GetPropertyFromExpression).ToList();

        properties.ForEach(ValidateColumnProperty);

        return properties;
    }

    private void SetAuditableProperties(List<string> propertiesNames)
    {
        var entitySettings = this._dbContext
            .EntitiesAuditSettings
            .SingleOrDefault(s => s.EntityType == this._entityType);

        if (entitySettings == null)
        {
            entitySettings = new() { EntityType = this._entityType };
            this._dbContext.EntitiesAuditSettings.Add(entitySettings);
        }

        foreach (var propertyName in propertiesNames)
        {
            if (entitySettings.AuditableProperties.ContainsKey(propertyName))
            {
                entitySettings.AuditableProperties[propertyName] = null;
            }
            else
            {
                entitySettings.AuditableProperties.Add(propertyName, null);
            }
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

        var entityType = this._dbContext.GetEntityType<TTableModel>();

        return entityType.FindProperty(member.Member.Name)
            ?? throw new ArgumentException($"The property {member.Member.Name} is not found in {this._entityType.Name} table model.");
    }

    private static bool ValidateSimpleMemberAccess(Expression<Func<TTableModel, object?>> expression)
    {
        // for the case : x=> x.Prop where Prop is Object
        if (expression.Body is MemberExpression memberExpression 
            && memberExpression.Expression is ParameterExpression)
        {
            return true;
        }

        // for the case : x=> x.Prop where Prop is not Object, where it will be explicitly converted to Object due to our expressions signature
        if (expression.Body is UnaryExpression unaryExpression 
            && unaryExpression.Operand is MemberExpression operandMemberExpression 
            && operandMemberExpression.Expression is ParameterExpression)
        {
            return true;
        }

        throw new ArgumentException("Only simple member access expressions are accepted: " + expression.Body);
    }

    #endregion

    #region Internal

    internal TableAuditConfiguration(SimpleAuditContext dbContext)
    {
        this._dbContext = dbContext;

        if (!this._dbContext.IsTableType<TTableModel>())
        {
            throw new InvalidOperationException($"The type {this._entityType.Name} is not recognized as a table Model.");
        }

        if (!this.GetColumnsProperties().Any(p => p.IsPrimaryKey()))
        {
            throw new InvalidOperationException($"The table of model type {this._entityType.Name} should have primary key to be auditable.");
        }

        if (this._entityType == this._dbContext.AuditTrailTableModelType)
        {
            throw new InvalidOperationException("The table that dedicated for audit trail, can't be audited.");
        }

    }

    #endregion

    #region Public

    public TableAuditConfiguration<TTableModel> StartAuditing()
    {
        this._dbContext.AuditingIsEnabled = true;

        return this;
    }

    public TableAuditConfiguration<TTableModel> AuditAllColumns(AutoExcludeColumnType autoExclusions = AutoExcludeColumnType.None)
    {
        var propertiesNames = this.GetColumnsProperties()
                            .Where(p => IsAutoAuditableProperty(p, autoExclusions))
                            .Select(p => p.Name)
                            .ToList();

        if (propertiesNames.Count == 0)
        {
            throw new InvalidOperationException($"The table model {this._entityType} has no auditable columns.");
        }

        this.SetAuditableProperties(propertiesNames);

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeTableFromAuditing()
    {
        var entitySettings = this._dbContext
            .EntitiesAuditSettings
            .SingleOrDefault(s => s.EntityType == this._entityType)
            ?? throw new InvalidOperationException($"The table model type {this._entityType.Name} is not configured for audit yet to be excluded.");

        this._dbContext.EntitiesAuditSettings.Remove(entitySettings);

        if (this._dbContext.EntitiesAuditSettings.Count == 0)
        {
            throw new InvalidOperationException("No tables left for auditing.");
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

        var propertiesNames = this.ValidateAndGetProperties(includedColumns)
                            .Select(p => p.Name)
                            .ToList();

        this.SetAuditableProperties(propertiesNames);

        return this;
    }

    /// <summary>
    /// Configure table's column for auditing, with column mapping call back.
    /// </summary>
    /// <param name="includedColumn">The column to audit</param>
    /// <param name="valueMapperCallBack">A call back to map values of this column.</param>
    /// <returns></returns>
    public TableAuditConfiguration<TTableModel>
       AuditColumn(
       Expression<Func<TTableModel, object?>> includedColumn,
       Func<object?, object?> valueMapperCallBack)
    {
        var propertiesNames = this.ValidateAndGetProperties([includedColumn])
                            .Select(p => p.Name)
                            .ToList();

        this.SetAuditableProperties(propertiesNames);

        // set the value mapper call back
        this._dbContext
            .EntitiesAuditSettings
            .Single(s => s.EntityType == this._entityType)
            .AuditableProperties[propertiesNames[0]] = valueMapperCallBack;

        return this;
    }

    public TableAuditConfiguration<TTableModel> ExcludeColumnsFromAuditing(
        Expression<Func<TTableModel, object?>> excludedColumn, 
        params Expression<Func<TTableModel, object?>>[] moreExcludedColumns)
    {
        var excludedColumns = moreExcludedColumns.ToList();
        excludedColumns.Add(excludedColumn);

        var propertiesNamesToExclude = this.ValidateAndGetProperties(excludedColumns)
                        .Select(p => p.Name);

        var entitySettings = this._dbContext
            .EntitiesAuditSettings
            .SingleOrDefault(s => s.EntityType == this._entityType)
            ?? throw new InvalidOperationException($"The table {this._entityType.Name} is not yet configured for audit.");

        foreach (var propertyName in propertiesNamesToExclude)
        {
            if (!entitySettings.AuditableProperties.Remove(propertyName))
            {
                throw new InvalidOperationException($"The property {propertyName} is not yet set to be audited.");
            }
        }

        if (entitySettings.AuditableProperties.Count == 0)
        {
            throw new InvalidOperationException($"No columns left to audit in {this._entityType.Name} table.");
        }

        return this;
    }

    public TableAuditConfiguration<TAnotherEntity> ConfigureTableAudit<TAnotherEntity>()
        where TAnotherEntity : class
        => new(this._dbContext);

    #endregion
}
