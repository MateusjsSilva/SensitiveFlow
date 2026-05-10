using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SensitiveFlow.Audit.Decorators;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Audit.Tests;

public sealed class RetryingAuditStoreTests
{
    private static AuditRecord SampleRecord() => new()
    {
        DataSubjectId = "subject",
        Entity        = "Entity",
        Field         = "Field",
        Operation     = AuditOperation.Access,
    };

    private static RetryingAuditStoreOptions FastOptions() => new()
    {
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromMilliseconds(1),
        MaxDelay = TimeSpan.FromMilliseconds(2),
    };

    [Fact]
    public async Task AppendAsync_TransientFailure_RetriesAndSucceeds()
    {
        var inner = Substitute.For<IAuditStore>();
        var calls = 0;
        inner.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                return calls < 2 ? Task.FromException(new IOException("transient")) : Task.CompletedTask;
            });

        var sut = new RetryingAuditStore(inner, FastOptions());

        await sut.AppendAsync(SampleRecord());

        calls.Should().Be(2);
    }

    [Fact]
    public async Task AppendAsync_ExhaustsAttempts_PropagatesException()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => new IOException("never recovers"));

        var sut = new RetryingAuditStore(inner, FastOptions());

        await sut.Invoking(s => s.AppendAsync(SampleRecord()))
            .Should().ThrowAsync<IOException>();

        await inner.Received(3).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendAsync_NonRetriableException_DoesNotRetry()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => new ArgumentException("bad input"));

        var sut = new RetryingAuditStore(inner, FastOptions());

        await sut.Invoking(s => s.AppendAsync(SampleRecord()))
            .Should().ThrowAsync<ArgumentException>();

        await inner.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_DelegatesToBatchStore_WhenAvailable()
    {
        var batch = Substitute.For<IBatchAuditStore>();
        var sut = new RetryingAuditStore(batch, FastOptions());
        var records = new[] { SampleRecord(), SampleRecord() };

        await sut.AppendRangeAsync(records);

        await batch.Received(1).AppendRangeAsync(records, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_AppendsOneByOne_WhenBatchStoreIsNotAvailable()
    {
        var inner = Substitute.For<IAuditStore>();
        var sut = new RetryingAuditStore(inner, FastOptions());
        var records = new[] { SampleRecord(), SampleRecord() };

        await sut.AppendRangeAsync(records);

        await inner.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_DoesNotRetry()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.QueryAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => new IOException("query failed"));

        var sut = new RetryingAuditStore(inner, FastOptions());

        await sut.Invoking(s => s.QueryAsync())
            .Should().ThrowAsync<IOException>();

        await inner.Received(1).QueryAsync(
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_DoesNotRetry()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.QueryByDataSubjectAsync("subject", Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => new IOException("query failed"));

        var sut = new RetryingAuditStore(inner, FastOptions());

        await sut.Invoking(s => s.QueryByDataSubjectAsync("subject"))
            .Should().ThrowAsync<IOException>();

        await inner.Received(1).QueryByDataSubjectAsync(
            "subject",
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendAsync_CancellationDuringRetry_DoesNotRetryFurther()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => new IOException("transient"));

        var sut = new RetryingAuditStore(inner, new RetryingAuditStoreOptions
        {
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromMilliseconds(50),
            MaxDelay = TimeSpan.FromMilliseconds(50),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await sut.Invoking(s => s.AppendAsync(SampleRecord(), cts.Token))
            .Should().ThrowAsync<Exception>();
    }
}
