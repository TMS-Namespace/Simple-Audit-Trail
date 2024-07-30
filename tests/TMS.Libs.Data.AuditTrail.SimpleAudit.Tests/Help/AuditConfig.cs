using TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;

public class AuditConfig
{
    public void Config(AuditableContext dbContext, bool explicitInclude)
    {
        if (explicitInclude)
        {
            dbContext
                .ConfigureAuditTrail(AuditMappingCallBackAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                    .AuditColumns(
                        t => t.Count,
                        t => t.CompanyName)
                .StartAuditing();
        }
        else
        {
            dbContext
                .ConfigureAuditTrail(AuditMappingCallBackAsync)
                    .AuditAllTables(AutoExcludeColumnType.All)
                    .ConfigureTableAudit<NotAuditableTableModel>()
                        .ExcludeTableFromAuditing()
                    .ConfigureTableAudit<AuditableTableModel>()
                        .AuditAllColumns(AutoExcludeColumnType.All)
                        .ExcludeColumnsFromAuditing(x => x.CreateAt)  // only CompanyName & Count columns are left
                .StartAuditing();
        }
    }

    private async Task<AuditTrailTableModel?> AuditMappingCallBackAsync(
        RowAuditInfo auditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        var customInfo = customAuditInfo as CustomAuditInfo
            ?? new() { UserName = "System", IpAddress = "127.0.0.1" };

        if (auditInfo.TableModelType == typeof(AuditableTableModel))
        {
            var changesJson = await Serializing.SerializeAsync(auditInfo.ColumnsChanges, cancellationToken);

            return new()
            {
                ReferenceId = (int)auditInfo.PrimaryKeyValue,
                TableName = auditInfo.TableSQLName,

                Action = auditInfo.Action.ToString(),

                UserName = customInfo.UserName,
                IpAddress = customInfo.IpAddress,

                Changes = changesJson,

                CreatedAt = DateTime.UtcNow,
            };
        }

        throw new InvalidOperationException("Non intended model is audited.");
    }
}
