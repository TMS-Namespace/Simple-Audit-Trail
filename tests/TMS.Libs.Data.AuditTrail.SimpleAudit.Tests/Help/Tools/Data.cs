using Microsoft.EntityFrameworkCore;

using FluentAssertions;
using Soenneker.Utils.AutoBogus;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools;

internal static class DataSetup
{
    public static (NotAuditableTableModel, AuditableTableModel, CustomAuditInfo) GenerateModels()
    {
        var notAuditableRow = new AutoFaker<NotAuditableTableModel>()
                .RuleFor(e => e.AuditableTableModels, f => [])
                .RuleFor(e => e.Id, f => 0)
            .Generate();

        var auditableRow = new AutoFaker<AuditableTableModel>()
                .RuleFor(e => e.Count, f => f.Random.Int(5, 10000))
                .RuleFor(e => e.Id, f => 0)
                .RuleFor(e => e.CreateAt, f => DateTime.UtcNow)
                .RuleFor(e => e.NotAuditableTableModel, f => notAuditableRow)
                .RuleFor(e => e.EnumColumn, f => (uint)f.PickRandom<EnumColumn>())
            .Generate();

        var customAuditInfo = new AutoFaker<CustomAuditInfo>()
                .RuleFor(e => e.IpAddress, f => f.Internet.IpAddress().ToString())
            .Generate();

        return (notAuditableRow, auditableRow, customAuditInfo);
    }

    public static async Task<(AuditableTableModel, CustomAuditInfo)> SeedAsync(AuditableContext dbContext)
    {
        var (notAuditableRow, auditableRow, customAuditInfo) = GenerateModels();

        await dbContext.AddAsync(notAuditableRow);
        await dbContext.AddAsync(auditableRow);

        var savedCount = await dbContext.SaveChangesAsync(customAuditInfo);

        savedCount.Should().Be(2);

        return (auditableRow, customAuditInfo);
    }


    public static int GetAuditableChangesCount(AuditableContext dbContext)
        => dbContext
                .ChangeTracker
                .Entries()
                .Count(e => e.State
                    is EntityState.Added
                    or EntityState.Deleted
                    or EntityState.Modified);
}
