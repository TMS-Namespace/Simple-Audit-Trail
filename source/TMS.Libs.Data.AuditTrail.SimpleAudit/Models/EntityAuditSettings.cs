using System.Dynamic;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

internal record EntityAuditSettings
{
    public required Type EntityType { get; init;}

    public Dictionary<string, Func<object?, object?>?> AuditableProperties = [];
}
