
using TMS.SimpleAudit.InfoModels;

namespace TMS.SimpleAudit.Settings
{
    internal interface IEntityAuditSettingsInternal
    {
        object GetReferenceKeyValue(RowAuditInfo rowAuditInfo);

        void Validate();

        PropertyAuditSettings RequestPropertyAuditSettings(string propertyName);

        PropertyAuditSettings? GetPropertySettings(string propertyName);

        void RemovePropertyAuditSettings(string propertyName);

        IGroupAuditSettingsInternal GroupAuditSettings { get; }

        Type TableModelType { get; }

        bool IsSoftDeleted(object rowModel);
    }
}