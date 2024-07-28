namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;

public enum AutoExcludeColumnType
{
    None = 0,

    PrimaryKey = 1,

    ForeignKey = 2,

    Virtual = 4,

    All = PrimaryKey | ForeignKey | Virtual,
}
