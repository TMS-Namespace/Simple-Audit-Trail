using System.Reflection;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configurators;

public sealed class AuditTrailConfiguration<TAuditTrailModel>
    where TAuditTrailModel : class
{
    #region Vars

    private readonly SimpleAuditContext _dbContext;

    private readonly Func<RowAuditInfo, object?, CancellationToken, Task<TAuditTrailModel?>> _auditMappingCallBackAsync;

    #endregion

    #region Private

    private async Task<object?> AuditMappingCallBackAsync(RowAuditInfo rowAuditInfo, object? customAuditInfo, CancellationToken cancellationToken)
        // This is a trick to define AuditMappingCallBackAsync as a variable on the
        // AuditSettings class level while staying ignorant about TAuditTrailModel generic type,
        // and to avoid using reflection during invoking the callback.
        => await _auditMappingCallBackAsync(rowAuditInfo, customAuditInfo, cancellationToken);

    #endregion

    #region Internal

    internal AuditTrailConfiguration(
        SimpleAuditContext dbContext,
        Func<RowAuditInfo, object?, CancellationToken, Task<TAuditTrailModel?>> auditMappingCallBackAsync)
    {
        if (!dbContext.IsTableType<TAuditTrailModel>())
        {
            throw new InvalidOperationException($"The type {typeof(TAuditTrailModel).Name} is not recognized as a table model.");
        }

        _dbContext = dbContext;

        _dbContext.AuditSettings.AuditTrailTableModelType = typeof(TAuditTrailModel);

        _auditMappingCallBackAsync = auditMappingCallBackAsync;
        _dbContext.AuditSettings.AuditMappingCallBackAsync = AuditMappingCallBackAsync;
    }

    #endregion

    #region Public

    public AuditTrailConfiguration<TAuditTrailModel> StartAuditing()
    {
        _dbContext.AuditingIsEnabled = true;

        return this;
    }

    public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>(string? tableAlias = null)
        where TTableModel : class
        => new(_dbContext, tableAlias);

    public AuditTrailConfiguration<TAuditTrailModel> AuditAllTables(AutoExcludeColumnType exclusions = AutoExcludeColumnType.None)
    {
        var entityTypes = _dbContext.Model.GetEntityTypes()
                    .Select(e => e.ClrType)
                    .Where(e => e != typeof(TAuditTrailModel)
                            && _dbContext.IsTableType(e));

        var methodName = nameof(TableAuditConfiguration<TAuditTrailModel>.AuditAllColumns);

        foreach (var entityType in entityTypes)
        {
            var genericType = typeof(TableAuditConfiguration<>).MakeGenericType(entityType);

            var constructor = genericType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(SimpleAuditContext), typeof(string)],
                    null);

            var instance = constructor!.Invoke([_dbContext, null]);

            var executeMethod = genericType.GetMethod(methodName);

            executeMethod!.Invoke(instance, [exclusions]);
        }

        return this;
    }

    #endregion
}
