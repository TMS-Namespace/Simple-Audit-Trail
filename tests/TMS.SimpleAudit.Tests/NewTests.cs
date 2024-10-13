using System.Linq.Expressions;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Tests.Help.Tools;
using TMS.SimpleAudit.Tests.TestDataBase;
using TMS.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.SimpleAudit.Tests;

public class NewTests
{
/*    [Fact]
    public async Task CreateAuditRecordExistanceCheck_CreateExpression_Correctly()
    {
        // Arrange
        var dbContext = new AuditableContext();
        var auditModel = new AuditableTableModel { Id = 1, Name = "Test" };
        var entityType = _manager.DataBaseContext.Model.FindEntityType(typeof(TGroupAuditModel));
        var properties = entityType!.GetProperties();

        var settings = new GroupAuditSettings<AuditableTableModel>(new SimpleAuditManager());
        var func = settings.CreateAuditRecordExistanceCheck();


        // Act
        var result = await func(dbContext, auditModel, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }*/
}