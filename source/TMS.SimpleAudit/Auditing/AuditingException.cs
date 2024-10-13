namespace TMS.SimpleAudit.Auditing;

public class AuditingException : Exception
{
    #region Internal Constructors

    internal AuditingException(string message)
        : base(message)
        {  }

    internal AuditingException(Exception innerException)
        : base(innerException.Message, innerException)
    { }

    #endregion
}
