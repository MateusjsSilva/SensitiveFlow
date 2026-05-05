namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Reversible pseudonymization. The data remains personal and all LGPD obligations apply (Art. 12, sec. 3).
/// </summary>
public interface IPseudonymizer
{
    string Pseudonymize(string value);
    string Revert(string token);
    bool CanPseudonymize(string value);
}
