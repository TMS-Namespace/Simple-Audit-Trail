using Microsoft.EntityFrameworkCore;

namespace TMS.SimpleAudit.Settings;

public sealed class PropertyAuditSettings
{
    internal PropertyAuditSettings(
        string propertyName,
        string columnSQLName,
        Type dataType,
        string dataSQLType)
    {
        PropertyName = propertyName;
        ColumnSQLName = columnSQLName;
        DataType = dataType;
        DataSQLTypeName = dataSQLType;
    }

    public string PropertyName { get; private init; }

    public Func<object?, object?>? ValueMapper { get; internal set; }

    public Func<DbContext, object, object?, CancellationToken, Task<object?>>? ValueMapperAsync { get; internal set; }

    internal async Task<object?> MapValueAsync(
        DbContext dbContext,
        object rowModel,
        object? value,
        CancellationToken cancellationToken)
    {
        if (ValueMapper is not null)
        {
            return ValueMapper(value);
        }

        if (ValueMapperAsync is not null)
        {
            return await ValueMapperAsync(dbContext, rowModel, value, cancellationToken);
        }

        return value;

    }

    public string? ColumnNameAlias { get; internal set; }

    public string ColumnSQLName { get; private init; }

    public Type DataType { get; private init; }

    public string DataSQLTypeName { get; private init; }
}
