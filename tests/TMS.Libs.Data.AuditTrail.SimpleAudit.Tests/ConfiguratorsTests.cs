using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests
{
    public class ConfiguratorsTests
    {
        private async Task<AuditTrailTableModel?> AuditMappingCallBackAsync(
            RowAuditInfo auditInfo,
            object? customAuditInfo,
            CancellationToken cancellationToken)
                => null;

        private AuditableContext GetContext()
        {
            var contextFactory = new ContextFactory();

            return contextFactory.Create();
        }

        [Fact]
        public void Should_Throw_On_Starting_Auditing_With_No_Configuration()
        {
            // arrange
            var dbContext = GetContext();

            // assert
            Assertion.AssertThrow(dbContext, (ctx) => ctx.AuditingIsEnabled = true);

            Assertion.AssertThrow(dbContext, (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                    .StartAuditing();
            });

            Assertion.AssertThrow(dbContext, (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditableTableModel>()
                    .StartAuditing();
            });

        }

        [Fact]
        public void Should_Throw_On_No_Tables_Left_To_Audit()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditableTableModel>()
                            .AuditAllColumns()
                        .ExcludeTableFromAuditing();
            });


        [Fact]
        public void Should_Throw_On_No_Columns_Left_To_Audit()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditableTableModel>()
                            .AuditColumn(t => t.EnumColumn)
                            .ExcludeColumnsFromAuditing(t => t.EnumColumn);
            });

        [Fact]
        public void Should_Throw_On_Wrong_Table_Model_Type_To_Audit()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<ContextFactory>();
            });

        [Fact]
        public void Should_Throw_On_Wrong_Column_To_Audit()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditableTableModel>()
                        .AuditColumn(t => t.CountTripled);
            });

        [Fact]
        public void Should_Throw_On_Non_Primitive_Column_Expression()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditableTableModel>()
                        .AuditColumn(t => t.Count + 2);
            });

        [Fact]
        public void Should_Throw_On_Configuring_Audit_Trail_Table()
        =>
            Assertion.AssertThrow(GetContext(), (ctx) =>
            {
                ctx
                    .ConfigureAuditTrail(AuditMappingCallBackAsync)
                        .ConfigureTableAudit<AuditTrailTableModel>();
            });
    }
}
