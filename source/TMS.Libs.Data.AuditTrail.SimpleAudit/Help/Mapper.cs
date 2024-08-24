using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Settings;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Help;

internal static class Mapper
{
    public static AuditAction ToModel(EntityState state)
    => state switch
    {
        EntityState.Added => AuditAction.Added,
        EntityState.Modified => AuditAction.Modified,
        EntityState.Deleted => AuditAction.Deleted,
        EntityState.Detached => throw new NotImplementedException(),
        EntityState.Unchanged => throw new NotImplementedException(),
        _ => throw new NotImplementedException(),
    };

    public static RowAuditInfo ToModel(
        EntityEntry entityEntry,
        EntityAuditSettings entitySettings)
    => new (
            entitySettings.TableSQLName,
            Mapper.ToModel(entityEntry.State),
            entitySettings.ModelType,
            entityEntry,
            entitySettings.TableAlias);

    public static ColumnAuditInfo ToModel(
        PropertyEntry propertyEntry,
        PropertyAuditSettings propertySettings)
    => new (
            propertySettings.ColumnSQLName,
            propertyEntry.Metadata.Name,
            propertyEntry.Metadata.ClrType,
            propertySettings.DataSQLType,
            propertySettings.ColumnNameAlias);
}