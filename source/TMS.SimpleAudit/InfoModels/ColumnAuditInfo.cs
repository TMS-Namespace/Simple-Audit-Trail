using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.InfoModels;

public sealed class ColumnAuditInfo
{
    internal ColumnAuditInfo(
        PropertyEntry trackingPropertyEntry,
        RowAuditInfo rowAuditInfo,
        PropertyAuditSettings propertyAuditSettings)
    {
        TrackingPropertyEntry = trackingPropertyEntry;
        RowAuditInfo = rowAuditInfo;
        PropertyAuditSettings = propertyAuditSettings;
    }

    internal PropertyEntry TrackingPropertyEntry { get; private init; }

    internal RowAuditInfo RowAuditInfo { get; private init; }

    internal PropertyAuditSettings PropertyAuditSettings { get; private init; }

    public string ColumnSQLName => PropertyAuditSettings.ColumnSQLName;

    public string PropertyName => PropertyAuditSettings.PropertyName;

    public string? ColumnNameAlias => PropertyAuditSettings.ColumnNameAlias;

    public Type DataType => PropertyAuditSettings.DataType;

    public string DataSQLTypeName => PropertyAuditSettings.DataSQLTypeName;

    public object? OldValue { get; internal set; }

    public object? NewValue { get; internal set; }
}
