using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Diagnostics.Decorators;
using SensitiveFlow.Diagnostics.Extensions;

namespace SensitiveFlow.Diagnostics.Tests;

public sealed class InstrumentedAuditStoreTests
{
    private static AuditRecord SampleRecord() => new()
    {
        DataSubjectId = "subject-x",
        Entity = "Customer",
        Field = "Email",
        Operation = AuditOperation.Update,
        Timestamp = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task AppendAsync_StartsActivityAndIncrementsCounter()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == SensitiveFlowDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => activities.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        var measurements = new List<long>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SensitiveFlowDiagnostics.MeterName &&
                    instrument.Name == SensitiveFlowDiagnostics.AuditAppendCountName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        meterListener.Start();

        await store.AppendAsync(SampleRecord());

        activities.Should().ContainSingle(a => a.OperationName == "sensitiveflow.audit.append");
        measurements.Sum().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AppendAsync_RejectsNullRecord()
    {
        var store = new InstrumentedAuditStore(Substitute.For<IAuditStore>());

        var act = () => store.AppendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_SetsActivityStatusAndRethrows_WhenInnerFails()
    {
        var inner = Substitute.For<IAuditStore>();
        inner.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));
        var store = new InstrumentedAuditStore(inner);

        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == SensitiveFlowDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);

        var act = () => store.AppendAsync(SampleRecord());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
        stopped.Should().NotBeNull();
        stopped!.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task AppendRangeAsync_PrefersBatchInner()
    {
        var inner = Substitute.For<IBatchAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        var records = new[] { SampleRecord(), SampleRecord() };
        await store.AppendRangeAsync(records);

        await inner.Received(1).AppendRangeAsync(records, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_RejectsNullRecords()
    {
        var store = new InstrumentedAuditStore(Substitute.For<IAuditStore>());

        var act = () => store.AppendRangeAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendRangeAsync_ReturnsWithoutCallingInner_WhenRecordsAreEmpty()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        await store.AppendRangeAsync([]);

        await inner.DidNotReceive().AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_FallsBackToOneByOne_WhenInnerIsNotBatch()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        await store.AppendRangeAsync([SampleRecord(), SampleRecord()]);

        await inner.Received(2).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendRangeAsync_SetsActivityStatusAndRethrows_WhenInnerFails()
    {
        var inner = Substitute.For<IBatchAuditStore>();
        inner.AppendRangeAsync(Arg.Any<IReadOnlyCollection<AuditRecord>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("batch failed"));
        var store = new InstrumentedAuditStore(inner);

        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == SensitiveFlowDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);

        var act = () => store.AppendRangeAsync([SampleRecord()]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("batch failed");
        stopped.Should().NotBeNull();
        stopped!.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task QueryAsync_DelegatesWithoutInstrumentation()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        await store.QueryAsync();

        await inner.Received(1).QueryAsync(null, null, 0, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryByDataSubjectAsync_DelegatesWithoutInstrumentation()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        await store.QueryByDataSubjectAsync("subject-x");

        await inner.Received(1).QueryByDataSubjectAsync(
            "subject-x",
            null,
            null,
            0,
            100,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_RejectsNullInner()
    {
        var act = () => new InstrumentedAuditStore(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSensitiveFlowDiagnostics_ThrowsWhenAuditStoreIsMissing()
    {
        var services = new ServiceCollection();

        var act = () => services.AddSensitiveFlowDiagnostics();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No IAuditStore registration was found*");
    }

    [Fact]
    public void AddSensitiveFlowDiagnostics_WrapsImplementationTypeRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAuditStore, ConcreteAuditStore>();

        services.AddSensitiveFlowDiagnostics();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().BeOfType<InstrumentedAuditStore>();
        provider.GetRequiredService<ConcreteAuditStore>().Should().NotBeNull();
    }

    [Fact]
    public void AddSensitiveFlowDiagnostics_WrapsFactoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditStore>(_ => new ConcreteAuditStore());

        services.AddSensitiveFlowDiagnostics();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().BeOfType<InstrumentedAuditStore>();
    }

    [Fact]
    public void AddSensitiveFlowDiagnostics_WrapsInstanceRegistration()
    {
        var services = new ServiceCollection();
        var instance = new ConcreteAuditStore();
        services.AddSingleton<IAuditStore>(instance);

        services.AddSensitiveFlowDiagnostics();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAuditStore>().Should().BeOfType<InstrumentedAuditStore>();
    }

    private sealed class ConcreteAuditStore : IAuditStore
    {
        public Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<IReadOnlyList<AuditRecord>> QueryByDataSubjectAsync(
            string dataSubjectId,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);

        public Task<IReadOnlyList<AuditRecord>> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditRecord>>([]);
    }
}
