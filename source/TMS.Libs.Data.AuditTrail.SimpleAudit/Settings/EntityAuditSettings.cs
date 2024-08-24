namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

public sealed class EntityAuditSettings
{
    internal EntityAuditSettings(Type modelType, string tableSQLName)
    {
        ModelType = modelType;
        TableSQLName = tableSQLName;
    }

    public string TableSQLName { get; private init; }

    public Type ModelType { get; private init; }

    public string? TableAlias { get; internal set; }

    internal List<PropertyAuditSettings> AuditableProperties = [];

    public IReadOnlyCollection<PropertyAuditSettings> AuditablePropertiesSettings
        => AuditableProperties.AsReadOnly();
}
