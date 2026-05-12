using System.Collections.Concurrent;
using System.Reflection;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Reflection;

namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Applies retention policies to entities whose <see cref="RetentionDataAttribute"/>-decorated
/// fields have expired. Unlike <see cref="RetentionEvaluator"/>, which only reports expiration
/// via handlers, the executor mutates the entity in place for <c>AnonymizeOnExpiration</c> and
/// returns a structured report for the remaining policies.
/// </summary>
/// <remarks>
/// <para>
/// The executor never deletes rows from a database itself — that requires persistence-layer
/// knowledge it intentionally does not have. Callers should iterate
/// <see cref="RetentionExecutionReport.Entries"/> for <see cref="RetentionAction.DeletePending"/>
/// and remove those entities from their unit of work before saving.
/// </para>
/// <para>
/// <b>Nested objects:</b> Properties whose type is a reference type not in the terminal set
/// (string, DateTime, Guid, etc.) are recursively traversed, matching the behavior of
/// <see cref="RetentionEvaluator"/>.
/// </para>
/// </remarks>
public sealed class RetentionExecutor
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

    private readonly RetentionExecutorOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance with default options and system clock.</summary>
    public RetentionExecutor() : this(new RetentionExecutorOptions(), TimeProvider.System) { }

    /// <summary>Initializes a new instance.</summary>
    public RetentionExecutor(RetentionExecutorOptions options)
        : this(options, TimeProvider.System) { }

    /// <summary>Initializes a new instance with a custom <see cref="TimeProvider"/>.</summary>
    public RetentionExecutor(RetentionExecutorOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Walks <paramref name="entities"/>, evaluates each retention-annotated field against the
    /// reference date returned by <paramref name="referenceDateSelector"/>, and applies the
    /// declared <see cref="RetentionPolicy"/>.
    /// </summary>
    /// <param name="entities">Entities to evaluate.</param>
    /// <param name="referenceDateSelector">Returns the retention start date for each entity (typically the record's creation timestamp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An aggregated report describing actions taken or required.</returns>
    public Task<RetentionExecutionReport> ExecuteAsync(
        IEnumerable<object> entities,
        Func<object, DateTimeOffset> referenceDateSelector,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(entities, referenceDateSelector, mutate: true, cancellationToken);
    }

    /// <summary>
    /// Evaluates retention policies and returns the actions that would be taken without mutating entities.
    /// </summary>
    /// <param name="entities">Entities to evaluate.</param>
    /// <param name="referenceDateSelector">Returns the retention start date for each entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An aggregated report describing pending actions.</returns>
    public Task<RetentionExecutionReport> DryRunAsync(
        IEnumerable<object> entities,
        Func<object, DateTimeOffset> referenceDateSelector,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(entities, referenceDateSelector, mutate: false, cancellationToken);
    }

    private Task<RetentionExecutionReport> ExecuteCoreAsync(
        IEnumerable<object> entities,
        Func<object, DateTimeOffset> referenceDateSelector,
        bool mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(referenceDateSelector);

        var report = new RetentionExecutionReport();
        var now = _timeProvider.GetUtcNow();

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity is null)
            {
                continue;
            }

            var referenceDate = referenceDateSelector(entity);
            ProcessEntityRecursive(entity, referenceDate, now, report, mutate);
        }

        return Task.FromResult(report);
    }

    private void ProcessEntityRecursive(
        object entity,
        DateTimeOffset referenceDate,
        DateTimeOffset now,
        RetentionExecutionReport report,
        bool mutate)
    {
        var retentionProperties = SensitiveMemberCache.GetRetentionProperties(entity.GetType());

        foreach (var pair in retentionProperties)
        {
            var expiration = pair.Attribute.GetExpirationDate(referenceDate);
            if (now <= expiration)
            {
                continue;
            }

            var action = pair.Attribute.Policy switch
            {
                RetentionPolicy.AnonymizeOnExpiration => mutate ? Anonymize(entity, pair.Property) : RetentionAction.Anonymized,
                RetentionPolicy.DeleteOnExpiration => RetentionAction.DeletePending,
                RetentionPolicy.BlockOnExpiration => RetentionAction.Blocked,
                RetentionPolicy.NotifyOwner => RetentionAction.NotifyPending,
                _ => RetentionAction.None,
            };

            report.Add(new RetentionExecutionEntry(entity, pair.Property.Name, expiration, action));
        }

        // Recurse into navigable properties.
        foreach (var prop in GetNavigableProperties(entity.GetType()))
        {
            var value = prop.GetValue(entity);
            if (value is null)
            {
                continue;
            }

            ProcessEntityRecursive(value, referenceDate, now, report, mutate);
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

    private RetentionAction Anonymize(object entity, System.Reflection.PropertyInfo property)
    {
        if (!property.CanWrite)
        {
            return RetentionAction.None;
        }

        var value = _options.AnonymousValueFactory(entity, property);
        property.SetValue(entity, value);
        return RetentionAction.Anonymized;
    }
}
