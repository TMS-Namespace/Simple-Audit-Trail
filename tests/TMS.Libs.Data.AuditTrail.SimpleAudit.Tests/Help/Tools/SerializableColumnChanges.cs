namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools
{
    // we need this, because `ColumnAuditInfo` contains some non-serializeble properties
    public record SerializableColumnChanges
    {
        public required string ColumnSQLName { get; init; }

        public required string PropertyName { get; init; }

        public required string DataTypeName { get; init; }

        public required string DataSQLTypeName { get; init; }

        public required object? OldValue { get; set; }

        public required object? NewValue { get; set; }
    }
}
