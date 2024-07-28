using System.Reflection;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;

public sealed class AuditTrailConfiguration<TAuditTrailModel>
    where TAuditTrailModel : class
{
    #region Vars

    private readonly SimpleAuditContext _dbContext;

    #endregion

    #region Internal

    internal AuditTrailConfiguration(
        SimpleAuditContext dbContext,
        Func<RowAuditInfo, object, CancellationToken, Task<TAuditTrailModel?>> auditMappingCallBack)
    {

        if (!dbContext.IsTableType<TAuditTrailModel>())
        {
            throw new InvalidOperationException($"The type {typeof(TAuditTrailModel).Name} is not recognized as a table model.");
        }

        _dbContext = dbContext;

        _dbContext.AuditTrailTableModelType = typeof(TAuditTrailModel);
        _dbContext.AuditMappingCallBackAsync = auditMappingCallBack;
    }

    #endregion

    #region Public

    public AuditTrailConfiguration<TAuditTrailModel> StartAuditing()
    {
        _dbContext.AuditingIsEnabled = true;

        return this;
    }

    public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>()
        where TTableModel : class
        => new(_dbContext);

    public AuditTrailConfiguration<TAuditTrailModel> AuditAllTables(AutoExcludeColumnType exclusions = AutoExcludeColumnType.None)
    {
        var entityTypes = _dbContext.Model.GetEntityTypes()
                    .Select(e => e.ClrType)
                    .Where(e => e != typeof(TAuditTrailModel) && _dbContext.IsTableType(e));

        var methodName = nameof(TableAuditConfiguration<TAuditTrailModel>.AuditAllColumns);

        foreach (var entityType in entityTypes)
        {

            var genericType = typeof(TableAuditConfiguration<>).MakeGenericType(entityType);

            var constructor = genericType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(SimpleAuditContext)],
                    null);

            var instance = constructor!.Invoke([_dbContext]);

            var executeMethod = genericType.GetMethod(methodName);

            executeMethod!.Invoke(instance, [exclusions]);
        }

        return this;
    }

    #endregion
}
