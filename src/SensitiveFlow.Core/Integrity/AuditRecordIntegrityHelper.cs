using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Integrity;

/// <summary>
/// Utility for computing and verifying SHA-256 hashes of audit records to detect tampering.
/// </summary>
/// <remarks>
/// <para>
/// This helper enables implementation of hash-linked audit chains where each record
/// contains a hash of the previous record. By walking the chain and recomputing hashes,
/// callers can detect:
/// <list type="bullet">
///   <item><description>Deleted records (missing link)</description></item>
///   <item><description>Modified records (hash mismatch)</description></item>
///   <item><description>Reordered records (hash chain broken)</description></item>
/// </list>
/// </para>
/// <para>
/// The hashing algorithm is deterministic and includes only immutable fields of the record.
/// </para>
/// </remarks>
public static class AuditRecordIntegrityHelper
{
    /// <summary>
    /// Computes the SHA-256 hash of an audit record's immutable fields.
    /// </summary>
    /// <param name="record">The audit record to hash.</param>
    /// <returns>
    /// Base64-encoded SHA-256 hash of the record, or <c>null</c> if the record is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The hash includes only fields that should be immutable after persistence:
    /// <see cref="AuditRecord.Id"/>, <see cref="AuditRecord.DataSubjectId"/>,
    /// <see cref="AuditRecord.Entity"/>, <see cref="AuditRecord.Field"/>,
    /// <see cref="AuditRecord.Operation"/>, and <see cref="AuditRecord.Timestamp"/>.
    /// </para>
    /// <para>
    /// Mutable fields like <see cref="AuditRecord.Details"/> are excluded to allow
    /// audit systems to append enrichment data without invalidating the hash chain.
    /// </para>
    /// </remarks>
    public static string? ComputeRecordHash(AuditRecord? record)
    {
        if (record is null)
        {
            return null;
        }

        var payload = new
        {
            record.Id,
            record.DataSubjectId,
            record.Entity,
            record.Field,
            Operation = record.Operation.ToString(),
            Timestamp = record.Timestamp.UtcDateTime.Ticks
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies that a record's <see cref="AuditRecord.CurrentRecordHash"/> matches
    /// the computed hash of its immutable fields.
    /// </summary>
    /// <param name="record">The audit record to verify.</param>
    /// <returns>
    /// <c>true</c> if the record's stored hash matches the computed hash, or if either is <c>null</c>;
    /// <c>false</c> if the hashes do not match (indicating tampering or corruption).
    /// </returns>
    public static bool VerifyRecordHash(AuditRecord record)
    {
        if (record.CurrentRecordHash is null)
        {
            return true; // Cannot verify if hash not present
        }

        var computedHash = ComputeRecordHash(record);
        return computedHash == record.CurrentRecordHash;
    }

    /// <summary>
    /// Verifies that the <see cref="AuditRecord.PreviousRecordHash"/> of a record
    /// matches the <see cref="AuditRecord.CurrentRecordHash"/> of the previous record.
    /// </summary>
    /// <param name="currentRecord">The current audit record.</param>
    /// <param name="previousRecord">The previous audit record in the chain.</param>
    /// <returns>
    /// <c>true</c> if the hash link is valid or if either hash is <c>null</c>;
    /// <c>false</c> if the hashes do not match (indicating a broken or tampered chain).
    /// </returns>
    public static bool VerifyHashLink(AuditRecord currentRecord, AuditRecord previousRecord)
    {
        if (currentRecord.PreviousRecordHash is null || previousRecord.CurrentRecordHash is null)
        {
            return true; // Cannot verify if hashes not present
        }

        return currentRecord.PreviousRecordHash == previousRecord.CurrentRecordHash;
    }

    /// <summary>
    /// Verifies the integrity of a chain of audit records by walking forward and checking
    /// that each record's <see cref="AuditRecord.PreviousRecordHash"/> matches the previous
    /// record's <see cref="AuditRecord.CurrentRecordHash"/>.
    /// </summary>
    /// <param name="records">
    /// The audit records in chronological order. Must be sorted by <see cref="AuditRecord.Timestamp"/>.
    /// </param>
    /// <param name="strictHash">
    /// If <c>true</c>, also verifies that each record's own hash is correct.
    /// If <c>false</c>, only checks the links between records.
    /// Defaults to <c>true</c>.
    /// </param>
    /// <returns>
    /// A tuple of (IsValid, BrokenAtIndex). If IsValid is <c>true</c>, the entire chain is valid.
    /// If <c>false</c>, BrokenAtIndex indicates the first record where verification failed (or -1 if unknown).
    /// </returns>
    public static (bool IsValid, int BrokenAtIndex) VerifyAuditChain(
        IReadOnlyList<AuditRecord> records,
        bool strictHash = true)
    {
        if (records.Count == 0)
        {
            return (true, -1);
        }

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];

            // Verify the record's own hash
            if (strictHash && !VerifyRecordHash(record))
            {
                return (false, i);
            }

            // Verify the link to the previous record
            if (i > 0)
            {
                var previousRecord = records[i - 1];
                if (!VerifyHashLink(record, previousRecord))
                {
                    return (false, i);
                }
            }
        }

        return (true, -1);
    }

    /// <summary>
    /// Detects gaps in an audit chain by checking if records form a continuous sequence.
    /// A gap occurs when a record's <see cref="AuditRecord.PreviousRecordHash"/> does not
    /// match the previous record's <see cref="AuditRecord.CurrentRecordHash"/>.
    /// </summary>
    /// <param name="records">
    /// The audit records, ideally sorted by <see cref="AuditRecord.Timestamp"/>.
    /// </param>
    /// <returns>
    /// A list of indices where gaps were detected. Empty if the chain is continuous.
    /// </returns>
    public static IReadOnlyList<int> DetectChainGaps(IReadOnlyList<AuditRecord> records)
    {
        var gaps = new List<int>();

        if (records.Count <= 1)
        {
            return gaps;
        }

        for (int i = 1; i < records.Count; i++)
        {
            var current = records[i];
            var previous = records[i - 1];

            if (current.PreviousRecordHash is not null &&
                previous.CurrentRecordHash is not null &&
                current.PreviousRecordHash != previous.CurrentRecordHash)
            {
                gaps.Add(i);
            }
        }

        return gaps;
    }
}
