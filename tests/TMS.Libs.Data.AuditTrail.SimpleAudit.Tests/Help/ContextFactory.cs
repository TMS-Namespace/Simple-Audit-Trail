using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.Help;

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
        this._connection = new SqliteConnection("DataSource=:memory:");

        this._options = new DbContextOptionsBuilder<SimpleAuditContext>()
            .UseSqlite(this._connection)
            .Options;
    }

    public void Dispose()
    {
        this._connection.Close();
        this._connection.Dispose();
    }

    public AuditableContext Create()
    {
        var dbContext = new AuditableContext(this._options);

        if (this._isFirstContextInstance)
        {
            dbContext.Database.OpenConnection();
            dbContext.Database.EnsureCreated();

            this._isFirstContextInstance = false;
        }

        return dbContext;
    }
}
