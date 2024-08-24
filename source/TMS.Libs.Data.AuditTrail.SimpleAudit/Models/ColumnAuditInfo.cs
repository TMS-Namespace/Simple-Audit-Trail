namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class ColumnAuditInfo
{
    internal ColumnAuditInfo(
        string columnSQLName,
        string propertyName,
        Type dataType,
        string dataSQLType,
        string? columnAlias)
    {
        this.ColumnSQLName = columnSQLName;
        this.PropertyName = propertyName;
        this.DataType = dataType;
        this.DataSQLTypeName = dataSQLType;
        this.ColumnNameAlias = columnAlias;
    }

    public string ColumnSQLName { get; private init; }

    public string PropertyName { get; private init; }

    public string? ColumnNameAlias { get; private init; }

    public Type DataType { get; private init; }

    public string DataSQLTypeName { get; private init; }

    public object? OldValue { get; internal set; }

    public object? NewValue { get; internal set; }
}
