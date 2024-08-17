namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help
{
    public record SerializableColumnChanges
    {
        public required string ColumnSQLName { get; init; }

        public required string PropertyName { get; init; }

        public required string DataTypeName { get; init; }

        public object? OldValue { get; set; }

        public object? NewValue { get; set; }
    }
}
