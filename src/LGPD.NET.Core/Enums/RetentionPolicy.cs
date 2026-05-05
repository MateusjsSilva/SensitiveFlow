namespace LGPD.NET.Core.Enums;

public enum RetentionPolicy
{
    AnonymizeOnExpiration = 0,
    DeleteOnExpiration,
    BlockOnExpiration,
    NotifyOwner
}
