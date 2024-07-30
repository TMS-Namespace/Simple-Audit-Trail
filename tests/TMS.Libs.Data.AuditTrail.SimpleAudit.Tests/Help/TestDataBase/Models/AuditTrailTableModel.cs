namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

// SQL table name: audit_trail_table
public partial class AuditTrailTableModel
{
    // primary key, DB-side auto-increment, SQL column name: id
    public int Id { get; set; }

    // SQL column name: reference_id
    public int ReferenceId { get; set; }

    // SQL column name: table_name
    public required string TableName { get; set; }

    // SQL column name: user_name
    public required string UserName { get; set; }

    // SQL column name: ip_address
    public required string IpAddress { get; set; }

    // SQL column name: action
    public required string Action { get; set; }

    // has a max length of 2048 characters, SQL column name: changes
    public required string Changes { get; set; }

    // has default value, which is current UTC time, SQL column name: created_at
    public DateTime CreatedAt { get; set; }
}