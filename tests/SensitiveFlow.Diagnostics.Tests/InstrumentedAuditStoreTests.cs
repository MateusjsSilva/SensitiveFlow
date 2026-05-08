using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using NSubstitute;
using SensitiveFlow.Core.Diagnostics;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.Core.Models;
using SensitiveFlow.Diagnostics.Decorators;

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
    public async Task AppendRangeAsync_PrefersBatchInner()
    {
        var inner = Substitute.For<IBatchAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        var records = new[] { SampleRecord(), SampleRecord() };
        await store.AppendRangeAsync(records);

        await inner.Received(1).AppendRangeAsync(records, Arg.Any<CancellationToken>());
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
    public async Task QueryAsync_DelegatesWithoutInstrumentation()
    {
        var inner = Substitute.For<IAuditStore>();
        var store = new InstrumentedAuditStore(inner);

        await store.QueryAsync();

        await inner.Received(1).QueryAsync(null, null, 0, 100, Arg.Any<CancellationToken>());
    }
}
