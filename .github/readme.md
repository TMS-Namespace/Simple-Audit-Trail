# Simple Audit Trail

A light, simple, and fast plug-and-play library to enable audit trails in any database using Entity Framework.

## Features

- All audit trails will be saved to a specified table.
- Tables and columns that should be audited can be fully configured.
- The mapping of the audit information to be saved is configurable.
- Value mapping can be configured on a per-column basis.
- Custom audit information can be passed.
- The audit trail will be saved along with all other changes in a single transaction and rolled back in case of any error.
- Tables and columns are audited with their original SQL names, not the mapped EF model/property names.

## Auditing details

Below is a table that describes which actions are audited and when the old/new values are considered null:

| Action   | Old value | New Value |
|:--------:|:---------:|:---------:|
| Added    |    Null   |   value   |
| Modified |   value   | value     |
| Deleted  |   value   | Null      |

## Usage

### Setup

Inherit your DataBase context from `SimpleAuditContext`:

```csharp
using TMS.Libs.Data.AuditTrail.SimpleAudit;

public partial class MyDbContext : SimpleAuditContext
{
    ...
}
```

For example, assuming that your audit table model is `MyAuditTrailModel` and defined as:

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

If we assume that you want to save also user name and IP address of each operation, we define the following custom audit record information:

```csharp
public record MyCustomAuditInfo
{
    public required string UserName { get; set; }
    public required string IpAddress { get; set; }
}
```

### Configuration

Configuration can be done using the Fluent API and fine-tuned for every table and column.

We start by defining the table model type where the audit trail will be saved (in our case, `MyAuditTrailModel`), along with the audit trail mapping function as a callback (in our case, `MyAuditMappingCallBackAsync`, which will be defined later):

```csharp
var dbContext = new MyDbContext();

dbContext
    .ConfigureAuditTrail<MyAuditTrailModel>(MyAuditMappingCallBackAsync)
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

### Mapping to your audit trail table

The audit information for each row is stored as an instance of the `RowAuditInfo` class, and `rowAuditInfo.ColumnChanges` is a list of `ColumnAuditInfo` instances that hold information about the audited columns (such as the SQL column name, old value, and new value).

The `RowAuditInfo` class instance is passed to the mapping callback function, along with your `MyCustomAuditInfo`. Below is an example of such a mapping function:

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

    // mapping can be customized, in various ways, here
    // we do that depending on the audited table
    if (auditInfo.TableModelType == typeof(MyTableModel1))
    {
        // lets generate json form the column changes (we are
        // skipping here details of how serialization is done)
        var changesJson = await JsonSerializeAsync(auditInfo.ColumnsChanges, cancellationToken);

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
    return null;
}
```

If this function returns null, the auditing of the current changes will be skipped, and no trail will be saved to the DB.

### Initiating audit trail

Finally, the above callback function will be executed whenever auditing is enabled, and `.SaveChanges()` is called:

```csharp
var customInfo = new MyCustomAuditInfo()
    {
        UserName = "Mr. Been",
        IpAddress = "15.15.15.15"
    };

await dbContext.SaveChangesAsync(customInfo, cancellationToken);
```

### Dealing with Enums (Value Mapping)

If your database contains columns that, for example, store the integer values of your enums, but you want your audit trail to contain the enum item names instead of their integer values, you can use Value Mapping. This makes it more convenient to present the audit trails to the end user.

Value mappings can be defined on a per-column basis and therefore can be set only when using the `AuditColumn()` function (and not the `AuditColumns()` function) during configuration.

Let's assume that `MyTableModel1` has the following column:

```csharp
public partial class MyTableModel1
{
    public int EmployeePosition {get; set;}

    ...
}
```

where employee positions are defined in the enum:

```csharp
public enum EmployeePositionEnum
{
    CEO,
    BoardMember,
    Manager,

    ...
}
```

So instead of having integer values in the audit trail whenever `EmployeePosition` is set or updated, we want to store the actual position name. To achieve this, we slightly alter the previous audit configuration for this table as follows:

```csharp
dbContext
    .ConfigureAuditTrail<MyAuditTrailModel>(MyAuditMappingCallBackAsync)
        .ConfigureTableAudit<MyTableModel1>()
        .AuditAllColumns(AutoExcludeColumnType.PrimaryKey | AutoExcludeColumnType.ForeignKey)
        .ExcludeColumns(
            tbl => tbl.Column3,
            tbl => tbl.Column4)
        // define a custom value mapper for EmployeePosition column
        .AuditColumn(tbl => tbl.EmployeePosition, EmployeePositionMappingCallBack)
```

where the value mapping function provides the database column value as a nullable object and expects a converted nullable object to be returned. In this example, it can be defined simply as:

```csharp
    public object? EmployeePositionMappingCallBack(object? value)
    {
        if(value is null)
        {
            return null;
        }
        // we convert the integer value to the corresponding enum item name
        return ((EmployeePositionEnum)(int)value).ToString();
    }
```

## Dependencies

- The main project is built using .Net 8, and has no external dependencies (Beside EF).
- Tests are done on an in-memory SQLite DB, and has some external dependencies.
