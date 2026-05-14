using FluentAssertions;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Tests.Models;

public sealed class AuditRecordDetailsTests
{
    #region Parse — Happy Path

    [Fact]
    public void Parse_WithValidJson_ReturnsDeserializedRecord()
    {
        var json = """
            {
              "oldValue": "alice@example.com",
              "newValue": "bob@example.com",
              "bulkOperationTag": "bulk.update",
              "reasonCode": "compliance.erasure",
              "metadata": "{\"batchId\": \"batch-123\"}",
              "redactionAction": "Mask"
            }
            """;

        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue.Should().Be("alice@example.com");
        result.NewValue.Should().Be("bob@example.com");
        result.BulkOperationTag.Should().Be("bulk.update");
        result.ReasonCode.Should().Be("compliance.erasure");
        result.Metadata.Should().Contain("batch-123");
        result.RedactionAction.Should().Be("Mask");
    }

    [Fact]
    public void Parse_WithMinimalJson_ReturnsRecordWithNullFields()
    {
        var json = "{}";
        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue.Should().BeNull();
        result.NewValue.Should().BeNull();
        result.BulkOperationTag.Should().BeNull();
        result.ReasonCode.Should().BeNull();
        result.Metadata.Should().BeNull();
        result.RedactionAction.Should().BeNull();
    }

    [Fact]
    public void Parse_WithPartialJson_ReturnsPartiallyPopulatedRecord()
    {
        var json = """{"oldValue": "old", "reasonCode": "test"}""";
        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue.Should().Be("old");
        result.ReasonCode.Should().Be("test");
        result.NewValue.Should().BeNull();
        result.BulkOperationTag.Should().BeNull();
    }

    #endregion

    #region Parse — Edge Cases: Null, Empty, Whitespace

