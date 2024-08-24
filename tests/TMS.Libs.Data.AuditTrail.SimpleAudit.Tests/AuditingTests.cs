using Microsoft.EntityFrameworkCore;
using System.Globalization;

using Bogus;
using FluentAssertions;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests;

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
            this._auditConfig.Config(dbContext, explicitInclude);

        // act
            (auditableRow, customAuditInfo) = await DataSetup.SeedAsync(dbContext);
        }

        // assert

        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Assertion.AssertTrailsAndGetColumnChangesAsync(
                dbContext,
                auditableRow.Id,
                this._auditConfig.AuditableTableAlias,
                AuditAction.Added,
                customAuditInfo,
                3);  // CompanyName, EnumColumn & Count


            Assertion.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                null,
                columnChanges[0],
                null,
                auditableRow.CompanyName);

            Assertion.AssertColumnValues(
                dbContext,
                t => t.Count,
                null,
                columnChanges[1],
                null,
                auditableRow.Count.ToString(CultureInfo.InvariantCulture));

            Assertion.AssertColumnValues(
                dbContext,
                t => t.EnumColumn,
                this._auditConfig.EnumColumnAlias,
                columnChanges[2],
                null,
                ((EnumColumn)auditableRow.EnumColumn).ToString());
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
        uint oldEnumValue = 0;

        var contextFactory = new ContextFactory();

        using (var dbContext = contextFactory.Create())
        {
            (auditableRow, customAuditInfo) = await DataSetup.SeedAsync(dbContext);

            // act
            this._auditConfig.Config(dbContext, explicitInclude);

            oldCompanyName = auditableRow.CompanyName;
            oldCount = auditableRow.Count;
            oldEnumValue = auditableRow.EnumColumn;

            auditableRow.CompanyName = new Faker().Random.String2(15);
            auditableRow.Count = new Faker().Random.Int(500, 10_000);

            var currentEnumValue = (EnumColumn)((int)auditableRow.EnumColumn);
            var newEnumColumn = new Faker().PickRandomWithout(currentEnumValue);
            auditableRow.EnumColumn = (uint)newEnumColumn;

            auditableRow.CreateAt = new Faker().Date.Past(); // not audited

            await dbContext.SaveChangesAsync(customAuditInfo);
        }

        // assert

        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Assertion.AssertTrailsAndGetColumnChangesAsync(
                dbContext,
                auditableRow.Id,
                this._auditConfig.AuditableTableAlias,
                AuditAction.Modified,
                customAuditInfo,
                3);  // CompanyName, EnumColumn & Count

            Assertion.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                null,
                columnChanges[0],
                oldCompanyName,
                auditableRow.CompanyName);

            Assertion.AssertColumnValues(
                dbContext,
                t => t.Count,
                null,
                columnChanges[1],
                oldCount.ToString(CultureInfo.InvariantCulture),
                auditableRow.Count.ToString(CultureInfo.InvariantCulture));

            Assertion.AssertColumnValues(
                dbContext,
                t => t.EnumColumn,
                this._auditConfig.EnumColumnAlias,
                columnChanges[2],
                ((EnumColumn)oldEnumValue).ToString(),
                ((EnumColumn)auditableRow.EnumColumn).ToString());
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
            (auditableRow, customAuditInfo) = await DataSetup.SeedAsync(dbContext);

            // act
            this._auditConfig.Config(dbContext, explicitInclude);

            dbContext.Remove(auditableRow);

            await dbContext.SaveChangesAsync(customAuditInfo);
        }

        // assert
        using (var dbContext = contextFactory.Create())
        {
            var columnChanges = await Assertion.AssertTrailsAndGetColumnChangesAsync(
                dbContext,
                auditableRow.Id,
                this._auditConfig.AuditableTableAlias,
                AuditAction.Deleted,
                customAuditInfo,
                3); // CompanyName, EnumColumn & Count

            Assertion.AssertColumnValues(
                dbContext,
                t => t.CompanyName,
                null,
                columnChanges[0],
                auditableRow.CompanyName,
                null);

            Assertion.AssertColumnValues(
                dbContext,
                t => t.Count,
                null,
                columnChanges[1],
                auditableRow.Count.ToString(CultureInfo.InvariantCulture),
                null);

            Assertion.AssertColumnValues(
                dbContext,
                t => t.EnumColumn,
                this._auditConfig.EnumColumnAlias,
                columnChanges[2],
                ((EnumColumn)auditableRow.EnumColumn).ToString(),
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
            (auditableRow, customAuditInfo) = await DataSetup.SeedAsync(dbContext);

            // act
            this._auditConfig.Config(dbContext, explicitInclude);

            // changes count and audit trails count should not change
            initialContextChangesCount = DataSetup.GetAuditableChangesCount(dbContext);

            initialAuditTrailsCount = await dbContext.AuditTrailTable.CountAsync();

            // we will trigger primary key violation error
            auditableRow.NotAuditableTableModelId = 1000;
            auditableRow.CompanyName = "Test";

            var action = async () => await dbContext.SaveChangesAsync();

            // assert
            await action.Should().ThrowAsync<DbUpdateException>();

            // added audit trail records should not be tracked anymore
            DataSetup.GetAuditableChangesCount(dbContext)
                .Should()
                .Be(initialContextChangesCount + 1); // the row that we changed
        }

        using (var dbContext = contextFactory.Create())
        {
            var newAuditTrailsCount = await dbContext.AuditTrailTable.CountAsync();

            // no audit trail records should be added
            newAuditTrailsCount.Should().Be(initialAuditTrailsCount);
        }
    }
}
