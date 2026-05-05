namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Irreversible anonymization. The resulting data is no longer personal data (Art. 12 of the LGPD).
/// </summary>
public interface IAnonymizer
{
    string Anonymize(string value);
    bool CanAnonymize(string value);
}
