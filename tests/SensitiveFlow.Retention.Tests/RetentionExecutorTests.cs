using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Retention.Services;

namespace SensitiveFlow.Retention.Tests;

public sealed class RetentionExecutorTests
{
    private sealed class AnonymizeEntity
    {
        public string Id { get; set; } = "u1";
        public DateTimeOffset CreatedAt { get; set; }

        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string Email { get; set; } = "alice@example.com";

        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public int Score { get; set; } = 42;
    }

    private sealed class DeleteEntity
    {
        public DateTimeOffset CreatedAt { get; set; }

        [RetentionData(Years = 1, Policy = RetentionPolicy.DeleteOnExpiration)]
        public string Email { get; set; } = "bob@example.com";
    }

    private sealed class FreshEntity
    {
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [RetentionData(Years = 5, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string Email { get; set; } = "carol@example.com";
    }

    private sealed class MixedPolicyEntity
    {
        public DateTimeOffset CreatedAt { get; set; }

        [RetentionData(Years = 1, Policy = RetentionPolicy.BlockOnExpiration)]
        public string BlockedField { get; set; } = "blocked";

        [RetentionData(Years = 1, Policy = RetentionPolicy.NotifyOwner)]
        public string NotifyField { get; set; } = "notify";
    }

    private sealed class ParentEntity
    {
        public DateTimeOffset CreatedAt { get; set; }

        public NestedEntity? MissingNested { get; set; }

        public NestedEntity Nested { get; set; } = new();
    }

    private sealed class NestedEntity
    {
        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string Secret { get; set; } = "nested-secret";
    }

    private sealed class ReadOnlyEntity
    {
        public DateTimeOffset CreatedAt { get; set; }

        [RetentionData(Years = 1, Policy = RetentionPolicy.AnonymizeOnExpiration)]
        public string Secret => "read-only";
    }

    private sealed class UnknownPolicyEntity
    {
        public DateTimeOffset CreatedAt { get; set; }

        [RetentionData(Years = 1, Policy = (RetentionPolicy)999)]
        public string Secret { get; set; } = "unknown";
    }

    [Fact]
    public async Task ExecuteAsync_AnonymizesExpiredString_WithMarker()
    {
        var entity = new AnonymizeEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync(
            [entity],
            referenceDateSelector: e => ((AnonymizeEntity)e).CreatedAt);

        entity.Email.Should().Be("[ANONYMIZED]");
        entity.Score.Should().Be(0);
        report.AnonymizedFieldCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_FlagsDeleteOnExpiration_WithoutMutating()
    {
        var entity = new DeleteEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync(
            [entity],
            referenceDateSelector: e => ((DeleteEntity)e).CreatedAt);

        entity.Email.Should().Be("bob@example.com");
        report.DeletePendingEntityCount.Should().Be(1);
        report.Entries.Should().ContainSingle(e => e.Action == RetentionAction.DeletePending);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNothing_WhenNotExpired()
    {
        var entity = new FreshEntity();
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync(
            [entity],
            referenceDateSelector: e => ((FreshEntity)e).CreatedAt);

        report.Entries.Should().BeEmpty();
        entity.Email.Should().Be("carol@example.com");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCustomMarker()
    {
        var entity = new AnonymizeEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor(new RetentionExecutorOptions { AnonymousStringMarker = "***" });

        await executor.ExecuteAsync(
            [entity],
            referenceDateSelector: e => ((AnonymizeEntity)e).CreatedAt);

        entity.Email.Should().Be("***");
    }

    [Fact]
    public async Task ExecuteAsync_NullCollection_Throws()
    {
        var executor = new RetentionExecutor();

        await executor.Invoking(e => e.ExecuteAsync(null!, _ => DateTimeOffset.UtcNow))
                      .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullSelector_Throws()
    {
        var executor = new RetentionExecutor();

        await executor.Invoking(e => e.ExecuteAsync([], null!))
                      .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNullEntities()
    {
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync([null!], _ => DateTimeOffset.UtcNow.AddYears(-2));

        report.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ReportsBlockAndNotifyPolicies()
    {
        var entity = new MixedPolicyEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync([entity], e => ((MixedPolicyEntity)e).CreatedAt);

        report.Entries.Should().Contain(e => e.FieldName == nameof(MixedPolicyEntity.BlockedField)
            && e.Action == RetentionAction.Blocked);
        report.Entries.Should().Contain(e => e.FieldName == nameof(MixedPolicyEntity.NotifyField)
            && e.Action == RetentionAction.NotifyPending);
    }

    [Fact]
    public async Task ExecuteAsync_RecursesIntoNestedEntities()
    {
        var entity = new ParentEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync([entity], e => ((ParentEntity)e).CreatedAt);

        entity.Nested.Secret.Should().Be("[ANONYMIZED]");
        report.Entries.Should().ContainSingle(e => e.Entity == entity.Nested
            && e.FieldName == nameof(NestedEntity.Secret)
            && e.Action == RetentionAction.Anonymized);
    }

    [Fact]
    public async Task ExecuteAsync_ReadOnlyExpiredProperty_ReportsNone()
    {
        var entity = new ReadOnlyEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync([entity], e => ((ReadOnlyEntity)e).CreatedAt);

        report.Entries.Should().ContainSingle(e => e.FieldName == nameof(ReadOnlyEntity.Secret)
            && e.Action == RetentionAction.None
            && e.ExpiredAt < DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCustomAnonymousValueFactory()
    {
        var entity = new AnonymizeEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor(new RetentionExecutorOptions
        {
            AnonymousValueFactory = (_, property) => property.PropertyType == typeof(string) ? "CUSTOM" : -1,
        });

        await executor.ExecuteAsync([entity], e => ((AnonymizeEntity)e).CreatedAt);

        entity.Email.Should().Be("CUSTOM");
        entity.Score.Should().Be(-1);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPolicy_ReportsNone()
    {
        var entity = new UnknownPolicyEntity { CreatedAt = DateTimeOffset.UtcNow.AddYears(-2) };
        var executor = new RetentionExecutor();

        var report = await executor.ExecuteAsync([entity], e => ((UnknownPolicyEntity)e).CreatedAt);

        report.Entries.Should().ContainSingle(e => e.FieldName == nameof(UnknownPolicyEntity.Secret)
            && e.Action == RetentionAction.None);
    }
}
