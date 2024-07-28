namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public record ColumnAuditInfo 
{
    public required string ColumnSQLName { get; set; }

    public object? OldValue { get; set; }

    public object? NewValue { get; set; }
}
