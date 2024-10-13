using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

using System.Linq.Expressions;
using TMS.SimpleAudit.Configurations;

namespace TMS.SimpleAudit.Help;

internal static class Extensions
{
    #region Internal

    internal static IEntityType GetEntityType(
        this DbContext dbContext,
        string tableModelTypeName)
        =>
            // Note that EF built in FindEntityType requires full CLR name of the entity type
            dbContext
                .Model
                .GetEntityTypes()
                .SingleOrDefault(et => et.ClrType.Name == tableModelTypeName)
                ?? throw new AuditConfigurationException($"No table found of '{tableModelTypeName}' model type name.");

    internal static IEntityType GetEntityType<TTableModel>(this DbContext dbContext)
        => dbContext.GetEntityType(typeof(TTableModel).Name);

    internal static string GetSQLColumnName(
        this DbContext _,
        EntityEntry entryEntity,
        PropertyEntry propertyEntry)
    {
        var entityType = entryEntity.Metadata;
        var property = entityType.FindProperty(propertyEntry.Metadata.Name)
            ?? throw new AuditConfigurationException($"The column {propertyEntry.Metadata.Name} is not found in {entryEntity.Metadata.Name} table.");

        var columnName = property
            .GetColumnName(
                StoreObjectIdentifier
                .Table(entityType.GetTableName()!, entityType.GetSchema()));

        return columnName!;
    }

    internal static IProperty GetProperty(IEntityType entryEntity, string propertyName)
        => entryEntity.FindProperty(propertyName)
            ?? throw new AuditConfigurationException($"The column {propertyName} is not found in {entryEntity} table.");

    internal static string GetSQLColumnName(
        this DbContext _,
        IEntityType entryEntity,
        string propertyName)
    => GetProperty(entryEntity, propertyName).GetColumnName();

    internal static string GetSQLTableName(this DbContext _, EntityEntry entityEntry)
        => entityEntry.Metadata.GetTableName()!;

    internal static Type GetTableModelType(this DbContext _, EntityEntry entityEntry)
        => entityEntry.Metadata.ClrType;

    #endregion

    #region Public

    public static Type GetColumnType(
    this DbContext dbContext,
    Type tableModelType,
    string propertyName)
    {
        var entityType = dbContext.GetEntityType(tableModelType.Name);
        return GetProperty(entityType, propertyName)
            .ClrType;
    }

    public static string GetColumnSQLType(
    this DbContext dbContext,
    Type tableModelType,
    string propertyName)
    {
        var entityType = dbContext.GetEntityType(tableModelType.Name);

        return GetProperty(entityType, propertyName)
            .GetRelationalTypeMapping()
            .StoreType;
    }

    public static string GetSQLTableName(this DbContext dbContext, string tableModelTypeName)
        => dbContext.GetEntityType(tableModelTypeName).GetTableName()!;

    public static string GetSQLTableName(this DbContext dbContext, Type tableModelType)
        => dbContext.GetEntityType(tableModelType.Name).GetTableName()!;

    public static string GetSQLTableName<TTableModel>(this DbContext dbContext)
        where TTableModel : class
        => dbContext.GetSQLTableName(typeof(TTableModel).Name);

    public static string GetSQLColumnName<TTableModel>(
        this DbContext dbContext,
        Expression<Func<TTableModel, object?>> propertyExpression)
        where TTableModel : class
    {
        var member = propertyExpression.Body as MemberExpression
                    ?? (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;

        if (member == null)
        {
            throw new AuditConfigurationException("Expression is not a valid member expression.");
        }

        return dbContext.GetSQLColumnName<TTableModel>(member.Member.Name);
    }

    public static string GetSQLColumnName<TTableModel>(
        this DbContext dbContext,
        string propertyName)
        where TTableModel : class
        => dbContext.GetSQLColumnName(dbContext.GetEntityType<TTableModel>(), propertyName);

    public static string GetSQLColumnName(
        this DbContext dbContext,
        string tableModelTypeName,
        string propertyName)
        => dbContext.GetSQLColumnName(dbContext.GetEntityType(tableModelTypeName), propertyName);

    public static string GetSQLColumnName(
        this DbContext dbContext,
        Type tableModelType,
        string propertyName)
        => dbContext.GetSQLColumnName(dbContext.GetEntityType(tableModelType.Name), propertyName);

    public static Type GetModelType(this DbContext dbContext, string tableSQLName)
    {
        var model = dbContext.Model;
        var entityTypes = model.GetEntityTypes();

        var entityType = entityTypes
            .SingleOrDefault(et => dbContext.GetSQLTableName(et.ClrType.Name) == tableSQLName)
            ?? throw new AuditConfigurationException($"The table '{tableSQLName}' has no associated model.");

        return entityType.ClrType;
    }

    public static bool IsTableType(this DbContext dbContext, Type tableModelType)
    {
        var entityTypeMetadata = dbContext.Model.FindEntityType(tableModelType);

        if (entityTypeMetadata == null)
        {
            return false;
        }

        // Check if the entity type has a corresponding table name
        var tableName = entityTypeMetadata.GetTableName();
        return !string.IsNullOrEmpty(tableName);
    }

    public static bool IsTableType<TTableModel>(this DbContext dbContext)
        => dbContext.IsTableType(typeof(TTableModel));

    #endregion
}
