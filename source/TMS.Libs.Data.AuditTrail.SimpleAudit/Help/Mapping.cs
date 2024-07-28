using Microsoft.EntityFrameworkCore;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Help;

internal static class Mapping
{
    public static AuditAction ToModel(EntityState state)
    => state switch
    {
        EntityState.Added => AuditAction.Add,
        EntityState.Modified => AuditAction.Update,
        EntityState.Deleted => AuditAction.Delete,
        EntityState.Detached => throw new NotImplementedException(),
        EntityState.Unchanged => throw new NotImplementedException(),
        _ => throw new NotImplementedException(),
    };
}