using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;
using System.Globalization;

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
                auditableRow.Count.ToString(CultureInfo.InvariantCulture));
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
                oldCount.ToString(CultureInfo.InvariantCulture),
                auditableRow.Count.ToString(CultureInfo.InvariantCulture));
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
                auditableRow.Count.ToString(CultureInfo.InvariantCulture),
                null);
        }
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_RollBack_Audit_Trails_On_Error(bool explicitInclude)
    {
        // arrange
        CustomAuditInfo? customAuditInfo;
        AuditableTableModel? auditableRow;

        var contextFactory = new ContextFactory();

        var initialAuditTrailsCount = 0;
        var initialContextChangesCount = 0;

        using (var dbContext = contextFactory.Create())
        {
            (auditableRow, customAuditInfo) = await Tools.SeedAsync(dbContext);

            // act
            _auditConfig.Config(dbContext, explicitInclude);

            // changes count and audit trails count should not change
            initialContextChangesCount = dbContext
                        .ChangeTracker
                        .Entries()
                        .Count(e => e.State 
                            is EntityState.Added 
                            or EntityState.Deleted  
                            or EntityState.Modified);

            initialAuditTrailsCount = await dbContext.AuditTrailTable.CountAsync();

            // we will trigger primary key violation error
            auditableRow.NotAuditableTableModelId = 1000;
            auditableRow.CompanyName = "Test";

            var action = async () => await dbContext.SaveChangesAsync();

            // assert

            await action.Should().ThrowAsync<DbUpdateException>();

            // added audit trail records should not be tracked anymore
            dbContext
                .ChangeTracker
                .Entries()
                .Count(e => e.State
                    is EntityState.Added
                    or EntityState.Deleted
                    or EntityState.Modified)
                .Should().Be(initialContextChangesCount + 1); // the row that we changed
        }

        using (var dbContext = contextFactory.Create())
        {
            var newAuditTrailsCount = await dbContext.AuditTrailTable.CountAsync();

            // no audit trail records should be added
            newAuditTrailsCount.Should().Be(initialAuditTrailsCount);
        }
    }
}
