using TMS.SimpleAudit.Auditing;

namespace TMS.SimpleAudit.Configurations;

public sealed class AuditConfigurationException : AuditingException
{
    internal AuditConfigurationException(string message)
        : base(message)
    { }
}
