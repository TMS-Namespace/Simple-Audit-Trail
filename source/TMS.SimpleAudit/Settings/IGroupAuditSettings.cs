using System.Collections.ObjectModel;

namespace TMS.SimpleAudit.Settings;

public interface IGroupAuditSettings
{
        ReadOnlyCollection<IEntityAuditSettings> EntityAuditSettings { get; }

        Type AuditTrailTableModelType { get; }

}
