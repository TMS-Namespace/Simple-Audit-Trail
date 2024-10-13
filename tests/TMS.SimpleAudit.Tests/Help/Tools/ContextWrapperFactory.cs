using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

using TMS.SimpleAudit.Auditing;
using TMS.SimpleAudit.Tests.TestDataBase;

namespace TMS.SimpleAudit.Tests.Help.Tools;

internal class ContextWrapperFactory : IDisposable
{
    private readonly DbContextOptions<SimpleAuditContext> _options;

    private readonly SqliteConnection _connection;

    private bool _isFirstContextInstance = true;

    public ContextWrapperFactory()
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
