using FluentAssertions;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Integrity;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Tests;

public sealed class AuditRecordIntegrityTests
{
    [Fact]
    public void ComputeRecordHash_ComputesDeterministicHash()
    {
        var record = new AuditRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ActorId = "admin-1",
        };

        var hash1 = AuditRecordIntegrityHelper.ComputeRecordHash(record);
        var hash2 = AuditRecordIntegrityHelper.ComputeRecordHash(record);

        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
        hash1.Should().Contain("="); // Base64
    }

    [Fact]
    public void ComputeRecordHash_ReturnsNullForNullInput()
    {
        var hash = AuditRecordIntegrityHelper.ComputeRecordHash(null);

        hash.Should().BeNull();
    }

    [Fact]
    public void ComputeRecordHash_ProducesDifferentHashesForDifferentRecords()
    {
        var record1 = new AuditRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };

        var record2 = new AuditRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            DataSubjectId = "user-124",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };

        var hash1 = AuditRecordIntegrityHelper.ComputeRecordHash(record1);
        var hash2 = AuditRecordIntegrityHelper.ComputeRecordHash(record2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyRecordHash_SucceedsWithMatchingHash()
    {
        var record = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var hash = AuditRecordIntegrityHelper.ComputeRecordHash(record);
        var recordWithHash = record with { CurrentRecordHash = hash };

        var isValid = AuditRecordIntegrityHelper.VerifyRecordHash(recordWithHash);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyRecordHash_FailsWithTamperedHash()
    {
        var record = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = DateTimeOffset.UtcNow,
            CurrentRecordHash = "tamperedHash123=="
        };

        var isValid = AuditRecordIntegrityHelper.VerifyRecordHash(record);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyHashLink_SucceedsWithMatchingLink()
    {
        var record1 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };

        var hash1 = AuditRecordIntegrityHelper.ComputeRecordHash(record1);
        var record1WithHash = record1 with { CurrentRecordHash = hash1 };

        var record2 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Name",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 16, 10, 30, 0, TimeSpan.Zero),
            PreviousRecordHash = hash1
        };

        var isLinked = AuditRecordIntegrityHelper.VerifyHashLink(record2, record1WithHash);

        isLinked.Should().BeTrue();
    }

    [Fact]
    public void VerifyHashLink_FailsWithBrokenLink()
    {
        var record1 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CurrentRecordHash = "hash1"
        };

        var record2 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Name",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 16, 10, 30, 0, TimeSpan.Zero),
            PreviousRecordHash = "differentHash"
        };

        var isLinked = AuditRecordIntegrityHelper.VerifyHashLink(record2, record1);

        isLinked.Should().BeFalse();
    }

    [Fact]
    public void VerifyAuditChain_ValidatesCompleteChain()
    {
        var record1 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };
        var hash1 = AuditRecordIntegrityHelper.ComputeRecordHash(record1);

        var record2 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Name",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 16, 10, 30, 0, TimeSpan.Zero),
            PreviousRecordHash = hash1
        };
        var hash2 = AuditRecordIntegrityHelper.ComputeRecordHash(record2);

        var records = new List<AuditRecord>
        {
            record1 with { CurrentRecordHash = hash1 },
            record2 with { CurrentRecordHash = hash2, PreviousRecordHash = hash1 }
        };

        var (isValid, brokenAt) = AuditRecordIntegrityHelper.VerifyAuditChain(records);

        isValid.Should().BeTrue();
        brokenAt.Should().Be(-1);
    }

    [Fact]
    public void DetectChainGaps_FindsMissingRecord()
    {
        var record1 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CurrentRecordHash = "hash1"
        };

        var record2 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Name",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 16, 10, 30, 0, TimeSpan.Zero),
            PreviousRecordHash = "missingHash",  // Different from record1's CurrentRecordHash
            CurrentRecordHash = "hash2"
        };

        var records = new List<AuditRecord> { record1, record2 };
        var gaps = AuditRecordIntegrityHelper.DetectChainGaps(records);

        gaps.Should().Contain(1);
    }

    [Fact]
    public void DetectChainGaps_FindsNoGapsInValidChain()
    {
        var record1 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Email",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            CurrentRecordHash = "hash1"
        };

        var record2 = new AuditRecord
        {
            DataSubjectId = "user-123",
            Entity = "Customer",
            Field = "Name",
            Operation = AuditOperation.Update,
            Timestamp = new DateTimeOffset(2026, 1, 16, 10, 30, 0, TimeSpan.Zero),
            PreviousRecordHash = "hash1",
            CurrentRecordHash = "hash2"
        };

        var records = new List<AuditRecord> { record1, record2 };
        var gaps = AuditRecordIntegrityHelper.DetectChainGaps(records);

        gaps.Should().BeEmpty();
    }
}
