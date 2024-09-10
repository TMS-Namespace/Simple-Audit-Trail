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
        // this is a trick to be able to define AuditMappingCallBackAsync as a variable on the
        // AuditSettings class level while stay ignorant about TAuditTrailModel generic type, and
        // to avoid using reflection during invoking the call back.
        => await this._auditMappingCallBackAsync(rowAuditInfo, customAuditInfo, cancellationToken);

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

        this._dbContext = dbContext;

        this._dbContext.AuditSettings.AuditTrailTableModelType = typeof(TAuditTrailModel);

        this._auditMappingCallBackAsync = auditMappingCallBackAsync;
        this._dbContext.AuditSettings.AuditMappingCallBackAsync = this.AuditMappingCallBackAsync;
    }

    #endregion

    #region Public

    public AuditTrailConfiguration<TAuditTrailModel> StartAuditing()
    {
        this._dbContext.AuditingIsEnabled = true;

        return this;
    }

    public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>(string? tableAlias = null)
        where TTableModel : class
        => new(this._dbContext, tableAlias);

    public AuditTrailConfiguration<TAuditTrailModel> AuditAllTables(AutoExcludeColumnType exclusions = AutoExcludeColumnType.None)
    {
        var entityTypes = _dbContext.Model.GetEntityTypes()
                    .Select(e => e.ClrType)
                    .Where(e => e != typeof(TAuditTrailModel)
                            && this._dbContext.IsTableType(e));

        var methodName = nameof(TableAuditConfiguration<TAuditTrailModel>.AuditAllColumns);

        foreach (var entityType in entityTypes)
        {
            var genericType = typeof(TableAuditConfiguration<>).MakeGenericType(entityType);

            var constructor = genericType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(SimpleAuditContext), typeof(string)],
                    null);

            var instance = constructor!.Invoke([this._dbContext, null]);

            var executeMethod = genericType.GetMethod(methodName);

            executeMethod!.Invoke(instance, [exclusions]);
        }

        return this;
    }

    #endregion
}
