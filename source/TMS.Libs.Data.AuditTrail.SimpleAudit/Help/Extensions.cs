using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

using System.Linq.Expressions;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Help;

internal static class Extensions
{
    #region Internal

    internal static IEntityType GetEntityType(
        this SimpleAuditContext dbContext,
        string tableModelTypeName)
        =>
            // Note that EF built in FindEntityType requires full CLR name of the entity type
            dbContext
                .Model
                .GetEntityTypes()
                .SingleOrDefault(et => et.ClrType.Name == tableModelTypeName)
                ?? throw new InvalidOperationException($"No table found of '{tableModelTypeName}' model type name.");

    internal static IEntityType GetEntityType<TTableModel>(this SimpleAuditContext dbContext)
        => dbContext.GetEntityType(typeof(TTableModel).Name);

    internal static string GetSQLColumnName(
        this SimpleAuditContext _,
        EntityEntry entryEntity,
        PropertyEntry propertyEntry)
    {
        var entityType = entryEntity.Metadata;
        var property = entityType.FindProperty(propertyEntry.Metadata.Name)
            ?? throw new InvalidOperationException($"The column {propertyEntry.Metadata.Name} is not found in {entryEntity.Metadata.Name} table.");

        var columnName = property
            .GetColumnName(
                StoreObjectIdentifier
                .Table(entityType.GetTableName()!, entityType.GetSchema()));

        return columnName!;
    }

    internal static IProperty GetProperty(IEntityType entryEntity, string propertyName)
        => entryEntity.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"The column {propertyName} is not found in {entryEntity} table.");

    internal static string GetSQLColumnName(
        this SimpleAuditContext _,
        IEntityType entryEntity,
        string propertyName)
    => GetProperty(entryEntity, propertyName).GetColumnName();

    internal static string GetSQLTableName(this SimpleAuditContext _, EntityEntry entityEntry)
        => entityEntry.Metadata.GetTableName()!;

    internal static Type GetTableModelType(this SimpleAuditContext _, EntityEntry entityEntry)
        => entityEntry.Metadata.ClrType;

    public static Type GetColumnType(
        this SimpleAuditContext dbContext,
        Type tableModelType,
        string propertyName)
    {
        var entityType = dbContext.GetEntityType(tableModelType.Name);
        return GetProperty(entityType, propertyName)
            .ClrType;
    }

    public static string GetColumnSQLType(
    this SimpleAuditContext dbContext,
    Type tableModelType,
    string propertyName)
    {
        var entityType = dbContext.GetEntityType(tableModelType.Name);

        return GetProperty(entityType, propertyName)
            .GetRelationalTypeMapping()
            .StoreType;
    }

    #endregion

    #region Public

    public static string GetSQLTableName(this SimpleAuditContext dbContext, string tableModelTypeName)
        => dbContext.GetEntityType(tableModelTypeName).GetTableName()!;

    public static string GetSQLTableName(this SimpleAuditContext dbContext, Type tableModelType)
        => dbContext.GetEntityType(tableModelType.Name).GetTableName()!;

    public static string GetSQLTableName<TTableModel>(this SimpleAuditContext dbContext)
        where TTableModel : class
        => dbContext.GetSQLTableName(typeof(TTableModel).Name);

    public static string GetSQLColumnName<TTableModel>(
        this SimpleAuditContext dbContext,
        Expression<Func<TTableModel, object?>> propertyExpression)
        where TTableModel : class
    {
        var member = propertyExpression.Body as MemberExpression
                    ?? (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;

        if (member == null)
        {
            throw new InvalidOperationException("Expression is not a valid member expression.");
        }

        return dbContext.GetSQLColumnName<TTableModel>(member.Member.Name);
    }

    public static string GetSQLColumnName<TTableModel>(
        this SimpleAuditContext dbContext,
        string propertyName)
        where TTableModel : class
        => dbContext.GetSQLColumnName(dbContext.GetEntityType<TTableModel>(), propertyName);

    public static string GetSQLColumnName(
        this SimpleAuditContext dbContext,
        string tableModelTypeName,
        string propertyName)
        => dbContext.GetSQLColumnName(dbContext.GetEntityType(tableModelTypeName), propertyName);

    public static string GetSQLColumnName(
        this SimpleAuditContext dbContext,
        Type tableModelType,
        string propertyName)
        => dbContext.GetSQLColumnName(dbContext.GetEntityType(tableModelType.Name), propertyName);

    public static Type GetModelType(this SimpleAuditContext dbContext, string tableSQLName)
    {
        var model = dbContext.Model;
        var entityTypes = model.GetEntityTypes();

        var entityType = entityTypes
            .SingleOrDefault(et => dbContext.GetSQLTableName(et.ClrType.Name) == tableSQLName)
            ?? throw new InvalidOperationException($"The table '{tableSQLName}' has no associated model.");

        return entityType.ClrType;
    }

    public static bool IsTableType(this SimpleAuditContext dbContext, Type tableModelType)
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

    public static bool IsTableType<TTableModel>(this SimpleAuditContext dbContext)
        => dbContext.IsTableType(typeof(TTableModel));

    #endregion
}
