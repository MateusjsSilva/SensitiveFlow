using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Models;

namespace SensitiveFlow.Core.Tests;

/// <summary>
/// Integration tests for Core improvements:
/// 1. Compile-time DataSubjectId validation (SF0003 Error)
/// 2. CompositeDataSubjectId for multi-key entities
/// 3. Role-based redaction contexts (AdminView, SupportView, CustomerView)
/// </summary>
public class CoreFeatureScenariosTests
{
    #region CompositeDataSubjectId Tests

    [Fact]
    public void CompositeDataSubjectId_InitializesWithMultipleKeys()
    {
        // Arrange & Act
        var attr = new CompositeDataSubjectIdAttribute("CustomerId", "OrderId", "ItemId");

        // Assert: All properties stored
        Assert.NotNull(attr.PropertyNames);
        Assert.Equal(3, attr.PropertyNames.Length);
        Assert.Equal("CustomerId", attr.PropertyNames[0]);
        Assert.Equal("OrderId", attr.PropertyNames[1]);
        Assert.Equal("ItemId", attr.PropertyNames[2]);
    }

    [Fact]
    public void CompositeDataSubjectId_RequiresAtLeastOneProperty()
    {
        // Act & Assert: Should throw
        Assert.Throws<ArgumentNullException>(() =>
            new CompositeDataSubjectIdAttribute(null!));
    }

