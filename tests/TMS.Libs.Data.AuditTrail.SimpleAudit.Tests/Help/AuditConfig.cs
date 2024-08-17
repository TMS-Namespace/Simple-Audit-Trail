using TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.TestDataBase;
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
                .ConfigureAuditTrail(this.AuditMappingCallBackAsync)
                    .ConfigureTableAudit<AuditableTableModel>()
                    .AuditColumns(
                        t => t.Count,
                        t => t.CompanyName)
                    .AuditColumn(t => t.EnumColumn, this.ValueMapperCallBack)
                .StartAuditing();
        }
        else
        {
            dbContext
                .ConfigureAuditTrail(this.AuditMappingCallBackAsync)
                    .AuditAllTables(AutoExcludeColumnType.All)
                    .ConfigureTableAudit<NotAuditableTableModel>()
                        .ExcludeTableFromAuditing()
                    .ConfigureTableAudit<AuditableTableModel>()
                        .ExcludeColumnsFromAuditing(x => x.CreateAt) // only CompanyName, EnumColumn & Count columns are left
                        .AuditColumn(t => t.EnumColumn, this.ValueMapperCallBack) // overwrite column config
                .StartAuditing();
        }
    }

    private string? ValueMapperCallBack(object? value)
    {
        if (value == null)
        {
            return null;
        }
        else
        {
            return ((EnumColumn)(int)(uint)value).ToString();
        }
    }

    private SerializableColumnChanges Map(ColumnAuditInfo changes)
    => new()
        {
            ColumnSQLName = changes.ColumnSQLName,
            DataTypeName = changes.DataType.Name,
            NewValue = changes.NewValue,
            OldValue = changes.OldValue,
            PropertyName = changes.PropertyName,
        };

    private async Task<AuditTrailTableModel?> AuditMappingCallBackAsync(
        RowAuditInfo auditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        var customInfo = customAuditInfo as CustomAuditInfo
            ?? new() { UserName = "System", IpAddress = "127.0.0.1" };

        if (auditInfo.ModelType == typeof(AuditableTableModel))
        {
            var changesJson = await Serializing
                .SerializeAsync(
                    auditInfo
                    .ColumnsChanges
                    .Select(this.Map),
                cancellationToken);

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

        throw new InvalidOperationException("A non intended model is audited.");
    }
}
