using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

public class RowAuditInfo(string tableName, AuditAction action, Type tableModelType)
{
    internal EntityEntry? TrackingEntityEntry { get; set; }

    public Type TableModelType => tableModelType;

    public string TableSQLName => tableName;

    public AuditAction Action => action;

    public object PrimaryKeyValue { get; internal set; } = default!;

    public List<ColumnAuditInfo> ColumnsChanges { get; } = [];
}
