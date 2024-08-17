using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Soenneker.Utils.AutoBogus;
using System.Linq.Expressions;
using System.Reflection;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;

internal static class Tools
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

    public static void AssertColumnValues(
        AuditableContext dbContext,
        Expression<Func<AuditableTableModel, object?>> propertyExpression,
        SerializableColumnChanges columnAuditInfo,
        string? oldValue,
        string? newValue)
    {
        var sqlColumnName = dbContext.GetSQLColumnName(propertyExpression);

        sqlColumnName.Should().Match(s => s.Contains('_') || s.All(char.IsLower));

        var (propName, propType) = GetExpressionDetails(propertyExpression);

        columnAuditInfo.PropertyName.Should().Be(propName);
        columnAuditInfo.DataTypeName.Should().Be(propType.Name);
        columnAuditInfo.ColumnSQLName.Should().Be(sqlColumnName);

        if (oldValue is null)
        {
            columnAuditInfo.OldValue.Should().BeNull();
        }
        else
        {
            columnAuditInfo.OldValue!.ToString().Should().Be(oldValue);
        }

        if (newValue is null)
        {
            columnAuditInfo.NewValue.Should().BeNull();
        }
        else
        {
            columnAuditInfo.NewValue!.ToString().Should().Be(newValue);
        }
    }

    public static async Task<List<SerializableColumnChanges>> AssertTrailsAndGetColumnChangesAsync(
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

        var columnChanges = await Serializing.DeserializeAsync<List<SerializableColumnChanges>>(auditTrail.Changes, CancellationToken.None);

        columnChanges.Should().NotBeNull();
        columnChanges!.Count.Should().Be(expectedColumnChanges);

        return columnChanges;
    }

    public static int GetAuditableChangesCount(AuditableContext dbContext)
        => dbContext
                .ChangeTracker
                .Entries()
                .Count(e => e.State
                    is EntityState.Added
                    or EntityState.Deleted
                    or EntityState.Modified);

    private static (string PropertyName, Type PropertyType) GetExpressionDetails(Expression<Func<AuditableTableModel, object?>> propertyExpression)
    {
        // if property returns reference type
        var expression = propertyExpression.Body as MemberExpression;

        if (expression == null)
        {
            // if the property returns value type
            var unaryExpression = (UnaryExpression)propertyExpression.Body;
            expression = (MemberExpression)unaryExpression.Operand;
        }

        var member = expression.Member;

        var propInfo = (PropertyInfo)member;

        return (member.Name, propInfo.PropertyType);
    }
}
