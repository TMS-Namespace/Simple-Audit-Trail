using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

using FluentAssertions;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools;

internal static class Assertion
{
    public static void AssertColumnValues(
        AuditableContext dbContext,
        Expression<Func<AuditableTableModel, object?>> propertyExpression,
        string? columnAlias,
        SerializableColumnChanges columnAuditInfo,
        string? oldValue,
        string? newValue)
    {
        var sqlColumnName = dbContext.GetSQLColumnName(propertyExpression);

        sqlColumnName.Should().Match(s => s.Contains('_') || s.All(char.IsLower));

        var (propName, propType) = GetExpressionDetails(propertyExpression);

        columnAuditInfo.PropertyName.Should().Be( propName);
        columnAuditInfo.DataTypeName.Should().Be(propType.Name);
        columnAuditInfo.ColumnSQLName.Should().Be(columnAlias ?? sqlColumnName);

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

    public static void AssertThrow(SimpleAuditContext dbContext, Action<SimpleAuditContext> action)
    {
        dbContext
                .Invoking(ctx => action(ctx))
                .Should()
                .Throw<InvalidOperationException>();
    }

    public static async Task<List<SerializableColumnChanges>> AssertTrailsAndGetColumnChangesAsync(
        AuditableContext dbContext,
        int referenceId,
        string? tableAlias,
        AuditAction expectedAction,
        CustomAuditInfo expectedCustomAuditInfo,
        int expectedColumnChanges)
    {
        var sqlTableName = tableAlias ?? dbContext.GetSQLTableName<AuditableTableModel>();

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
