using Microsoft.EntityFrameworkCore.ChangeTracking;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class ColumnAuditInfo
{
    internal ColumnAuditInfo(
        /*string columnSQLName,
        string propertyName,
        Type dataType,
        string dataSQLType,
        string? columnAlias,*/
        PropertyEntry trackingPropertyEntry,
        RowAuditInfo rowAuditInfo,
        PropertyAuditSettings propertyAuditSettings)
    {
        /*this.ColumnSQLName = columnSQLName;
        this.PropertyName = propertyName;
        this.DataType = dataType;
        this.DataSQLTypeName = dataSQLType;
        this.ColumnNameAlias = columnAlias;*/
        this.TrackingPropertyEntry = trackingPropertyEntry;
        this.RowAuditInfo = rowAuditInfo;
        this.AuditSettings = propertyAuditSettings;
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
