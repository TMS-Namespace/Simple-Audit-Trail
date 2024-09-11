using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class ColumnAuditInfo
{
    internal ColumnAuditInfo(
        PropertyEntry trackingPropertyEntry,
        RowAuditInfo rowAuditInfo,
        PropertyAuditSettings propertyAuditSettings)
    {
        TrackingPropertyEntry = trackingPropertyEntry;
        RowAuditInfo = rowAuditInfo;
        AuditSettings = propertyAuditSettings;
    }

    internal PropertyEntry TrackingPropertyEntry { get; private init; }

    internal RowAuditInfo RowAuditInfo { get; private init; }

    internal PropertyAuditSettings AuditSettings { get; private init; }

    public string ColumnSQLName => AuditSettings.ColumnSQLName;

    public string PropertyName => AuditSettings.PropertyName;

    public string? ColumnNameAlias => AuditSettings.ColumnNameAlias;

    public Type DataType => AuditSettings.DataType;

    public string DataSQLTypeName => AuditSettings.DataSQLTypeName;

    public object? OldValue { get; internal set; }

    public object? NewValue { get; internal set; }
}
