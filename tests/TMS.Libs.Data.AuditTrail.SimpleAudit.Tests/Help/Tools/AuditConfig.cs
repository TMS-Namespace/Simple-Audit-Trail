using Bogus;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Configurators;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help.Tools;

public class AuditConfig
{
    public string? AuditableTableAlias = null;
    public string? EnumColumnAlias = null;

    private static Faker _faker = new Faker();

    public void Config(AuditableContext dbContext, bool explicitInclude)
    {
        AuditableTableAlias = _faker.Random.Bool() ? _faker.Random.Word() : null;
        EnumColumnAlias = _faker.Random.Bool() ? _faker.Random.Word() : null;

        if (explicitInclude)
        {
            dbContext
                .ConfigureAuditTrail(this.AuditMappingCallBackAsync)
                    .ConfigureTableAudit<AuditableTableModel>(AuditableTableAlias)
                    .AuditColumns(
                        t => t.Count,
                        t => t.CompanyName)
                    .AuditColumn(t => t.EnumColumn, this.ValueMapperCallBack, EnumColumnAlias)
                .StartAuditing();
        }
        else
        {
            dbContext
                .ConfigureAuditTrail(this.AuditMappingCallBackAsync)
                    .AuditAllTables(AutoExcludeColumnType.AllSpecial)
                    .ConfigureTableAudit<NotAuditableTableModel>()
                        .ExcludeTableFromAuditing()
                    .ConfigureTableAudit<AuditableTableModel>(AuditableTableAlias)
                        .ExcludeColumnsFromAuditing(x => x.CreateAt) // only CompanyName, EnumColumn & Count columns are left
                        .AuditColumn(t => t.EnumColumn, this.ValueMapperCallBack, EnumColumnAlias) // overwrite column config
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
                    .Select(this.Map),
                cancellationToken);

            return new()
            {
                ReferenceId = (int)auditInfo.PrimaryKeyValue,
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
