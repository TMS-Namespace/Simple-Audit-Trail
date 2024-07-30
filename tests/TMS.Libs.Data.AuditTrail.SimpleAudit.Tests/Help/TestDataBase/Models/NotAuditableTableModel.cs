namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

// SQL table name: not_auditable_table
public partial class NotAuditableTableModel
{
    // primary key, DB-side auto-increment, SQL column name: id
    public int Id { get; set; }

    // SQL column name: name
    public required string Name { get; set; }

    // the relation to AuditableTableModel table
    public List<AuditableTableModel> AuditableTableModels { get; set; } = [];
}
