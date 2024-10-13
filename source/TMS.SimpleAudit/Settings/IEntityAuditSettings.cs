
namespace TMS.SimpleAudit.Settings
{
    public interface IEntityAuditSettings
    {
        IReadOnlyCollection<PropertyAuditSettings> AuditablePropertiesSettings { get; }

        string ReferenceColumnName { get; }

        Type TableModelType { get; }

        string? TableNameAlias { get; }

        string TableSQLName { get; }
    }
}