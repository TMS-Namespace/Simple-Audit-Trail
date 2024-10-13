using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Tests.Help.Tools;
using TMS.SimpleAudit.Tests.TestDataBase;
using TMS.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.SimpleAudit.Tests;

public class ConfigurationTests
{
    private Task<AuditTrailTableModel?> AuditRecordGeneratorAsync(
        RowAuditInfo auditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
            => Task.FromResult<AuditTrailTableModel?>(null);

    private AuditableContext GetContext()
    {
        var contextFactory = new ContextWrapperFactory();

        return contextFactory.Create();
    }

    [Fact]
    public void Should_Throw_On_Starting_Auditing_With_No_Configuration()
    {
        // arrange
        var dbContext = GetContext();

        // assert
        Assertion.AssertConfigException(dbContext, (ctx) =>
            ctx.AuditingIsEnabled = true);

        Assertion.AssertConfigException(dbContext, (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                .StartAuditing();
        });

        Assertion.AssertConfigException(dbContext, (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                .StartAuditing();
        });

    }

    [Fact]
    public void Should_Throw_On_No_Tables_Left_To_Audit()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                        .AuditAllColumns()
                    .ExcludeTableFromAuditing();
        });


    [Fact]
    public void Should_Throw_On_No_Columns_Left_To_Audit()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                        .AuditColumn(t => t.EnumColumn)
                        .ExcludeColumnsFromAuditing(t => t.EnumColumn);
        });

    [Fact]
    public void Should_Throw_On_Wrong_Table_Model_Type_To_Audit()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<ContextWrapperFactory>();
        });

    [Fact]
    public void Should_Throw_On_Bad_Column_To_Audit()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                    .AuditColumn(t => t.CountTripled);
        });

    [Fact]
    public void Should_Throw_On_Non_Primitive_Column_Expression()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                    .AuditColumn(t => t.Count + 2);
        });

    [Fact]
    public void Should_Throw_On_Configuring_Audit_Trail_Table()
    =>
        Assertion.AssertConfigException(GetContext(), (ctx) =>
        {
            ctx
                .ConfigureAuditTrailGroup(AuditRecordGeneratorAsync)
                    .ConfigureTableAudit<AuditTrailTableModel>();
        });
}
