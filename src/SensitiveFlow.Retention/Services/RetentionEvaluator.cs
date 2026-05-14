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
    /// Thrown when one or more fields are expired and no handlers are registered.
    /// When handlers are registered, they receive the event instead.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Collect-all behavior:</b> All expired fields are collected before throwing <see cref="RetentionExpiredException"/>.
    /// This allows batch validation of all expired fields in a single pass. If handlers are registered,
    /// all expired fields receive handler events before the method completes.
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
        var expiredFields = new List<(string TypeName, string PropertyName, DateTimeOffset ExpirationDate)>();
        await EvaluateRecursiveAsync(entity, referenceDate, expiredFields, cancellationToken).ConfigureAwait(false);

        if (expiredFields.Count > 0 && !_handlers.Any())
        {
            var first = expiredFields[0];
            throw new RetentionExpiredException(first.TypeName, first.PropertyName, first.ExpirationDate);
        }
    }

    private async Task EvaluateRecursiveAsync(
        object entity,
        DateTimeOffset referenceDate,
        List<(string TypeName, string PropertyName, DateTimeOffset ExpirationDate)> expiredFields,
        CancellationToken cancellationToken)
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

            expiredFields.Add((type.Name, pair.Property.Name, expiration));

            if (_handlers.Any())
            {
                foreach (var handler in _handlers)
                {
                    await handler.HandleAsync(entity, pair.Property.Name, expiration, cancellationToken)
                        .ConfigureAwait(false);
                }
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

            // Handle collections: iterate and evaluate each item
            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is not null)
                    {
                        await EvaluateRecursiveAsync(item, referenceDate, expiredFields, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await EvaluateRecursiveAsync(value, referenceDate, expiredFields, cancellationToken)
                    .ConfigureAwait(false);
            }
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
