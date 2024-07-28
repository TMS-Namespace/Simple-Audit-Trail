# Simple Audit Trail

A simple and fast plug & play library to enable audit trails in any database using Entity Framework.

## Features

- All audit trails will be saved to one table that you specify.
- Tables and the columns that should be audited can be fully configured.
- Mapping of the audit information that will be saved is configurable.
- Custom audit information can be passed.
- Audit trail will be saved along with all other changes in a single transaction, and rolled back in case of any error.
- Tables and columns are audited with original SQL names, not the EF model names.

## Usage

Inherit your DataBase context from `SimpleAuditContext`:

```csharp
using TMS.Libs.Data.AuditTrail.SimpleAudit;

public partial class MyDbContext : SimpleAuditContext
{
    ...
}
```

for example, assuming that your audit table model `MyAuditTrailModel` defined as:

```csharp
public partial class MyAuditTrailModel
{
    // table primary key, auto-incremental
    public int Id { get; set; }

    public int ReferenceId { get; set; }
    public string TableName { get; set; }

    // information for the below two properties will be
    // passed using the 'MyCustomAuditInfo' record
    // that will be defined in what follows
    public string UserName { get; set; }
    public string IpAddress { get; set; }

    public string Action { get; set; }
    // we will save column changes in JSON format
    public string ChangesAsJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
```

and we assume that your custom audit information are defined as the following record:

```csharp
public record MyCustomAuditInfo
{
    public required string UserName { get; set; }
    public required string IpAddress { get; set; }
}
```

Configuration can be done using fluent API as follows:

```csharp
var dbContext = new MyDbContext();

// start audit trail configuration, by defining the
// table model type where audit trail will be saved,
// as well as passing the audit trail information
// mapping function as a call back that will be
// defined in what follows
dbContext.ConfigureAuditTrail<MyAuditTrailModel>(MyAuditMappingCallBackAsync)
         // configure how 'MyTableModel1' should be audited
         .ConfigureTableAudit<MyTableModel1>()
            // start with tracking all table columns except primary and foreign keys
            .AuditAllColumns(AutoExcludeColumnType.PrimaryKey | AutoExcludeColumnType.ForeignKey)
            // but also exclude those two
            .ExcludeColumns(
                tbl => tbl.Column3,
                tbl => tbl.Column4)
         // configure how 'MyTableModel2' should be audited
         .ConfigureTableAudit<MyTableModel2>()
            // audit only two of the columns
            .AuditColumns(
                tbl => tbl.Column1,
                tbl => tbl.Column2)
         // activate auditing immediately
         .StartAuditing();
```

audit information for every row are passed as `RowAuditInfo` class instance, and `rowAuditInfo.ColumnChanges` is a list of `ColumnAuditInfo` instances that holds information about the audited columns (like sql column name, old and new value). `RowAuditInfo` class instance is passed to the mapping call back function, that can be defined as follows:

```csharp
public async Task<MyAuditTrailModel?> MyAuditMappingCallBackAsync(
    RowAuditInfo auditInfo,
    object? customAuditInfo,
    CancellationToken cancellationToken)
{
    // get custom audit information, if any
    var customInfo = customAuditInfo as MyCustomAuditInfo;

    // if no custom info is available, we may assume that the
    // operation is executed by the 'system'
    if (customInfo == null)
    {
        customInfo = new() {UserName = "System", IpAddress = "127.0.0.1"};
    }

    // mapping can be customized depending on the audited table
    if (auditInfo.TableModelType == typeof(MyTableModel1))
    {
        // lets generate json form the column changes (we are
        // skipping the part of converting to stream)
        var changesJson = await JsonSerializer.SerializeAsync(auditInfo.ColumnsChanges, cancellationToken);

        // return the audit trail row
        return new()
        {
            ReferenceId = (int)auditInfo.PrimaryKeyValue,
            TableName = auditInfo.TableSQLName,
            Action = auditInfo.Action.ToString(),

            UserName = customInfo.UserName,
            IpAddress = customInfo.IpAddress,

            ChangesAsJson = changesJson,

            CreatedAt = DateTime.UtcNow
        };
    }

    // if null is returned, the auditing of the current row will be skipped

    // beside for `MyTableModel1` table, we do not want to save
    // any audit trails, although audit trail for them is configured.
    return null;
}
```

finally, above call back function will be executed whenever auditing is enabled, and `.SaveChanges()` is called:

```csharp

var customInfo = new MyCustomAuditInfo()
    {
        UserName = "Mr. A.B.",
        IpAddress = "15.15.15.15"
    };

await dbContext.SaveChangesAsync(customInfo, someCancellationToken);
```

## Dependencies

The project is built using .Net 8, and has no external dependencies (Beside EF).
