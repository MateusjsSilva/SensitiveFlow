namespace LGPD.NET.Core.Enums;

public enum AuditOperation
{
    Access = 0,
    Create,
    Update,
    Delete,
    Anonymize,
    Pseudonymize,
    Export,
    Share,
    Revoke
}
