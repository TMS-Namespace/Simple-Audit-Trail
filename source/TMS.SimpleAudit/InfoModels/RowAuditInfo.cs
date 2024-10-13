using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using System.Collections.ObjectModel;

using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.InfoModels;

public sealed class RowAuditInfo
{
    internal RowAuditInfo(
        AuditAction action,
        EntityEntry trackingEntityEntry,
        IEntityAuditSettingsInternal entityAuditSettings)
    {
        Action = action;
        TrackingEntityEntry = trackingEntityEntry;
        EntityAuditSettings = entityAuditSettings;
    }

    internal EntityEntry TrackingEntityEntry { get; private init; }

    internal IEntityAuditSettingsInternal EntityAuditSettings { get; private init; }

    public object RowModel => TrackingEntityEntry.Entity;

    public string TableSQLName => ((IEntityAuditSettings)EntityAuditSettings).TableSQLName;

    public string? TableNameAlias => ((IEntityAuditSettings)EntityAuditSettings).TableNameAlias;

    public DbContext DataBaseContext => TrackingEntityEntry.Context;

    public AuditAction Action { get; private init; }

    public object ReferenceKeyValue { get; internal set; } = default!;

    internal List<ColumnAuditInfo> ColumnsAuditInfos { get; } = [];

    public ReadOnlyCollection<ColumnAuditInfo> ColumnsChanges
        => ColumnsAuditInfos.AsReadOnly();
}
