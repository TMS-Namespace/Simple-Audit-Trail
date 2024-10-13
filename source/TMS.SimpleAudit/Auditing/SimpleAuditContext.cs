using Microsoft.EntityFrameworkCore;

using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Auditing;

public abstract class SimpleAuditContext : DbContext
{
    #region Private Vars

    internal readonly AuditContextWrapper<SimpleAuditContext> _wrapper;


    #endregion

    #region Public Properties

    /// <summary>
    /// Determines if auditing of changes will be performed.
    /// </summary>
    public bool AuditingIsEnabled
    {
        get => _wrapper.AuditingIsEnabled;
        set => _wrapper.AuditingIsEnabled = value;
    }

    public AuditSettings AuditSettings => _wrapper.AuditSettings;

    /// <summary>
    /// Determines if the context should capture the context of the current change, usually should be True when used in UI contexts
    /// </summary>
    public bool CaptureThreadContext
    {
        get => _wrapper.CaptureThreadContext;
        set => _wrapper.CaptureThreadContext = value;
    }

    #endregion

    #region Protected Constructors

    protected SimpleAuditContext(DbContextOptions<SimpleAuditContext> options)
        : base(options)
        => _wrapper = new(this);

    protected SimpleAuditContext(DbContextOptions options)
        : base(options)
        => _wrapper = new(this);

    protected SimpleAuditContext()
        => _wrapper = new(this);

    #endregion

    #region Public Functions

    /// <summary>
    /// Configures the auditing table, audit records generator, and specifies which tables/columns should be audited.
    /// </summary>
    /// <typeparam name="TAuditTrailTableModel">The model type of the table to audit.</typeparam>
    /// <param name="auditRecordGeneratorAsync">
    /// A callback that takes <see cref="RowAuditInfo"/> and custom audit information, as well as a <see cref="CancellationToken"/>,
    /// and returns a task that creates and returns the audit record of type <typeparamref name="TAuditTrailTableModel"/>.
    /// </param>
    /// <remarks>
    /// If the <paramref name="auditRecordGeneratorAsync"/> returns null, auditing of the current change will be skipped.
    /// </remarks>
    /// <returns>An <see cref="GroupAuditConfiguration{TAuditTrailTableModel}"/> instance.</returns>
    public GroupAuditConfiguration<TAuditTrailTableModel> ConfigureAuditTrailGroup<TAuditTrailTableModel>(
        Func<RowAuditInfo, object?, CancellationToken, Task<TAuditTrailTableModel?>> auditRecordGeneratorAsync)
        where TAuditTrailTableModel : class
        => _wrapper.ConfigureAuditTrailGroup(auditRecordGeneratorAsync);

    /// <summary>
    /// Configures the auditing table, audit records generator callback, and specifies which tables/columns should be audited.
    /// </summary>
    /// <typeparam name="TAuditTrailTableModel">The model type of the table to audit.</typeparam>
    /// <param name="auditRecordsGeneratorAsync">
    /// A callback that takes <see cref="RowAuditInfo"/> and custom audit information, as well as a <see cref="CancellationToken"/>,
    /// and returns a task that creates and returns a list of audit records of type <typeparamref name="TAuditTrailTableModel"/>.
    /// </param>
    /// <remarks>
    /// If the <paramref name="auditRecordsGeneratorAsync"/> returns an empty list, auditing of the current change will be skipped.
    /// </remarks>
    /// <returns>An <see cref="GroupAuditConfiguration{TAuditTrailTableModel}"/> instance.</returns>
    public GroupAuditConfiguration<TAuditTrailTableModel> ConfigureAuditTrailGroup<TAuditTrailTableModel>(
        Func<RowAuditInfo, object?, CancellationToken, Task<List<TAuditTrailTableModel>>> auditRecordsGeneratorAsync)
        where TAuditTrailTableModel : class
        => _wrapper.ConfigureAuditTrailGroup(auditRecordsGeneratorAsync);
    /*
        /// <summary>
        /// Configure which columns should be audited of the table of <typeparamref name="TTableModel"/> model type.
        /// </summary>
        /// <typeparam name="TTableModel">The table model type to configure.</typeparam>
        /// <param name="tableAlias">
        /// Sets the wanted alias for the given table.
        /// </param>
        /// <remarks>
        /// This can be used only after <paramref name="ConfigureAuditTrail"/> is being executed.
        /// </remarks>
        /// <returns>An <see cref="TableAuditConfiguration{TTableModel}"/> instance.</returns>
        public TableAuditConfiguration<TTableModel> ConfigureTableAudit<TTableModel>(string? tableAlias = null)
            where TTableModel : class
            => AuditingManager.ConfigureTableAudit<TTableModel>(tableAlias);*/

    #region Saving

    public override int SaveChanges()
        => SaveChangesAsync()
        .GetAwaiter()
        .GetResult();
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _wrapper
        .SaveChangesAsync(null, cancellationToken, null)
        .ConfigureAwait(_wrapper.CaptureThreadContext);

    public new async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
        => await Task.FromException<int>(new NotImplementedException());

    public int SaveChanges(object customAuditInfo)
        => SaveChangesAsync(customAuditInfo, CancellationToken.None)
        .GetAwaiter()
        .GetResult();


    public async Task<int> SaveChangesAsync(object customAuditInfo)
        => await _wrapper
        .SaveChangesAsync(customAuditInfo, CancellationToken.None, null)
        .ConfigureAwait(_wrapper.CaptureThreadContext);

    public async Task<int> SaveChangesAsync(
        object? customAuditInfo,
        CancellationToken cancellationToken,
        Func<Task<bool>>? checkRetrySuccessFallBackAsync = null)
    => await _wrapper
        .SaveChangesAsync(customAuditInfo, cancellationToken, checkRetrySuccessFallBackAsync)
        .ConfigureAwait(_wrapper.CaptureThreadContext);

    #endregion

    #endregion

}
