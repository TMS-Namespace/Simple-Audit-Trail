namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class ColumnAuditInfo
{
    internal ColumnAuditInfo(string columnSQLName, string propertyName, Type dataType) 
    {
        this.ColumnSQLName = columnSQLName;
        this.PropertyName = propertyName;
        this.DataType = dataType;
    }

    public string ColumnSQLName { get; init; }

    public string PropertyName { get; init; }

    public Type DataType { get; init; }

    public object? OldValue { get; set; }

    public object? NewValue { get; set; }
}
