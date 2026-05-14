using FluentAssertions;
using SensitiveFlow.Core.Models;
using Xunit;

namespace SensitiveFlow.Audit.Tests.Models;

public sealed class AuditQueryTests
{
    #region Builder Pattern — Chaining

    [Fact]
    public void Builder_Chains_AllMethods()
    {
        var query = new AuditQuery()
            .ByEntity("User")
            .ByOperation("Delete")
            .ByActorId("admin@example.com")
            .ByDataSubject("subj-123")
            .ByField("Email")
            .InTimeRange(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow)
            .WithPagination(10, 50)
            .OrderByProperty("DataSubjectId", false);

        query.Entity.Should().Be("User");
        query.Operation.Should().Be("Delete");
        query.ActorId.Should().Be("admin@example.com");
        query.DataSubjectId.Should().Be("subj-123");
        query.Field.Should().Be("Email");
        query.Skip.Should().Be(10);
        query.Take.Should().Be(50);
        query.OrderBy.Should().Be("DataSubjectId");
        query.OrderByDescending.Should().BeFalse();
    }

    #endregion

    #region Happy Path — Individual Filters

    [Fact]
    public void ByEntity_SetsEntity()
    {
        var query = new AuditQuery().ByEntity("Order");
        query.Entity.Should().Be("Order");
    }

    [Fact]
    public void ByOperation_SetsOperation()
    {
        var query = new AuditQuery().ByOperation("Update");
        query.Operation.Should().Be("Update");
    }

    [Fact]
    public void ByActorId_SetsActorId()
    {
        var query = new AuditQuery().ByActorId("user@example.com");
        query.ActorId.Should().Be("user@example.com");
    }

    [Fact]
    public void ByDataSubject_SetsDataSubjectId()
    {
        var query = new AuditQuery().ByDataSubject("subj-456");
        query.DataSubjectId.Should().Be("subj-456");
    }

    [Fact]
    public void ByField_SetsField()
    {
        var query = new AuditQuery().ByField("PhoneNumber");
        query.Field.Should().Be("PhoneNumber");
    }

    #endregion

    #region Time Range

    [Fact]
    public void InTimeRange_SetsBothFromAndTo()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        var query = new AuditQuery().InTimeRange(from, to);

