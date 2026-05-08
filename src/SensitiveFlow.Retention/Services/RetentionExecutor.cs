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
/// </remarks>
public sealed class RetentionExecutor
{
    private readonly RetentionExecutorOptions _options;

    /// <summary>Initializes a new instance with default options.</summary>
    public RetentionExecutor() : this(new RetentionExecutorOptions()) { }

    /// <summary>Initializes a new instance.</summary>
    public RetentionExecutor(RetentionExecutorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
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
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(referenceDateSelector);

        var report = new RetentionExecutionReport();
        var now = DateTimeOffset.UtcNow;

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entity is null)
            {
                continue;
            }

            var referenceDate = referenceDateSelector(entity);
            ProcessEntity(entity, referenceDate, now, report);
        }

        return Task.FromResult(report);
    }

    private void ProcessEntity(object entity, DateTimeOffset referenceDate, DateTimeOffset now, RetentionExecutionReport report)
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
                RetentionPolicy.AnonymizeOnExpiration => Anonymize(entity, pair.Property),
                RetentionPolicy.DeleteOnExpiration => RetentionAction.DeletePending,
                RetentionPolicy.BlockOnExpiration => RetentionAction.Blocked,
                RetentionPolicy.NotifyOwner => RetentionAction.NotifyPending,
                _ => RetentionAction.None,
            };

            report.Add(new RetentionExecutionEntry(entity, pair.Property.Name, expiration, action));
        }
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
