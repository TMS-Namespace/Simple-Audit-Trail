using Bogus;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace DataRepository.Tests.Integration;

public class AuditingTests
{
    private readonly AuditConfig _auditConfig = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_Add_Audit_Trail_On_Insert(bool explicitInclude)
    {
        // arrange
        CustomAuditInfo? customAuditInfo;
        AuditableTableModel? auditableRow;


        var contextFactory = new ContextFactory();

        using (var dbContext = contextFactory.Create())
        {
            _auditConfig.Config(dbContext, explicitInclude);

        // act
            (auditableRow, customAuditInfo) = await Tools.SeedAsync(dbContext);
        }

        // assert

        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Tools.AssertTrailsAndGetColumnChangesAsync(
                                    dbContext,
                                    auditableRow.Id,
                                    AuditAction.Add,
                                    customAuditInfo,
                                    2 // CompanyName & Count
                                    );

            Tools.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                columnChanges[0],
                null,
                auditableRow.CompanyName);

            Tools.AssertColumnValues(
                dbContext,
                t => t.Count,
                columnChanges[1],
                null,
                auditableRow.Count.ToString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_Add_Audit_Trail_On_Update(bool explicitInclude)
    {
        // arrange
        CustomAuditInfo? customAuditInfo;
        AuditableTableModel? auditableRow;

        var oldCompanyName = string.Empty;
        var oldCount = 0;

        var contextFactory = new ContextFactory();

        using (var dbContext = contextFactory.Create())
        {
            (auditableRow, customAuditInfo) = await Tools.SeedAsync(dbContext);

            // act
            _auditConfig.Config(dbContext, explicitInclude);

            oldCompanyName = auditableRow.CompanyName;
            oldCount = auditableRow.Count;

            auditableRow.CompanyName = new Faker().Random.String2(15);
            auditableRow.Count = new Faker().Random.Int(500, 10_000);
            auditableRow.CreateAt = new Faker().Date.Past(); // not audited

            await dbContext.SaveChangesAsync(customAuditInfo);
        }

        // assert

        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Tools.AssertTrailsAndGetColumnChangesAsync(
                                    dbContext,
                                    auditableRow.Id,
                                    AuditAction.Update,
                                    customAuditInfo,
                                    2 // CompanyName & Count
                                    );

            Tools.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                columnChanges[0],
                oldCompanyName,
                auditableRow.CompanyName);

            Tools.AssertColumnValues(
                dbContext,
                t => t.Count,
                columnChanges[1],
                oldCount.ToString(),
                auditableRow.Count.ToString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_Add_Audit_Trail_On_Delete(bool explicitInclude)
    {
        // arrange
        CustomAuditInfo? customAuditInfo;
        AuditableTableModel? auditableRow;

        var contextFactory = new ContextFactory();

        using (var dbContext = contextFactory.Create())
        {
            (auditableRow, customAuditInfo) = await Tools.SeedAsync(dbContext);

            // act
            _auditConfig.Config(dbContext, explicitInclude);

            dbContext.Remove(auditableRow);

            await dbContext.SaveChangesAsync(customAuditInfo);
        }

        // assert

        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Tools.AssertTrailsAndGetColumnChangesAsync(
                                    dbContext,
                                    auditableRow.Id,
                                    AuditAction.Delete,
                                    customAuditInfo,
                                    2 // CompanyName & Count
                                    );

            Tools.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                columnChanges[0],
                auditableRow.CompanyName,
                null);

            Tools.AssertColumnValues(
                dbContext,
                t => t.Count,
                columnChanges[1],
                auditableRow.Count.ToString(),
                null);
        }
    }
}
