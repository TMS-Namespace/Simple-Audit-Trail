namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Settings
{
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
            DataSQLType = dataSQLType;
        }

        public string PropertyName { get; private init; }

        public Func<object?, object?>? ValueMapper { get; internal set; }

        public string? ColumnNameAlias { get; internal set; }

        public string ColumnSQLName { get; private init; }

        public Type DataType { get; private init; }

        public string DataSQLType { get; private init; }
    }
}
