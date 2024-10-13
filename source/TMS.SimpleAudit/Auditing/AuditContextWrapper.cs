using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using TMS.SimpleAudit.Configurations;
using TMS.SimpleAudit.InfoModels;
using TMS.SimpleAudit.Settings;

namespace TMS.SimpleAudit.Auditing
{
    public sealed class AuditContextWrapper<TContext> : IDisposable
        where TContext : DbContext
    {
        #region Private Vars

        private readonly SimpleAuditManager _manager;

        #endregion

        #region Public Constructors

        public AuditContextWrapper(TContext dataBaseContext)
        {
            if (dataBaseContext is SimpleAuditContext)
            {
                throw new AuditingException($"You can't wrap a DBContext that already inherits {nameof(SimpleAuditContext)}, since it is already wrapped.");
            }

            _manager = new(dataBaseContext);
        }

        #endregion

        #region Internal Constructors

        internal AuditContextWrapper(SimpleAuditContext dataBaseContext)
        =>  _manager = new(dataBaseContext);

        #endregion

        #region Public Properties

        /// <summary>
        /// Determines if auditing of changes will be performed.
        /// </summary>
        public bool AuditingIsEnabled
        {
            get => _manager.AuditingIsEnabled;
            set => _manager.AuditingIsEnabled = value;
        }

        /// <summary>
        /// New auditing session can't be started, before the previous one is finished.
        /// </summary>
        public bool IsAuditingSessionStarted => _manager.IsAuditingSessionStarted; 

        public AuditSettings AuditSettings => _manager.AuditSettings;

        /// <summary>
        /// Determines if the context should capture the context of the current change, usually should be True when used in UI contexts
        /// </summary>
        public bool CaptureThreadContext
        {
            get => _manager.CaptureThreadContext;
            set => _manager.CaptureThreadContext = value;
        }

        public DbContext DataBaseContext => _manager.DataBaseContext;

        public bool HasAuditableChanges => _manager.HasAuditableChanges;

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
            => _manager.ConfigureAuditTrailGroup(auditRecordGeneratorAsync);

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
            => _manager.ConfigureAuditTrailGroup(auditRecordsGeneratorAsync);


        #region Saving

        public async Task<int> SaveChangesAsync(
            object? customAuditInfo,
            CancellationToken cancellationToken,
            Func<Task<bool>>? checkRetrySuccessFallBackAsync)
        {
            _manager.OnBeforeSaving();

            if (!_manager.HasAuditableChanges) // No audit trail needed
            {
                return await SaveWithOutRetryAsync(customAuditInfo, null, cancellationToken)
                    .ConfigureAwait(_manager.CaptureThreadContext);
            }

            var strategy = _manager
                .DataBaseContext
                .Database
                .CreateExecutionStrategy();

            if (strategy.RetriesOnFailure)
            {
                return await SaveWithRetryAsync(
                        strategy,
                        customAuditInfo,
                        checkRetrySuccessFallBackAsync,
                        cancellationToken)
                    .ConfigureAwait(_manager.CaptureThreadContext);
            }

            using var transaction = await _manager
                .DataBaseContext
                .Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(_manager.CaptureThreadContext);

            return await SaveWithOutRetryAsync(customAuditInfo, transaction, cancellationToken)
                .ConfigureAwait(_manager.CaptureThreadContext);
        }

        #endregion

        #endregion

        #region Private Functions

        private async Task<int> SaveWithOutRetryAsync(
            object? customAuditInfo,
            IDbContextTransaction? transaction,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _manager
                    .DataBaseContext
                    .SaveChangesAsync(false, cancellationToken)
                    .ConfigureAwait(_manager.CaptureThreadContext);

                if (transaction is not null)
                {
                    await _manager
                        .OnAfterSaveAsync(
                            _manager.DataBaseContext.SaveChangesAsync,
                            customAuditInfo,
                            cancellationToken)
                        .ConfigureAwait(_manager.CaptureThreadContext);

                    await transaction
                        .CommitAsync(cancellationToken)
                        .ConfigureAwait(_manager.CaptureThreadContext);
                }

                _manager.OnFinalize(false);

                return result;
            }
            catch (Exception)
            {
                if (transaction is not null)
                {
                    await transaction
                        .RollbackAsync(cancellationToken)
                        .ConfigureAwait(_manager.CaptureThreadContext);
                }

                _manager.OnFinalize(true);

                throw;
            }
        }

        private async Task<int> SaveWithRetryAsync(
            IExecutionStrategy executionStrategy,
            object? customAuditInfo,
            Func<Task<bool>>? checkRetrySuccessFallBackAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await executionStrategy.ExecuteInTransactionAsync(
                    operation: async (cnt) =>
                    {
                        try
                        {
                            var result = await _manager
                                .DataBaseContext
                                .SaveChangesAsync(false, cnt)
                                .ConfigureAwait(_manager.CaptureThreadContext);

                            await _manager
                                .OnAfterSaveAsync(
                                    _manager.DataBaseContext.SaveChangesAsync,
                                    customAuditInfo,
                                    cnt)
                                .ConfigureAwait(_manager.CaptureThreadContext);

                            return result;
                        }
                        catch (Exception)
                        {
                            _manager.OnRetryFail();

                            throw;
                        }
                    },
                    verifySucceeded: async (cnt) =>
                    {
                        var checkResult = await _manager
                            .CheckAuditSuccessAsync(cnt)
                            .ConfigureAwait(_manager.CaptureThreadContext);

                        if (checkResult != null)
                        {
                            return checkResult.Value;
                        }

                        if (checkRetrySuccessFallBackAsync != null)
                        {
                            return await checkRetrySuccessFallBackAsync()
                                .ConfigureAwait(_manager.CaptureThreadContext);
                        }

                        return true;
                    },
                    cancellationToken);

                _manager.OnFinalize(false);

                return result;
            }
            catch (Exception)
            {
                _manager.OnFinalize(true);

                throw;
            }
        }

        #endregion

        #region Disposing

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _manager.Dispose();
                }

                _disposed = true;
            }
        }

        ~AuditContextWrapper() => Dispose(false);

        #endregion

    }
}
