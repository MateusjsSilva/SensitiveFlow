using System.Collections.Concurrent;
using System.Reflection;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Exceptions;
using SensitiveFlow.Core.Reflection;
using SensitiveFlow.Retention.Contracts;

namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Evaluates retention metadata on an entity and invokes expiration handlers for expired fields.
/// </summary>
public sealed class RetentionEvaluator
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> NavigablePropertiesCache = new();

    private static readonly HashSet<Type> TerminalTypes =
    [
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
    ];

    private readonly IEnumerable<IRetentionExpirationHandler> _handlers;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="RetentionEvaluator"/>.
    /// </summary>
    public RetentionEvaluator(IEnumerable<IRetentionExpirationHandler> handlers)
        : this(handlers, TimeProvider.System) { }

    /// <summary>
    /// Initializes a new instance of <see cref="RetentionEvaluator"/> with a custom <see cref="TimeProvider"/>.
    /// </summary>
    public RetentionEvaluator(IEnumerable<IRetentionExpirationHandler> handlers, TimeProvider timeProvider)
    {
        _handlers = handlers;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
    /// <remarks>
    /// <para>
    /// <b>Fail-fast behavior:</b> When no handlers are registered, the first expired field
    /// throws <see cref="RetentionExpiredException"/> immediately — subsequent fields on the
    /// same entity are not evaluated. To collect all expired fields in one pass, register at
    /// least one handler (even a no-op collector) so the loop completes without throwing.
    /// </para>
    /// <para>
    /// <b>Nested objects:</b> Properties whose type is a reference type not in the terminal set
    /// (string, DateTime, Guid, etc.) are recursively traversed. This means a
    /// <c>[RetentionData]</c> attribute on <c>Customer.Address.PostalCode</c> is discovered
    /// automatically — you do not need to call the evaluator on <c>Address</c> separately.
    /// </para>
    /// </remarks>
    public async Task EvaluateAsync(object entity, DateTimeOffset referenceDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await EvaluateRecursiveAsync(entity, referenceDate, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateRecursiveAsync(object entity, DateTimeOffset referenceDate, CancellationToken cancellationToken)
    {
        var type = entity.GetType();
        var retentionProperties = SensitiveMemberCache.GetRetentionProperties(type);

        foreach (var pair in retentionProperties)
        {
            var expiration = pair.Attribute.GetExpirationDate(referenceDate);
            if (_timeProvider.GetUtcNow() <= expiration)
            {
                continue;
            }

            if (_handlers.Any())
            {
                foreach (var handler in _handlers)
                {
                    await handler.HandleAsync(entity, pair.Property.Name, expiration, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                throw new RetentionExpiredException(type.Name, pair.Property.Name, expiration);
            }
        }

        // Recurse into navigable properties (complex types that may carry their own [RetentionData]).
        foreach (var prop in GetNavigableProperties(type))
        {
            var value = prop.GetValue(entity);
            if (value is null)
            {
                continue;
            }

            await EvaluateRecursiveAsync(value, referenceDate, cancellationToken).ConfigureAwait(false);
        }
    }

    private static PropertyInfo[] GetNavigableProperties(Type type)
    {
        return NavigablePropertiesCache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(static p => p.CanRead
                 && !p.PropertyType.IsValueType
                 && !TerminalTypes.Contains(p.PropertyType)
                 && p.PropertyType != typeof(object)
                 && p.GetIndexParameters().Length == 0)
             .ToArray());
    }
}