        query.From.Should().Be(from);
        query.To.Should().Be(to);
    }

    [Fact]
    public void InTimeRange_WithNullValues_ClearsFilters()
    {
        var query = new AuditQuery()
            .InTimeRange(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow)
            .InTimeRange(null, null);

        query.From.Should().BeNull();
        query.To.Should().BeNull();
    }

    [Fact]
    public void InTimeRange_WithOnlyFrom_SetsFromOnly()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var query = new AuditQuery().InTimeRange(from, null);

        query.From.Should().Be(from);
        query.To.Should().BeNull();
    }

    #endregion

    #region Pagination

    [Fact]
    public void WithPagination_SetsSkipAndTake()
    {
        var query = new AuditQuery().WithPagination(20, 75);

        query.Skip.Should().Be(20);
        query.Take.Should().Be(75);
    }

    [Fact]
    public void WithPagination_DefaultsAreZeroAndHundred()
    {
        var query = new AuditQuery();

        query.Skip.Should().Be(0);
        query.Take.Should().Be(100);
    }

    [Fact]
    public void WithPagination_CanBeZero()
    {
        var query = new AuditQuery().WithPagination(0, 0);

        query.Skip.Should().Be(0);
        query.Take.Should().Be(0);
    }

    #endregion

    #region Ordering

    [Fact]
    public void OrderByProperty_DefaultsToTimestampDescending()
    {
        var query = new AuditQuery();

        query.OrderBy.Should().Be("Timestamp");
        query.OrderByDescending.Should().BeTrue();
    }

    [Fact]
    public void OrderByProperty_SetsPropertyAndDirection()
    {
        var query = new AuditQuery().OrderByProperty("Entity", false);

        query.OrderBy.Should().Be("Entity");
        query.OrderByDescending.Should().BeFalse();
    }

    [Fact]
    public void OrderByProperty_Descending_True()
    {
        var query = new AuditQuery().OrderByProperty("DataSubjectId", true);

        query.OrderBy.Should().Be("DataSubjectId");
        query.OrderByDescending.Should().BeTrue();
    }

    [Fact]
    public void OrderByProperty_AcceptsValidProperties()
    {
        var validProperties = new[] { "Timestamp", "DataSubjectId", "Entity", "Field", "Operation" };

        foreach (var prop in validProperties)
        {
            var query = new AuditQuery().OrderByProperty(prop);
            query.OrderBy.Should().Be(prop);
        }
    }

    #endregion

    #region Clone

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new AuditQuery()
            .ByEntity("User")
            .ByOperation("Delete")
            .WithPagination(10, 50);

        var clone = original.Clone();

        clone.Entity.Should().Be(original.Entity);
        clone.Operation.Should().Be(original.Operation);
        clone.Skip.Should().Be(original.Skip);
        clone.Take.Should().Be(original.Take);
    }

    [Fact]
    public void Clone_IsIndependent_ModifyingCloneDoesNotAffectOriginal()
    {
        var original = new AuditQuery().ByEntity("User");
        var clone = original.Clone().ByOperation("Delete");

        original.Operation.Should().BeNull();
        clone.Operation.Should().Be("Delete");
        original.Entity.Should().Be("User");
        clone.Entity.Should().Be("User");
    }

    #endregion

    #region Edge Cases — Null/Empty Strings

    [Fact]
    public void ByEntity_WithNullOrEmpty_CanBeSetButIsIgnoredByStore()
    {
        var queryNull = new AuditQuery().ByEntity(null!);
        var queryEmpty = new AuditQuery().ByEntity("");

        queryNull.Entity.Should().BeNull();
        queryEmpty.Entity.Should().BeEmpty();
    }

    [Fact]
    public void ByEntity_WithWhitespace_IsKeptAsIs()
    {
        var query = new AuditQuery().ByEntity("   ");
        query.Entity.Should().Be("   ");
    }

    #endregion

    #region Complex Scenarios — Full Audit Trail Query

    [Fact]
    public void FullAuditQuery_AllFiltersApplied()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        var query = new AuditQuery()
            .ByEntity("User")
            .ByOperation("Update")
            .ByActorId("system@app.com")
            .ByDataSubject("user-001")
            .ByField("Password")
            .InTimeRange(from, to)
            .WithPagination(0, 100)
            .OrderByProperty("Timestamp", true);

        query.Entity.Should().Be("User");
        query.Operation.Should().Be("Update");
        query.ActorId.Should().Be("system@app.com");
        query.DataSubjectId.Should().Be("user-001");
        query.Field.Should().Be("Password");
        query.From.Should().Be(from);
        query.To.Should().Be(to);
        query.Skip.Should().Be(0);
        query.Take.Should().Be(100);
        query.OrderBy.Should().Be("Timestamp");
        query.OrderByDescending.Should().BeTrue();
    }

    [Fact]
    public void MultipleQueries_SameBuilder_AreIndependent()
    {
        var query1 = new AuditQuery().ByEntity("User").WithPagination(0, 10);
        var query2 = new AuditQuery().ByEntity("Order").WithPagination(10, 20);

        query1.Entity.Should().Be("User");
        query1.Skip.Should().Be(0);
        query1.Take.Should().Be(10);

        query2.Entity.Should().Be("Order");
        query2.Skip.Should().Be(10);
        query2.Take.Should().Be(20);
    }

    #endregion

    #region Compliance & Forensics

    [Fact]
    public void Query_ForDataSubjectExport_AllPiiChanges()
    {
        var query = new AuditQuery()
            .ByDataSubject("subj-alice")
            .InTimeRange(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow)
            .OrderByProperty("Timestamp", false)
            .WithPagination(0, 1000);

        query.DataSubjectId.Should().Be("subj-alice");
        query.From.Should().NotBeNull();
        query.To.Should().NotBeNull();
    }

    [Fact]
    public void Query_ForComplianceAudit_AllDeletesByActor()
    {
        var query = new AuditQuery()
            .ByOperation("Delete")
            .ByActorId("gdpr-processor@company.com")
            .WithPagination(0, 5000);

        query.Operation.Should().Be("Delete");
        query.ActorId.Should().Be("gdpr-processor@company.com");
    }

    #endregion
}
