using System.Reflection;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Retention.Contracts;

namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Evaluates retention metadata on an entity and invokes expiration handlers for expired fields.
/// </summary>
public sealed class RetentionEvaluator
{
    private readonly IEnumerable<IRetentionExpirationHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of <see cref="RetentionEvaluator"/>.
    /// </summary>
    public RetentionEvaluator(IEnumerable<IRetentionExpirationHandler> handlers)
    {
        _handlers = handlers;
    }

    /// <summary>
    /// Inspects all properties of <paramref name="entity"/> decorated with <see cref="RetentionDataAttribute"/>
    /// and triggers registered handlers for any field whose retention period has expired relative to
    /// <paramref name="referenceDate"/>.
    /// </summary>
    /// <param name="entity">The entity to evaluate.</param>
    /// <param name="referenceDate">The date used as the retention start point (typically record creation date).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="RetentionExpiredException">
    /// Thrown when a field is expired and no handlers are registered.
    /// When handlers are registered, they receive the event instead.
    /// </exception>
    public async Task EvaluateAsync(object entity, DateTimeOffset referenceDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var attr = property.GetCustomAttribute<RetentionDataAttribute>();
            if (attr is null)
                continue;

            var expiration = attr.GetExpirationDate(referenceDate);
            if (DateTimeOffset.UtcNow <= expiration)
                continue;

            if (_handlers.Any())
            {
                foreach (var handler in _handlers)
                    await handler.HandleAsync(entity, property.Name, expiration, cancellationToken);
            }
            else
            {
                throw new RetentionExpiredException(entity.GetType().Name, property.Name, expiration);
            }
        }
    }
}
