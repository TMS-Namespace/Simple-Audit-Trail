using System.Runtime.InteropServices;
using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.Help;
using TMS.SimpleAudit.InfoModels;

namespace TMS.SimpleAudit.Settings;

public sealed class EntityAuditSettings<TTableModel> : IEntityAuditSettingsInternal, IEntityAuditSettings 
    where TTableModel : class
{
    #region Private Vars

    private readonly SimpleAuditManager _manager;

    private HashSet<PropertyAuditSettings> _auditableProperties = [];

    private readonly IGroupAuditSettingsInternal _groupAuditSettings;

    #endregion

    #region Public Properties

    public string TableSQLName { get; private init; }

    public Type TableModelType { get; private init; }

    public string? TableNameAlias { get; internal set; }

    public string ReferenceColumnName { get; internal set; } = string.Empty;

    public Func<TTableModel, bool>? IsSoftDeletedCallBack { get; internal set;} = null;

    #endregion

    #region Internal Constructors

    internal EntityAuditSettings(
    SimpleAuditManager manager,
    Type tableModelType,
    IGroupAuditSettingsInternal groupAuditSettings,
    string tableSQLName)
    {
        TableModelType = tableModelType;
        TableSQLName = tableSQLName;
        _groupAuditSettings = groupAuditSettings;
        _manager = manager;
    }

    #endregion

    #region Internal Functions

    bool IEntityAuditSettingsInternal.IsSoftDeleted(object rowModel)
    {
        if (IsSoftDeletedCallBack is not null)
        {
            return IsSoftDeletedCallBack((TTableModel)rowModel);
        }

        return false;
    }

    object IEntityAuditSettingsInternal.GetReferenceKeyValue(RowAuditInfo rowAuditInfo)
    => rowAuditInfo
        .ColumnsChanges[0]
        .TrackingPropertyEntry
        .EntityEntry
        .Properties
        .Single(p => p.Metadata.Name == ReferenceColumnName)
        .CurrentValue!;

    void IEntityAuditSettingsInternal.Validate()
    {
        if (_auditableProperties.Count < 1)
        {
            throw new AuditConfigurationException($"The table model '{TableModelType.Name}' has no configured column audit settings.");
        }
    }

    PropertyAuditSettings IEntityAuditSettingsInternal.RequestPropertyAuditSettings(string propertyName)
    {
        var propertySettings = ((IEntityAuditSettingsInternal)this).GetPropertySettings(propertyName);

        if (propertySettings == null)
        {
            propertySettings = new(
                propertyName,
                _manager.DataBaseContext.GetSQLColumnName(TableModelType, propertyName),
                _manager.DataBaseContext.GetColumnType(TableModelType, propertyName),
                _manager.DataBaseContext.GetColumnSQLType(TableModelType, propertyName));

            _auditableProperties.Add(propertySettings);
        }

        return propertySettings;
    }

    PropertyAuditSettings? IEntityAuditSettingsInternal.GetPropertySettings(string propertyName)
    => _auditableProperties
            .SingleOrDefault(ap => ap.PropertyName == propertyName);

    void IEntityAuditSettingsInternal.RemovePropertyAuditSettings(string propertyName)
    {
        var propertySettings = ((IEntityAuditSettingsInternal)this).GetPropertySettings(propertyName)
            ?? throw new AuditConfigurationException($"The property {propertyName} is not yet set to be audited on {TableModelType} table model.");

        _auditableProperties.Remove(propertySettings);

        if (_auditableProperties.Count == 0)
        {
            throw new AuditConfigurationException($"No columns left to audit in {TableModelType} table model.");
        }
    }

    IGroupAuditSettingsInternal IEntityAuditSettingsInternal.GroupAuditSettings => _groupAuditSettings;

    #endregion Internal

    #region Public Properties

    public IReadOnlyCollection<PropertyAuditSettings> AuditablePropertiesSettings
        => _auditableProperties.ToList().AsReadOnly();

    #endregion
}