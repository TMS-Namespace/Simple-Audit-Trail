using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.ObjectModel;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class RowAuditInfo
{
    internal RowAuditInfo(
        string tableSQLName,
        AuditAction action,
        Type modelType,
        EntityEntry trackingEntityEntry,
        string? alias)
    {
        this.ModelType = modelType;
        this.TableSQLName = tableSQLName;
        this.Action = action;
        this.TrackingEntityEntry = trackingEntityEntry;
        this.TableNameAlias = alias;
    }

    internal EntityEntry TrackingEntityEntry { get; private init; }

    public Type ModelType { get; private init; }

    public string TableSQLName { get; private init; }

    public string? TableNameAlias { get; private init; }

    public AuditAction Action { get; private init; }

    public object PrimaryKeyValue { get; internal set; } = default!;

    internal List<ColumnAuditInfo> ColumnsAuditInfos { get; } = [];

    public ReadOnlyCollection<ColumnAuditInfo> ColumnsChanges
        => this.ColumnsAuditInfos.AsReadOnly();
}
