using Microsoft.EntityFrameworkCore;

using TMS.SimpleAudit.InfoModels;

namespace TMS.SimpleAudit.Settings;

internal interface IGroupAuditSettingsInternal
{
    Type AuditTrailModelType { get; }

    Task<bool> AuditRecordExistenceCheckAsync(
        DbContext dbContext,
        object auditModel,
        CancellationToken cancellationToken);

/*    void SetPropertyAuditSettings(
        Type type,
        string? tableAlias,
        string propertyName,
        Func<DbContext, object, object?, CancellationToken, Task<object?>>? valueMapperAsync,
        string? columnAlias,
        string referenceColumnName);*/

    void Validate();

/*    void SetPropertyAuditSettings(
        Type type,
        string? tableAlias,
        string propertyName,
        Func<object?, object?>? valueMapper,
        string? columnAlias,
        string referenceColumnName);

    void SetPropertyAuditSettings(
        Type type,
        string? tableAlias,
        List<string> propertiesNames,
        string referenceColumnName);*/

    void RemovePropertyAuditSettings(Type type, List<string> propertiesNames);

    void RemoveEntityAuditSettings(Type type);

    IEntityAuditSettingsInternal? GetEntityAuditSettings(Type type);

    Task<IEnumerable<object>> GenerateAuditRecordsAsync(
        RowAuditInfo rowAuditInfo,
        object? customAuditInfo,
        CancellationToken cancellationToken);
}