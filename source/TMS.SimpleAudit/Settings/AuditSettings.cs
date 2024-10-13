using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Configurations;

namespace TMS.SimpleAudit.Settings;

public sealed class AuditSettings
{
    #region Private Vars

    private readonly Dictionary<Type, IGroupAuditSettingsInternal> _groupsSettings = [];
    
    private readonly SimpleAuditManager _manager;

    #endregion

    #region Internal

    internal AuditSettings(SimpleAuditManager manager)
    => _manager = manager;

    internal void Validate()
    {
        if (_groupsSettings.Count < 1)
        {
            throw new AuditConfigurationException($"No groups are configured for audit trail.");
        }

        _groupsSettings
            .Values
            .ToList()
            .ForEach(gs => gs.Validate());

    }

    internal IGroupAuditSettingsInternal? GetGroupSettingsByTableModelType(Type tableModelType)
    =>
        _groupsSettings
            .Where(g => g.Value.GetEntityAuditSettings(tableModelType) is not null)
            .Select(g => g.Value)
            .SingleOrDefault();

    internal IEntityAuditSettingsInternal? GetEntityAuditSettings(Type tableModelType)
    =>
        _groupsSettings
            .Select(g => g.Value.GetEntityAuditSettings(tableModelType))
            .SingleOrDefault(eas => eas is not null);

    internal GroupAuditSettings<TGroupAuditModel> RequestGroupSettings<TGroupAuditModel>()
    where TGroupAuditModel : class
    {
        if (!_groupsSettings.TryGetValue(typeof(TGroupAuditModel), out var group))

        {
            group = new GroupAuditSettings<TGroupAuditModel>(_manager);
            _groupsSettings.Add(typeof(TGroupAuditModel), group);
        }

        return (GroupAuditSettings<TGroupAuditModel>)group;
    }

    internal IGroupAuditSettingsInternal? GetGroupSettings(Type groupAuditModelType)
    {
        if (!_groupsSettings.TryGetValue(groupAuditModelType, out var group))
        {
            return null;
        }

        return group;
    }

    #endregion

    #region Public

    public IReadOnlyCollection<IGroupAuditSettings> GroupSettings
        => _groupsSettings.Values.Cast<IGroupAuditSettings>().ToList();

    #endregion

}