using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.Anonymization.Erasure;

/// <summary>
/// Default <see cref="IDataSubjectErasureService"/> implementation. Reuses the cached
/// reflection lookup from <see cref="SensitiveMemberCache"/> so a SaveChanges-time erasure
/// of many entities does not re-scan attributes.
/// </summary>
public sealed class DataSubjectErasureService : IDataSubjectErasureService
{
    private readonly IErasureStrategy _strategy;

    /// <summary>Initializes a new instance with the provided strategy.</summary>
    public DataSubjectErasureService(IErasureStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategy = strategy;
    }

    /// <inheritdoc />
    public int Erase(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var properties = SensitiveMemberCache.GetSensitiveProperties(entity.GetType());
        var count = 0;

        foreach (var property in properties)
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var newValue = _strategy.GetErasureValue(entity, property);
            property.SetValue(entity, newValue);
            count++;
        }

        return count;
    }
}
