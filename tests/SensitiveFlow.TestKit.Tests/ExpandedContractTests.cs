using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Retention.Contracts;
using SensitiveFlow.TestKit;

namespace SensitiveFlow.TestKit.Tests;

public sealed class InMemoryAuditSnapshotStoreContractTests : AuditSnapshotStoreContractTests
{
    protected override Task<IAuditSnapshotStore> CreateStoreAsync()
    {
        return Task.FromResult<IAuditSnapshotStore>(new InMemoryAuditSnapshotStore());
    }
}

public sealed class ReversiblePseudonymizerContractTests : PseudonymizerContractTests
{
    protected override Task<IPseudonymizer> CreatePseudonymizerAsync()
    {
        return Task.FromResult<IPseudonymizer>(new ReversiblePseudonymizer());
    }
}

public sealed class SimpleMaskerContractTests : MaskerContractTests
{
    protected override IMasker CreateMasker()
    {
        return new SimpleMasker();
    }
}

public sealed class SimpleAnonymizerContractTests : AnonymizerContractTests
{
    protected override IAnonymizer CreateAnonymizer()
    {
        return new SimpleAnonymizer();
    }
}

public sealed class NoopRetentionExpirationHandlerContractTests : RetentionExpirationHandlerContractTests
{
    protected override IRetentionExpirationHandler CreateHandler()
    {
        return new NoopRetentionExpirationHandler();
    }
}

internal sealed class InMemoryAuditSnapshotStore : IAuditSnapshotStore
{
    private readonly List<AuditSnapshot> _snapshots = [];

    public Task AppendAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditSnapshot>> QueryByAggregateAsync(
        string aggregate,
        string aggregateId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuditSnapshot>>(_snapshots
            .Where(s => s.Aggregate == aggregate && s.AggregateId == aggregateId)
            .Skip(skip)
            .Take(take)
            .ToArray());
    }

    public Task<IReadOnlyList<AuditSnapshot>> QueryByDataSubjectAsync(
        string dataSubjectId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuditSnapshot>>(_snapshots
            .Where(s => s.DataSubjectId == dataSubjectId)
            .Skip(skip)
            .Take(take)
            .ToArray());
    }
}

internal sealed class ReversiblePseudonymizer : IPseudonymizer
{
    public string Pseudonymize(string value) => "token:" + value;

    public Task<string> PseudonymizeAsync(string value, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Pseudonymize(value));
    }

    public string Reverse(string token) => token["token:".Length..];

    public Task<string> ReverseAsync(string token, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Reverse(token));
    }

    public bool CanPseudonymize(string value) => !string.IsNullOrEmpty(value);
}

internal sealed class SimpleMasker : IMasker
{
    public string Mask(string value) => "***";

    public bool CanMask(string value) => !string.IsNullOrEmpty(value);
}

internal sealed class SimpleAnonymizer : IAnonymizer
{
    public string Anonymize(string value) => "[ANONYMIZED]";

    public bool CanAnonymize(string value) => !string.IsNullOrEmpty(value);
}

internal sealed class NoopRetentionExpirationHandler : IRetentionExpirationHandler
{
    public Task HandleAsync(object entity, string fieldName, DateTimeOffset expiredAt, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
