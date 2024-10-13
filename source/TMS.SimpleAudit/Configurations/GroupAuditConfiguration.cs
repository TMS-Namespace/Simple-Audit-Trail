using System.Linq.Expressions;
using System.Reflection;

using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Help;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Configurations;

public sealed class GroupAuditConfiguration<TGroupAuditModel>
    where TGroupAuditModel : class
{
    #region Private Vars

    private readonly Func<RowAuditInfo, object?, CancellationToken, Task<TGroupAuditModel?>>? _auditRecordGeneratorAsync;

    private readonly Func<RowAuditInfo, object?, CancellationToken, Task<List<TGroupAuditModel>>>? _auditRecordsGeneratorAsync;

    private readonly SimpleAuditManager _manager;

    private readonly GroupAuditSettings<TGroupAuditModel> _groupAuditSettings;

    private readonly Type _groupAuditModelType;

    #endregion

    #region Internal Constructors

    internal GroupAuditConfiguration(
        SimpleAuditManager manager,
        Func<RowAuditInfo, object?, CancellationToken, Task<TGroupAuditModel?>> auditRecordGeneratorAsync)
    {
        if (!manager.DataBaseContext.IsTableType<TGroupAuditModel>())
        {
            throw new AuditConfigurationException($"The type {typeof(TGroupAuditModel).Name} is not recognized as a table model.");
        }


        _manager = manager;
        _groupAuditModelType = typeof(TGroupAuditModel);

        _groupAuditSettings = _manager.AuditSettings.RequestGroupSettings<TGroupAuditModel>();

        _auditRecordGeneratorAsync = auditRecordGeneratorAsync;
        _groupAuditSettings.AuditRecordGeneratorAsync = auditRecordGeneratorAsync;// AuditRecordGeneratorAsync;
    }

    internal GroupAuditConfiguration(
    SimpleAuditManager manager,
    Func<RowAuditInfo, object?, CancellationToken, Task<List<TGroupAuditModel>>> auditRecordsGeneratorAsync)
    {
        if (!manager.DataBaseContext.IsTableType<TGroupAuditModel>())
        {
            throw new AuditConfigurationException($"The type {typeof(TGroupAuditModel).Name} is not recognized as a table model.");
        }


        _manager = manager;
        _groupAuditModelType = typeof(TGroupAuditModel);

        _groupAuditSettings = _manager.AuditSettings.RequestGroupSettings<TGroupAuditModel>();

        _auditRecordsGeneratorAsync = auditRecordsGeneratorAsync;
        _groupAuditSettings.AuditRecordsGeneratorAsync = auditRecordsGeneratorAsync;
    }

    #endregion

    #region Public Functions

    public GroupAuditConfiguration<TGroupAuditModel> StartAuditing()
    {
        _manager.AuditingIsEnabled = true;

        return this;
    }

    public TableAuditConfiguration<TGroupAuditModel, TTableModel> ConfigureTableAudit<TTableModel>(
        string? tableAlias = null, 
        Expression<Func<TTableModel, object?>>? referenceColumnExpression = null,
        Func<TTableModel, bool>? isSoftDeleted = null)
        where TTableModel : class
        => new(
            _manager, 
            _groupAuditSettings, 
            tableAlias,
            referenceColumnExpression,
            isSoftDeleted);

    public GroupAuditConfiguration<TGroupAuditModel> AuditAllTables(
        AutoExcludeColumnType exclusions = AutoExcludeColumnType.None)
    {
        var entityTypes = _manager
            .DataBaseContext
            .Model
            .GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(e => e != typeof(TGroupAuditModel)
                    && _manager.DataBaseContext.IsTableType(e));

        var methodName = nameof(TableAuditConfiguration<TGroupAuditModel, TGroupAuditModel>.AuditAllColumns);

        foreach (var entityType in entityTypes)
        {
            var genericType = typeof(TableAuditConfiguration<,>).MakeGenericType(_groupAuditModelType, entityType);

            var constructor = genericType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(SimpleAuditManager), typeof(GroupAuditSettings<TGroupAuditModel>)],
                    null);

            var instance = constructor!.Invoke([_manager, _groupAuditSettings]);

            var executeMethod = genericType.GetMethod(methodName);

            executeMethod!.Invoke(instance, [exclusions]);
        }

        return this;
    }

    #endregion

}
