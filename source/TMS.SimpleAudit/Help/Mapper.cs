using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Help;

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
        IEntityAuditSettingsInternal entitySettings,
        AuditAction action)
    => new(
            action,
            entityEntry,
            entitySettings);

    public static ColumnAuditInfo ToModel(
        PropertyEntry propertyEntry,
        PropertyAuditSettings propertySettings,
        RowAuditInfo rowAuditInfo)
    => new(
            propertyEntry,
            rowAuditInfo,
            propertySettings);
}