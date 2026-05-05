namespace LGPD.NET.Core.Enums;

public enum RetentionPolicy
{
    AnonymizeOnExpiry = 0,
    DeleteOnExpiry,
    BlockOnExpiry,
    NotifyOwner
}
