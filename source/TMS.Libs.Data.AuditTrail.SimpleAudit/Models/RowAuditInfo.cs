using Microsoft.EntityFrameworkCore.ChangeTracking;

using System.Collections.ObjectModel;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class RowAuditInfo
{
    internal RowAuditInfo(
        AuditAction action,
        EntityEntry trackingEntityEntry,
        EntityAuditSettings entityAuditSettings)
    {
        Action = action;
        TrackingEntityEntry = trackingEntityEntry;
        AuditSettings = entityAuditSettings;
    }

    internal EntityEntry TrackingEntityEntry { get; private init; }

    internal EntityAuditSettings AuditSettings { get; private init; }

    public object RowModel => TrackingEntityEntry.Entity;

    public string TableSQLName => AuditSettings.TableSQLName;

    public string? TableNameAlias => AuditSettings.TableAlias;

    public AuditAction Action { get; private init; }

    public object PrimaryKeyValue { get; internal set; } = default!;

    internal List<ColumnAuditInfo> ColumnsAuditInfos { get; } = [];

    public ReadOnlyCollection<ColumnAuditInfo> ColumnsChanges
        => ColumnsAuditInfos.AsReadOnly();
}
