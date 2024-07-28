using Microsoft.EntityFrameworkCore.Query.Internal;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

// SQL table name: auditable_table
public partial class AuditableTableModel
{
    //primary key, DB-side auto-increment, SQL column name: id
    public int Id { get; set; }

    // SQL column name : company_name
    public string? CompanyName { get; set; }

    // SQL column name: count
    public int Count { get; set; }

    // virtual (computable) column from 'Count' column, and equals its value doubled, SQL column name: count_doubled
    public int CountDoubled { get; set; }

    // a read only property that will not be mapped to any SQL column
    public int CountTripled => Count * 3;

    // a property that will not be mapped to any SQL column
    public int CountQuadruplet
    {
        set { }
        get => Count * 4;
    }

    // SQL column name: created_id
    public DateTime CreateAt { get; set; }

    // foreign key to NotAuditableTableModel table, SQL column name: not_auditable_table_model_id
    public int NotAuditableTableModelId { get; set; }

    public required NotAuditableTableModel NotAuditableTableModel { get; set; }

}