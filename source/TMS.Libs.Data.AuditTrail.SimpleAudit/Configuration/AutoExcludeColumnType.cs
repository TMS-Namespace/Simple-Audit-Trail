namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Configuration;

[Flags]
public enum AutoExcludeColumnType
{
    None = 0,

    PrimaryKey = 1 << 0,

    ForeignKey = 1 << 1,

    Virtual = 1 << 2,

    All = PrimaryKey | ForeignKey | Virtual,
}
