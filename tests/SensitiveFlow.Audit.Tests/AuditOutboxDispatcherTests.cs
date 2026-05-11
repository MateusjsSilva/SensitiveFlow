using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Audit.Outbox;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class AuditOutboxDispatcherTests
{
    [Fact]
    public async Task DispatchOnceAsync_WithNoOutbox_ReturnsImmediately()
    {
        var dispatcher = new AuditOutboxDispatcher(
            null,
            [Substitute.For<IAuditOutboxPublisher>()],
            new AuditOutboxDispatcherOptions());

        await dispatcher.DispatchOnceAsync();
    }

    [Fact]
    public async Task DispatchOnceAsync_WithNoPublishers_ReturnsImmediately()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [],
            new AuditOutboxDispatcherOptions());

        await dispatcher.DispatchOnceAsync();

        await outbox.DidNotReceive().DequeueBatchAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_DispatchesEntryToPublisher()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry();

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions { BatchSize = 100 });

        await dispatcher.DispatchOnceAsync();

        await publisher.Received(1).PublishAsync(entry, Arg.Any<CancellationToken>());
        await outbox.Received(1).MarkProcessedAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(entry.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_MarksFailed_WhenPublisherThrows()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry();

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);
        publisher.PublishAsync(entry, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions { BatchSize = 100 });

        await dispatcher.DispatchOnceAsync();

        await outbox.Received(1).MarkFailedAsync(
            entry.Id,
            "boom",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_DeadLetters_WhenMaxAttemptsReached()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry(attempts: 5);

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions
            {
                BatchSize = 100,
                MaxAttempts = 5,
                DeadLetterAfterMax = true,
            });

        await dispatcher.DispatchOnceAsync();

        await outbox.Received(1).MarkDeadLetteredAsync(
            entry.Id,
            "Max audit outbox attempts reached.",
            Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().PublishAsync(Arg.Any<AuditOutboxEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_DoesNotDeadLetter_WhenDeadLetterAfterMaxIsFalse()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry(attempts: 5);

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions
            {
                BatchSize = 100,
                MaxAttempts = 5,
                DeadLetterAfterMax = false,
            });

        await dispatcher.DispatchOnceAsync();

        await outbox.DidNotReceive().MarkDeadLetteredAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync(entry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_CallsMultiplePublishers()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var pub1 = Substitute.For<IAuditOutboxPublisher>();
        var pub2 = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry();

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [pub1, pub2],
            new AuditOutboxDispatcherOptions { BatchSize = 100 });

        await dispatcher.DispatchOnceAsync();

        await pub1.Received(1).PublishAsync(entry, Arg.Any<CancellationToken>());
        await pub2.Received(1).PublishAsync(entry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_StopsOnCancellation()
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        await dispatcher.StartAsync(cts.Token);

        // Should complete quickly without throwing
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await dispatcher.StopAsync(timeoutCts.Token);
    }

    [Theory]
    [InlineData(BackoffStrategy.None, 1, 0)]
    [InlineData(BackoffStrategy.Linear, 1, 100)]
    [InlineData(BackoffStrategy.Linear, 3, 300)]
    [InlineData(BackoffStrategy.Exponential, 1, 200)]
    [InlineData(BackoffStrategy.Exponential, 3, 800)]
    public async Task DispatchOnceAsync_AppliesBackoffDelay(BackoffStrategy strategy, int attempts, int expectedDelayMs)
    {
        var outbox = Substitute.For<IDurableAuditOutbox>();
        var publisher = Substitute.For<IAuditOutboxPublisher>();
        var entry = SampleEntry(attempts);

        outbox.DequeueBatchAsync(100, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var dispatcher = new AuditOutboxDispatcher(
            outbox,
            [publisher],
            new AuditOutboxDispatcherOptions
            {
                BatchSize = 100,
                Backoff = strategy,
            });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await dispatcher.DispatchOnceAsync();
        sw.Stop();

        if (expectedDelayMs == 0)
        {
            sw.ElapsedMilliseconds.Should().BeLessThan(50);
        }
        else
        {
            sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(expectedDelayMs - 50);
        }
    }

    private static AuditOutboxEntry SampleEntry(int attempts = 0) => new()
    {
        Id = Guid.NewGuid(),
        Record = new AuditRecord
        {
            DataSubjectId = "subject-1",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
        },
        Attempts = attempts,
    };
}
