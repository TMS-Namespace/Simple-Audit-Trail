using Bogus;
using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Tests.Help.TestDataBase;
using TMS.SimpleAudit.Tests.TestDataBase;
using TMS.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.SimpleAudit.Tests.Help.Tools;

public class AuditConfig
{
    public string? AuditableTableAlias = null;
    public string? EnumColumnAlias = null;

    private static Faker _faker = new Faker();

    public void Config(AuditableContext wrapper, bool explicitInclude)
    {
        AuditableTableAlias = _faker.Random.Bool() ? _faker.Random.Word() : null;
        EnumColumnAlias = _faker.Random.Bool() ? _faker.Random.Word() : null;

        if (explicitInclude)
        {
            wrapper
                .ConfigureAuditTrailGroup(AuditMappingCallBackAsync)
                    .ConfigureTableAudit<AuditableTableModel>(AuditableTableAlias)
                    .AuditColumns(
                        t => t.Count,
                        t => t.CompanyName)
                    .AuditColumn(t => t.EnumColumn, ValueMapperCallBack, EnumColumnAlias)
                .StartAuditing();
        }
        else
        {
            wrapper
                .ConfigureAuditTrailGroup(AuditMappingCallBackAsync)
                    .AuditAllTables(AutoExcludeColumnType.AllSpecial)
                    .ConfigureTableAudit<NotAuditableTableModel>()
                        .ExcludeTableFromAuditing()
                    .ConfigureTableAudit<AuditableTableModel>(AuditableTableAlias)
                        .ExcludeColumnsFromAuditing(x => x.CreateAt) // only CompanyName, EnumColumn & Count columns are left
                        .AuditColumn(t => t.EnumColumn, ValueMapperCallBack, EnumColumnAlias) // overwrite column config
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
            ColumnSQLName = changes.ColumnNameAlias ?? changes.ColumnSQLName,
            DataTypeName = changes.DataType.Name,
            NewValue = changes.NewValue,
            OldValue = changes.OldValue,
            DataSQLTypeName = changes.DataSQLTypeName,
            PropertyName =  changes.PropertyName,
        };

    private async Task<AuditTrailTableModel?> AuditMappingCallBackAsync(
        RowAuditInfo auditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken)
    {
        var customInfo = customAuditInfo as CustomAuditInfo
            ?? new() { UserName = "System", IpAddress = "127.0.0.1" };

        if (auditInfo.RowModel.GetType() == typeof(AuditableTableModel))
        {
            var changesJson = await Serializing
                .SerializeAsync(
                    auditInfo
                    .ColumnsChanges
                    .Select(Map),
                cancellationToken);

            return new()
            {
                ReferenceId = (int)auditInfo.ReferenceKeyValue,
                TableName = auditInfo.TableNameAlias ?? auditInfo.TableSQLName,

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
