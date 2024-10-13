namespace TMS.SimpleAudit.Tests.Help.Tools;

public record CustomAuditInfo
{
    public required string UserName { get; set; }

    public required string IpAddress { get; set; }
}
