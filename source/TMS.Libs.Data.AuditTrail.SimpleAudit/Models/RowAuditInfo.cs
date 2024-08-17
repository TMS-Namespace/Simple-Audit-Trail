using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.ObjectModel;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public sealed class RowAuditInfo
{
    internal RowAuditInfo(
        string tableName,
        AuditAction action,
        Type modelType,
        EntityEntry trackingEntityEntry)
    {
        this.ModelType = modelType;
        this.TableSQLName = tableName;
        this.Action = action;
        this.TrackingEntityEntry = trackingEntityEntry;
    }

    internal EntityEntry TrackingEntityEntry { get; init; }

    public Type ModelType { get; init; }

    public string TableSQLName { get; init; }

    public AuditAction Action { get; init; }

    public object PrimaryKeyValue { get; internal set; } = default!;

    internal List<ColumnAuditInfo> InternalColumnsChanges { get; set; } = [];

    public ReadOnlyCollection<ColumnAuditInfo> ColumnsChanges => this.InternalColumnsChanges.AsReadOnly();
}