    [Fact]
    public void CompositeDataSubjectId_RequiresNonEmptyArray()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new CompositeDataSubjectIdAttribute(new string[] { }));
    }

    [Fact]
    public void CompositeDataSubjectId_CanInitializeWithSingleProperty()
    {
        // Arrange & Act
        var attr = new CompositeDataSubjectIdAttribute("AccountId");

        // Assert
        Assert.Single(attr.PropertyNames);
        Assert.Equal("AccountId", attr.PropertyNames[0]);
    }

    #endregion

    #region Role-Based Redaction Tests

    [Fact]
    public void RedactionAttribute_SetsRoleBasedActions()
    {
        // Arrange & Act
        var attr = new RedactionAttribute
        {
            AdminView = OutputRedactionAction.None,
            SupportView = OutputRedactionAction.Mask,
            CustomerView = OutputRedactionAction.Redact,
            ApiResponse = OutputRedactionAction.Mask,
            Logs = OutputRedactionAction.Redact
        };

        // Assert: Each role has correct action
        Assert.Equal(OutputRedactionAction.None, attr.AdminView);
        Assert.Equal(OutputRedactionAction.Mask, attr.SupportView);
        Assert.Equal(OutputRedactionAction.Redact, attr.CustomerView);
    }

    [Fact]
    public void RedactionAttribute_ForContext_ReturnsCorrectActionForAdminView()
    {
        // Arrange
        var attr = new RedactionAttribute
        {
            AdminView = OutputRedactionAction.None,
            SupportView = OutputRedactionAction.Mask
        };

        // Act
        var action = attr.ForContext(RedactionContext.AdminView);

        // Assert
        Assert.Equal(OutputRedactionAction.None, action);
    }

    [Fact]
    public void RedactionAttribute_ForContext_ReturnsCorrectActionForSupportView()
    {
        // Arrange
        var attr = new RedactionAttribute
        {
            AdminView = OutputRedactionAction.None,
            SupportView = OutputRedactionAction.Mask,
            CustomerView = OutputRedactionAction.Redact
        };

        // Act
        var action = attr.ForContext(RedactionContext.SupportView);

        // Assert
        Assert.Equal(OutputRedactionAction.Mask, action);
    }

    [Fact]
    public void RedactionAttribute_ForContext_ReturnsCorrectActionForCustomerView()
    {
        // Arrange
        var attr = new RedactionAttribute
        {
            CustomerView = OutputRedactionAction.Redact
        };

        // Act
        var action = attr.ForContext(RedactionContext.CustomerView);

        // Assert
        Assert.Equal(OutputRedactionAction.Redact, action);
    }

    [Fact]
    public void RedactionAttribute_AllContextsSupported()
    {
        // Arrange: Create attribute with all 7 contexts
        var attr = new RedactionAttribute
        {
            ApiResponse = OutputRedactionAction.Mask,
            Logs = OutputRedactionAction.Redact,
            Audit = OutputRedactionAction.Redact,
            Export = OutputRedactionAction.None,
            AdminView = OutputRedactionAction.None,
            SupportView = OutputRedactionAction.Mask,
            CustomerView = OutputRedactionAction.Redact
        };

        // Act & Assert: All contexts resolve correctly
        Assert.Equal(OutputRedactionAction.Mask, attr.ForContext(RedactionContext.ApiResponse));
        Assert.Equal(OutputRedactionAction.Redact, attr.ForContext(RedactionContext.Log));
        Assert.Equal(OutputRedactionAction.Redact, attr.ForContext(RedactionContext.Audit));
        Assert.Equal(OutputRedactionAction.None, attr.ForContext(RedactionContext.Export));
        Assert.Equal(OutputRedactionAction.None, attr.ForContext(RedactionContext.AdminView));
        Assert.Equal(OutputRedactionAction.Mask, attr.ForContext(RedactionContext.SupportView));
        Assert.Equal(OutputRedactionAction.Redact, attr.ForContext(RedactionContext.CustomerView));
    }

    #endregion

    #region RedactionContext Enum Tests

    [Fact]
    public void RedactionContext_HasAllSevenValues()
    {
        // Arrange & Act
        var values = Enum.GetValues(typeof(RedactionContext)).Cast<RedactionContext>().ToList();

        // Assert
        Assert.Contains(RedactionContext.ApiResponse, values);
        Assert.Contains(RedactionContext.Log, values);
        Assert.Contains(RedactionContext.Audit, values);
        Assert.Contains(RedactionContext.Export, values);
        Assert.Contains(RedactionContext.AdminView, values);
        Assert.Contains(RedactionContext.SupportView, values);
        Assert.Contains(RedactionContext.CustomerView, values);
        Assert.Equal(7, values.Count);
    }

    #endregion

    #region OutputRedactionAction Tests

    [Fact]
    public void OutputRedactionAction_HasAllActions()
    {
        // Arrange & Act
        var actions = Enum.GetValues(typeof(OutputRedactionAction)).Cast<OutputRedactionAction>().ToList();

        // Assert: All actions available
        Assert.Contains(OutputRedactionAction.None, actions);
        Assert.Contains(OutputRedactionAction.Redact, actions);
        Assert.Contains(OutputRedactionAction.Mask, actions);
        Assert.Contains(OutputRedactionAction.Omit, actions);
    }

    [Fact]
    public void OutputRedactionAction_DescribesIntention()
    {
        // Arrange: Each action has different behavior intent
        // None = show full value
        // Mask = show partial (e.g., j*****@example.com)
        // Redact = show [REDACTED]
        // Omit = don't include field

        // Act & Assert: Enum values exist and differ
        Assert.NotEqual(OutputRedactionAction.None, OutputRedactionAction.Mask);
        Assert.NotEqual(OutputRedactionAction.Mask, OutputRedactionAction.Redact);
        Assert.NotEqual(OutputRedactionAction.Redact, OutputRedactionAction.Omit);
    }

    #endregion

    #region AuditQuery Tests

    [Fact]
    public void AuditQuery_BuilderPattern_ChainsFilters()
    {
        // Arrange & Act
        var query = new AuditQuery()
            .ByEntity("User")
            .ByOperation("Delete")
            .ByDataSubject("user-123")
            .InTimeRange(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow)
            .WithPagination(0, 50)
            .OrderByProperty("Timestamp", descending: true);

        // Assert: All filters applied
        Assert.Equal("User", query.Entity);
        Assert.Equal("Delete", query.Operation);
        Assert.Equal("user-123", query.DataSubjectId);
        Assert.Equal(0, query.Skip);
        Assert.Equal(50, query.Take);
        Assert.Equal("Timestamp", query.OrderBy);
        Assert.True(query.OrderByDescending);
    }

    [Fact]
    public void AuditQuery_Clone_CreatesCopy()
    {
        // Arrange
        var original = new AuditQuery()
            .ByEntity("Order")
            .WithPagination(10, 25);

        // Act
        var clone = original.Clone();

        // Assert: Clone has same values
        Assert.Equal(original.Entity, clone.Entity);
        Assert.Equal(original.Skip, clone.Skip);
        Assert.Equal(original.Take, clone.Take);

        // Assert: Clone is independent
        clone.ByEntity("Invoice");
        Assert.NotEqual(original.Entity, clone.Entity);
    }

    #endregion

    #region AuditRecord Tests

    [Fact]
    public void AuditRecord_HasDefaultValues()
    {
        // Arrange & Act
        var record = new AuditRecord
        {
            DataSubjectId = "user-1",
            Entity = "Account",
            Field = "Email"
        };

        // Assert: Defaults applied
        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(AuditOperation.Access, record.Operation); // Default
        Assert.NotEqual(DateTimeOffset.MinValue, record.Timestamp); // Auto-set
    }

    [Fact]
    public void AuditRecord_SupportsIntegrityChain()
    {
        // Arrange & Act
        var record = new AuditRecord
        {
            DataSubjectId = "user-1",
            Entity = "Document",
            Field = "Content",
            PreviousRecordHash = "hash-of-previous",
            CurrentRecordHash = "hash-of-current"
        };

        // Assert: Hash chain fields set
        Assert.Equal("hash-of-previous", record.PreviousRecordHash);
        Assert.Equal("hash-of-current", record.CurrentRecordHash);
    }

    [Fact]
    public void AuditRecord_IsImmutable()
    {
        // Arrange: Record is a sealed record (immutable)
        var originalId = Guid.NewGuid();
        var original = new AuditRecord
        {
            Id = originalId,
            DataSubjectId = "user-1",
            Entity = "Settings",
            Field = "Timezone"
        };

        // Act: Create modified copy using 'with' expression
        var modified = original with { Field = "Language" };

        // Assert: Original unchanged, copy modified
        Assert.Equal("Timezone", original.Field);
        Assert.Equal("Language", modified.Field);
        Assert.Equal(original.Id, modified.Id); // Same ID (with-expression copies all fields)

        // Assert: Records are records (support immutable updates)
        Assert.NotEqual(original, modified); // Different because Field differs
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_MultiKeyOrderLineItem()
    {
        // Scenario: E-commerce order line items use (CustomerId + OrderId) as composite key

        // Arrange: Order line item with composite ID
        var compositeAttr = new CompositeDataSubjectIdAttribute(
            nameof(TestOrderLineItem.CustomerId),
            nameof(TestOrderLineItem.OrderId));

        var item = new TestOrderLineItem
        {
            CustomerId = "CUST-456",
            OrderId = "ORD-2024-001",
            ProductId = "PROD-789",
            Quantity = 3,
            Price = 29.99m
        };

        // Act: Composite key would be "CUST-456:ORD-2024-001"
        var compositeKey = $"{item.CustomerId}:{item.OrderId}";

        // Assert
        Assert.Equal(2, compositeAttr.PropertyNames.Length);
        Assert.Equal("CUST-456:ORD-2024-001", compositeKey);
    }

    [Fact]
    public void Scenario_RoleBasedDataVisibility()
    {
        // Scenario: Same API endpoint returns different data visibility by user role

        // Admin can see full email
        var attr = new RedactionAttribute
        {
            ApiResponse = OutputRedactionAction.Mask,
            Logs = OutputRedactionAction.Redact,
            Audit = OutputRedactionAction.Redact,
            Export = OutputRedactionAction.None,
            AdminView = OutputRedactionAction.None,
            SupportView = OutputRedactionAction.Mask,
            CustomerView = OutputRedactionAction.Redact
        };

        // Support sees masked (john.d...@example.com)
        var supportAction = attr.ForContext(RedactionContext.SupportView);
        Assert.Equal(OutputRedactionAction.Mask, supportAction);

        // Customer sees [REDACTED]
        var customerAction = attr.ForContext(RedactionContext.CustomerView);
        Assert.Equal(OutputRedactionAction.Redact, customerAction);
    }

    [Fact]
    public void Scenario_AuditTrailQuery()
    {
        // Scenario: Query audit logs for specific user's actions in last 7 days

        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);
        var now = DateTimeOffset.UtcNow;

        var query = new AuditQuery()
            .ByDataSubject("user-secure-123")
            .ByEntity("CreditCard")
            .InTimeRange(sevenDaysAgo, now)
            .OrderByProperty("Timestamp", descending: true)
            .WithPagination(0, 100);

        // Assert: Query properly constructed
        Assert.Equal("user-secure-123", query.DataSubjectId);
        Assert.Equal("CreditCard", query.Entity);
        Assert.True(query.OrderByDescending);
        Assert.Equal(100, query.Take);
    }

    #endregion

    /// <summary>
    /// Test entity: Order line item with composite data subject key.
    /// </summary>
    private class TestOrderLineItem
    {
        [CompositeDataSubjectId(nameof(CustomerId), nameof(OrderId))]
        public class Model
        {
            public required string CustomerId { get; set; }
            public required string OrderId { get; set; }
            public required string ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        public string CustomerId { get; set; } = "";
        public string OrderId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
