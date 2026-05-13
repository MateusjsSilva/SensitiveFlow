using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Extensions;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Tests;

public sealed class AuditRecordDiffTests
{
    [Fact]
    public void ToDiff_ConvertsSingleRecordToReadableFormat()
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ActorId = "admin-1",
            Details = @"{
                ""before"": ""alice@old.com"",
                ""after"": ""alice@new.com"",
                ""sensitive"": true,
                ""category"": ""Contact""
            }"
        };

        var diff = record.ToDiff(maskSensitiveValues: false);

        diff.DataSubjectId.Should().Be("user-123");
        diff.Entity.Should().Be("Customer");
        diff.Operation.Should().Be(AuditOperation.Update);
        diff.Changes.Should().HaveCount(1);
        diff.Changes[0].FieldName.Should().Be("Email");
        diff.Changes[0].BeforeValue.Should().Be("alice@old.com");
        diff.Changes[0].AfterValue.Should().Be("alice@new.com");
    }

    [Fact]
    public void ToDiff_MasksValuesWhenRequested()
    {
        var record = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            Details = @"{
                ""before"": ""alice@old.com"",
                ""after"": ""alice@new.com"",
                ""sensitive"": true
            }"
        };

        var diff = record.ToDiff(maskSensitiveValues: true);

        diff.Changes[0].BeforeValue.Should().Be("[REDACTED]");
        diff.Changes[0].AfterValue.Should().Be("[REDACTED]");
    }

    [Fact]
    public void FieldChange_ToStringFormatsReadably()
    {
        var change = new FieldChange
        {
            FieldName = "Email",
            BeforeValue = "alice@old.com",
            AfterValue = "alice@new.com",
            WasSensitive = true
        };

        var str = change.ToString();

        str.Should().Be("Email: alice@old.com → alice@new.com");
    }

    [Fact]
    public void AuditRecordDiff_ToMultilineStringFormatsForReporting()
    {
        var diff = new AuditRecordDiff
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ActorId = "admin-1",
            Changes = new List<FieldChange>
            {
                new FieldChange
                {
                    FieldName = "Email",
                    BeforeValue = "alice@old.com",
                    AfterValue = "alice@new.com",
                    WasSensitive = true
                },
                new FieldChange
                {
                    FieldName = "Status",
                    BeforeValue = "Active",
                    AfterValue = "Inactive",
                    WasSensitive = false
                }
            }
        };

        var multiline = diff.ToMultilineString();

        multiline.Should().Contain("Operation: Update");
        multiline.Should().Contain("admin-1");
        multiline.Should().Contain("Email: alice@old.com → alice@new.com");
        multiline.Should().Contain("Status: Active → Inactive");
    }

    [Fact]
    public void AuditRecordDiff_ToCompactSummaryReturnsJsonLike()
    {
        var diff = new AuditRecordDiff
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Operation = AuditOperation.Update,
            Changes = new List<FieldChange>
            {
                new FieldChange { FieldName = "Email", WasSensitive = true },
                new FieldChange { FieldName = "Status", WasSensitive = false }
            }
        };

        var summary = diff.ToCompactSummary();

        summary.Should().Contain("entity:Customer");
        summary.Should().Contain("operation:Update");
        summary.Should().Contain("changes:2");
        summary.Should().Contain("sensitive:1");
    }

    [Fact]
    public void ToDiffAggregate_CombinesMultipleRecordsIntoOne()
    {
        var records = new List<AuditRecord>
        {
            new AuditRecord
            {
                Id = Guid.NewGuid(),
                DataSubjectId = "user-123",
                Entity = "Customer",
                Field = "Email",
                Operation = AuditOperation.Update,
                Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
                ActorId = "admin-1",
                Details = @"{ ""before"": ""alice@old.com"", ""after"": ""alice@new.com"", ""sensitive"": true }"
            },
            new AuditRecord
            {
                Id = Guid.NewGuid(),
                DataSubjectId = "user-123",
                Entity = "Customer",
                Field = "Name",
                Operation = AuditOperation.Update,
                Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
                ActorId = "admin-1",
                Details = @"{ ""before"": ""Alice Smith"", ""after"": ""Alice Johnson"", ""sensitive"": false }"
            }
        };

        var diff = records.ToDiffAggregate(maskSensitiveValues: false);

        diff.DataSubjectId.Should().Be("user-123");
        diff.Changes.Should().HaveCount(2);
        diff.Changes[0].FieldName.Should().Be("Email");
        diff.Changes[1].FieldName.Should().Be("Name");
    }

    [Fact]
    public void ToDiffAggregate_ThrowsOnMismatchedDataSubjects()
    {
        var records = new List<AuditRecord>
        {
            new AuditRecord { DataSubjectId = "user-123", Entity = "Customer", Field = "Email" },
            new AuditRecord { DataSubjectId = "user-456", Entity = "Customer", Field = "Email" }
        };

        var action = () => records.ToDiffAggregate();

        action.Should().Throw<ArgumentException>()
            .WithMessage("*different data subjects*");
    }

    [Fact]
    public void FromSnapshot_ConvertsMapsSnapshotToDiff()
    {
        var snapshot = new AuditSnapshot
        {
            DataSubjectId = "user-123",
            Aggregate = "Customer",
            AggregateId = "cust-123",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ActorId = "admin-1",
            BeforeJson = @"{ ""email"": ""alice@old.com"", ""status"": ""Active"" }",
            AfterJson = @"{ ""email"": ""alice@new.com"", ""status"": ""Inactive"" }"
        };

        var diff = AuditRecordDiffExtensions.FromSnapshot(snapshot, maskSensitiveValues: false);

        diff.DataSubjectId.Should().Be("user-123");
        diff.Entity.Should().Be("Customer");
        diff.Changes.Should().HaveCount(2);
        diff.Changes.Should().Satisfy(
            c => c.FieldName == "email" && c.BeforeValue == "alice@old.com",
            c => c.FieldName == "status" && c.BeforeValue == "Active"
        );
    }

    [Fact]
    public void FieldChange_HandlesNullValues()
    {
        var change = new FieldChange
        {
            FieldName = "OptionalField",
            BeforeValue = null,
            AfterValue = "newValue",
            WasSensitive = false
        };

        var str = change.ToString();

        str.Should().Be("OptionalField: (empty) → newValue");
    }
}
