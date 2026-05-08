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
}
