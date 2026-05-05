namespace LGPD.NET.Core.Interfaces;

/// <summary>
/// Record of processing operations under Art. 37 of the LGPD.
/// </summary>
public interface IProcessingInventory
{
    string Id { get; }
    string Description { get; }
    DateTimeOffset CreatedAt { get; }
}