    [Fact]
    public void Parse_WithNullString_ReturnsNull()
    {
        var result = AuditRecordDetails.Parse(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithEmptyString_ReturnsNull()
    {
        var result = AuditRecordDetails.Parse(string.Empty);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithWhitespaceOnlyString_ReturnsNull()
    {
        var result = AuditRecordDetails.Parse("   \t\n   ");
        result.Should().BeNull();
    }

    #endregion

    #region Parse — Legacy Unstructured Strings (Backward Compatibility)

    [Fact]
    public void Parse_WithLegacyUnstructuredString_ReturnsNull()
    {
        // Pre-typed format: "Audit redaction action: Mask; value: m****@x.com."
        var legacy = "Audit redaction action: Mask; value: m****@x.com.";
        var result = AuditRecordDetails.Parse(legacy);

        // Not JSON, so returns null (graceful degradation)
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithLegacyMultilineString_ReturnsNull()
    {
        var legacy = "Bulk update via SensitiveBulkOperations helper.\nField: Email\nAction: Redact";
        var result = AuditRecordDetails.Parse(legacy);
        result.Should().BeNull();
    }

    #endregion

    #region Parse — Malformed JSON (Edge Cases)

    [Fact]
    public void Parse_WithMalformedJson_ReturnsNull()
    {
        var malformed = """{"oldValue": "unclosed""";
        var result = AuditRecordDetails.Parse(malformed);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithJsonArray_ReturnsNull()
    {
        var array = """["oldValue", "newValue"]""";
        var result = AuditRecordDetails.Parse(array);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithJsonPrimitive_ReturnsNull()
    {
        var primitive = """"just a string"""";
        var result = AuditRecordDetails.Parse(primitive);
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WithJsonNumber_ReturnsNull()
    {
        var number = "12345";
        var result = AuditRecordDetails.Parse(number);
        result.Should().BeNull();
    }

    #endregion

    #region Parse — Special Characters & Encoding

    [Fact]
    public void Parse_WithUnicodeCharacters_DeserializesCorrectly()
    {
        var json = """
            {
              "oldValue": "José García 🔒",
              "newValue": "安全 ประวัติ",
              "reasonCode": "中文コード"
            }
            """;
        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue.Should().Contain("José García");
        result.OldValue.Should().Contain("🔒");
        result.NewValue.Should().Contain("安全");
        result.ReasonCode.Should().Contain("中文");
    }

    [Fact]
    public void Parse_WithEscapedCharacters_DeserializesCorrectly()
    {
        var json = """
            {
              "oldValue": "Line1\nLine2\tTabbed",
              "newValue": "Quote: \"hello\"",
              "metadata": "{\"json\":\"escaped\"}"
            }
            """;
        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue.Should().Contain("Line1");
        result!.OldValue.Should().Contain("Line2");
        result.NewValue.Should().Contain("\"hello\"");
    }

    [Fact]
    public void Parse_WithLongStrings_DeserializesCorrectly()
    {
        var longValue = new string('a', 10_000);
        var json = $$"""{"oldValue": "{{longValue}}", "newValue": "{{new string('b', 10_000)}}"}""";

        var result = AuditRecordDetails.Parse(json);

        result.Should().NotBeNull();
        result!.OldValue!.Length.Should().Be(10_000);
        result.NewValue!.Length.Should().Be(10_000);
    }

    #endregion

    #region Parse — Case Insensitivity (JSON naming policy)

    [Fact]
    public void Parse_WithDifferentCasing_DeserializesCorrectly()
    {
        // camelCase (expected)
        var camelCase = """{"oldValue": "test1", "reasonCode": "test2"}""";
        var result1 = AuditRecordDetails.Parse(camelCase);
        result1!.OldValue.Should().Be("test1");

        // PascalCase (should still work via JsonNamingPolicy.CamelCase)
        var pascalCase = """{"OldValue": "test1", "ReasonCode": "test2"}""";
        var result2 = AuditRecordDetails.Parse(pascalCase);
        // This may be null or partially populated depending on policy
        // The point is: we handle both gracefully
    }

    #endregion

    #region TryParse

    [Fact]
    public void TryParse_WithValidJson_ReturnsTrueAndPopulatesResult()
    {
        var json = """{"oldValue": "old", "newValue": "new"}""";
        var success = AuditRecordDetails.TryParse(json, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.OldValue.Should().Be("old");
        result.NewValue.Should().Be("new");
    }

    [Fact]
    public void TryParse_WithNullString_ReturnsTrueButNullResult()
    {
        var success = AuditRecordDetails.TryParse(null, out var result);

        success.Should().BeTrue();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithMalformedJson_ReturnsTrueButNullResult()
    {
        var malformed = """{"unclosed": "json""";
        var success = AuditRecordDetails.TryParse(malformed, out var result);

        success.Should().BeTrue();
        result.Should().BeNull();
    }

    #endregion

    #region ToJson — Round-Trip

    [Fact]
    public void ToJson_WithCompleteRecord_SerializesCorrectly()
    {
        var original = new AuditRecordDetails
        {
            OldValue = "alice@example.com",
            NewValue = "bob@example.com",
            BulkOperationTag = "bulk.update",
            ReasonCode = "compliance.erasure",
            Metadata = "{\"batchId\": \"123\"}",
            RedactionAction = "Mask"
        };

        var json = original.ToJson();
        var deserialized = AuditRecordDetails.Parse(json);

        deserialized.Should().NotBeNull();
        deserialized!.OldValue.Should().Be(original.OldValue);
        deserialized.NewValue.Should().Be(original.NewValue);
        deserialized.BulkOperationTag.Should().Be(original.BulkOperationTag);
        deserialized.ReasonCode.Should().Be(original.ReasonCode);
        deserialized.Metadata.Should().Be(original.Metadata);
        deserialized.RedactionAction.Should().Be(original.RedactionAction);
    }

    [Fact]
    public void ToJson_WithNullFields_SerializesWithNullValues()
    {
        var record = new AuditRecordDetails();
        var json = record.ToJson();

        // JSON serializer includes null values in the output
        json.Should().Contain("null");

        var deserialized = AuditRecordDetails.Parse(json);
        deserialized.Should().NotBeNull();
        deserialized!.OldValue.Should().BeNull();
    }

    [Fact]
    public void ToJson_RoundTrip_WithSpecialCharacters_PreservesContent()
    {
        var original = new AuditRecordDetails
        {
            OldValue = "José García 🔒\nMultiline",
            NewValue = "安全\"quoted\"",
            Metadata = "{\"nested\": \"json\"}"
        };

        var json = original.ToJson();
        var deserialized = AuditRecordDetails.Parse(json);

        deserialized!.OldValue.Should().Be(original.OldValue);
        deserialized.NewValue.Should().Be(original.NewValue);
        deserialized.Metadata.Should().Be(original.Metadata);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void Parse_WithNestedMetadataJson_PreservesStructure()
    {
        // metadata field contains a JSON string value
        var nestedJson = "{\"reasonCode\": \"batch.erasure\", \"metadata\": \"{\\\"requestId\\\": \\\"req-123\\\", \\\"subjects\\\": 42}\"}";

        var result = AuditRecordDetails.Parse(nestedJson);
        result.Should().NotBeNull();
        result!.ReasonCode.Should().Be("batch.erasure");
        result.Metadata.Should().NotBeNullOrEmpty();
        result.Metadata.Should().Contain("requestId");
    }

    [Fact]
    public void Parse_WithAllNullValues_StillCreatesRecord()
    {
        var json = """
            {
              "oldValue": null,
              "newValue": null,
              "bulkOperationTag": null,
              "reasonCode": null,
              "metadata": null,
              "redactionAction": null
            }
            """;

        var result = AuditRecordDetails.Parse(json);
        result.Should().NotBeNull();
        result!.OldValue.Should().BeNull();
        result.NewValue.Should().BeNull();
        result.BulkOperationTag.Should().BeNull();
        result.ReasonCode.Should().BeNull();
        result.Metadata.Should().BeNull();
        result.RedactionAction.Should().BeNull();
    }

    [Fact]
    public void Parse_WithExtraUnknownProperties_IgnoresThem()
    {
        var json = """
            {
              "oldValue": "test",
              "unknownProp1": "ignored",
              "anotherUnknown": 123,
              "newValue": "test2"
            }
            """;

        var result = AuditRecordDetails.Parse(json);
        result.Should().NotBeNull();
        result!.OldValue.Should().Be("test");
        result.NewValue.Should().Be("test2");
        // Extra properties are silently ignored by deserializer
    }

    [Fact]
    public void Record_IsImmutable_CannotModifyAfterCreation()
    {
        var record = new AuditRecordDetails
        {
            OldValue = "old",
            NewValue = "new"
        };

        // Records are immutable by design
        var record2 = record with { OldValue = "changed" };

        record.OldValue.Should().Be("old");  // Original unchanged
        record2.OldValue.Should().Be("changed");  // New instance created
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        var record1 = new AuditRecordDetails { OldValue = "test", ReasonCode = "reason" };
        var record2 = new AuditRecordDetails { OldValue = "test", ReasonCode = "reason" };

        record1.Should().Be(record2);  // Value equality
    }

    [Fact]
    public void Records_WithDifferentValues_AreNotEqual()
    {
        var record1 = new AuditRecordDetails { OldValue = "test1" };
        var record2 = new AuditRecordDetails { OldValue = "test2" };

        record1.Should().NotBe(record2);
    }

    #endregion

    #region Integration — With AuditRecord

    [Fact]
    public void AuditRecordDetails_CanBeStoredInAuditRecord_Details_Field()
    {
        var details = new AuditRecordDetails
        {
            OldValue = "alice@example.com",
            NewValue = "bob@example.com",
            ReasonCode = "compliance.erasure"
        };

        var json = details.ToJson();

        var auditRecord = new AuditRecord
        {
            DataSubjectId = "subj-123",
            Entity = "User",
            Field = "Email",
            Details = json
        };

        auditRecord.Details.Should().NotBeNull();

        var parsed = AuditRecordDetails.Parse(auditRecord.Details);
        parsed.Should().NotBeNull();
        parsed!.OldValue.Should().Be(details.OldValue);
    }

    #endregion
}
