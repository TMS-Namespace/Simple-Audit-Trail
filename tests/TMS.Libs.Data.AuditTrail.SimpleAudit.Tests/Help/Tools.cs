using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soenneker.Utils.AutoBogus;
using System.Linq.Expressions;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help
{
    internal static class Tools
    {
        public static async Task<(AuditableTableModel, CustomAuditInfo)> SeedAsync(AuditableContext dbContext)
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
                .Generate();

            var customAuditInfo = new AutoFaker<CustomAuditInfo>()
                    .RuleFor(e => e.IpAddress, f => f.Internet.IpAddress().ToString())
                .Generate();

            await dbContext.AddAsync(notAuditableRow);
            await dbContext.AddAsync(auditableRow);

            var savedCount = await dbContext.SaveChangesAsync(customAuditInfo);

            savedCount.Should().Be(2);

            return (auditableRow, customAuditInfo);
        }

        public static void AssertColumnValues(
            AuditableContext dbContext,
            Expression<Func<AuditableTableModel, object?>> propertyExpression,
            ColumnAuditInfo columnAuditInfo,
            string? oldValue,
            string? newValue)
        {
            var sqlColumnName = dbContext.GetSQLColumnName(propertyExpression);

            sqlColumnName.Should().Match(s => s.Contains('_') || s.All(char.IsLower));

            columnAuditInfo.ColumnSQLName.Should().Be(sqlColumnName);

            columnAuditInfo.OldValue?.ToString().Should().Be(oldValue);
            columnAuditInfo.NewValue?.ToString()!.Should().Be(newValue);
        }

        public static async Task<List<ColumnAuditInfo>> AssertTrailsAndGetColumnChangesAsync(
            AuditableContext dbContext,
            int referenceId,
            AuditAction expectedAction,
            CustomAuditInfo expectedCustomAuditInfo,
            int expectedColumnChanges)
        {
            var sqlTableName = dbContext.GetSQLTableName<AuditableTableModel>();

            var auditTrails = await dbContext
                        .AuditTrailTable
                        .Where(at =>
                                at.ReferenceId == referenceId &&
                                at.TableName == sqlTableName &&
                                at.CreatedAt >= DateTime.UtcNow.AddSeconds(-1) &&
                                at.CreatedAt <= DateTime.UtcNow.AddSeconds(1))
                        .ToListAsync();

            auditTrails.Count.Should().Be(1);
            var auditTrail = auditTrails[0];

            auditTrail.Action.Should().Be(expectedAction.ToString());
            auditTrail.UserName.Should().Be(expectedCustomAuditInfo.UserName);
            auditTrail.IpAddress.Should().Be(expectedCustomAuditInfo.IpAddress);

            var columnChanges = await Serializing.DeserializeAsync<List<ColumnAuditInfo>>(auditTrail.Changes, CancellationToken.None);

            columnChanges.Should().NotBeNull();
            columnChanges!.Count.Should().Be(expectedColumnChanges);

            return columnChanges;
        }

    }
}
