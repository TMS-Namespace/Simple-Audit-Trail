using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help
{
    internal class ContextFactory : IDisposable
    {
        private readonly DbContextOptions<SimpleAuditContext> _options;

        private readonly SqliteConnection _connection;

        private bool _isFirstContextInstance = true;

        public ContextFactory()
        {
            Batteries.Init();

            // in-memory DB life time is bounded to connection openness,
            // so we need to re-use options for every new Context instance
            // in factory life scope
            _connection = new SqliteConnection("DataSource=:memory:");

            _options = new DbContextOptionsBuilder<SimpleAuditContext>()
                .UseSqlite(_connection)
                .Options;

        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        public AuditableContext Create()
        {
            var dbContext = new AuditableContext(_options);

            if (_isFirstContextInstance)
            {
                dbContext.Database.OpenConnection();
                dbContext.Database.EnsureCreated();

                _isFirstContextInstance = false;
            }

            return dbContext;
        }

    }
}
