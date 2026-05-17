using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SensitiveFlow.AspNetCore.EFCore.DtoMapping;
using SensitiveFlow.AspNetCore.EFCore.HeaderControl;
using SensitiveFlow.AspNetCore.EFCore.PerformanceMetrics;
using SensitiveFlow.AspNetCore.EFCore.RoleBasedRedaction;
using SensitiveFlow.Json.Enums;
using System.Security.Claims;
using Xunit;

namespace SensitiveFlow.AspNetCore.EFCore.Tests;

public class AspNetCoreEFCoreEnhancementsTests
{
    // DTO Mapping Tests
    [Fact]
    public void DtoMappingOptions_MapEntity_RegistersMapping()
    {
        var options = new DtoMappingOptions();
        options.MapEntity<TestEntity, TestDto>();

        options.Mappings.Should().ContainKey(typeof(TestEntity));
        options.GetDtoType(typeof(TestEntity)).Should().Be(typeof(TestDto));
    }

    [Fact]
    public void DtoMappingOptions_GetDtoType_ReturnsNullForUnmappedType()
    {
        var options = new DtoMappingOptions();

        options.GetDtoType(typeof(TestEntity)).Should().BeNull();
    }

    [Fact]
    public void DtoMapper_Map_ReturnsDtoWhenMappingExists()
    {
        var options = new DtoMappingOptions();
        options.MapEntity<TestEntity, TestDto>();
        var mapper = new DtoMapper(options);

        var entity = new TestEntity { Id = 1, Name = "Test" };
        var result = mapper.Map(entity);

        result.Should().BeOfType<TestDto>();
        ((TestDto)result).Id.Should().Be(1);
        ((TestDto)result).Name.Should().Be("Test");
    }

    [Fact]
    public void DtoMapper_Map_ReturnsEntityWhenNoMappingExists()
    {
        var options = new DtoMappingOptions();
        var mapper = new DtoMapper(options);

        var entity = new TestEntity { Id = 1, Name = "Test" };
        var result = mapper.Map(entity);

        result.Should().Be(entity);
    }

    [Fact]
    public void DtoMapper_Map_ReturnsNullForNullInput()
    {
        var options = new DtoMappingOptions();
        var mapper = new DtoMapper(options);

        var result = mapper.Map(null);

        result.Should().BeNull();
    }

    [Fact]
    public void DtoMapper_MapEnumerable_MapsAllEntities()
    {
        var options = new DtoMappingOptions();
        options.MapEntity<TestEntity, TestDto>();
        var mapper = new DtoMapper(options);

        var entities = new object[]
        {
            new TestEntity { Id = 1, Name = "Test1" },
            new TestEntity { Id = 2, Name = "Test2" }
        };

        var results = mapper.MapEnumerable(entities).ToList();

        results.Should().HaveCount(2);
        results.All(r => r is TestDto).Should().BeTrue();
    }

    [Fact]
    public void DtoMapper_MapEnumerable_ReturnsEmptyForNullInput()
    {
        var options = new DtoMappingOptions();
        var mapper = new DtoMapper(options);

        var result = mapper.MapEnumerable(null).ToList();

        result.Should().BeEmpty();
    }

    // Role-Based Redaction Tests
    [Fact]
    public void RoleBasedRedactionOptions_ConfigureRole_RegistersRole()
    {
        var options = new RoleBasedRedactionOptions();
        options.ConfigureRole("admin", JsonRedactionMode.None);

        options.RoleOverrides.Should().ContainKey("admin");
        options.RoleOverrides["admin"].Should().Be(JsonRedactionMode.None);
    }

    [Fact]
    public void RoleBasedRedactionOptions_GetModeForRoles_ReturnsModeForMatchingRole()
    {
        var options = new RoleBasedRedactionOptions();
        options.ConfigureRole("admin", JsonRedactionMode.None);
        options.ConfigureRole("user", JsonRedactionMode.Mask);

        var mode = options.GetModeForRoles(new[] { "user", "admin" });

        mode.Should().Be(JsonRedactionMode.Mask); // First match
    }

    [Fact]
    public void RoleBasedRedactionOptions_GetModeForRoles_ReturnsDefaultForNoMatch()
    {
        var options = new RoleBasedRedactionOptions { DefaultMode = JsonRedactionMode.Mask };

        var mode = options.GetModeForRoles(new[] { "unknown" });

        mode.Should().Be(JsonRedactionMode.Mask);
    }

    [Fact]
    public void RoleBasedRedactionOptions_GetModeForRoles_ReturnsDefaultForNullRoles()
    {
        var options = new RoleBasedRedactionOptions { DefaultMode = JsonRedactionMode.Mask };

        var mode = options.GetModeForRoles(null);

        mode.Should().Be(JsonRedactionMode.Mask);
    }

    [Fact]
    public void RoleBasedRedactionOptions_RoleNames_CaseInsensitive()
    {
        var options = new RoleBasedRedactionOptions();
        options.ConfigureRole("Admin", JsonRedactionMode.None);

        options.RoleOverrides.Should().ContainKey("Admin");
        var mode = options.GetModeForRoles(new[] { "admin" });
        mode.Should().Be(JsonRedactionMode.None);
    }

    // Header Control Tests
    [Fact]
    public void RedactionLevelHeader_TryExtractFromHeaders_ReturnsModWhenHeaderPresent()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Redaction-Level"] = "None";

        var mode = RedactionLevelHeader.TryExtractFromHeaders(context.Request);

        mode.Should().Be(JsonRedactionMode.None);
    }

    [Fact]
    public void RedactionLevelHeader_TryExtractFromHeaders_ReturnsNullWhenHeaderAbsent()
    {
        var context = new DefaultHttpContext();

        var mode = RedactionLevelHeader.TryExtractFromHeaders(context.Request);

        mode.Should().BeNull();
    }

    [Fact]
    public void RedactionLevelHeader_TryExtractFromHeaders_ReturnsNullForInvalidValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Redaction-Level"] = "Invalid";

        var mode = RedactionLevelHeader.TryExtractFromHeaders(context.Request);

        mode.Should().BeNull();
    }

    [Fact]
    public void RedactionLevelHeader_TryExtractFromHeaders_IsCaseInsensitive()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Redaction-Level"] = "mask";

        var mode = RedactionLevelHeader.TryExtractFromHeaders(context.Request);

        mode.Should().Be(JsonRedactionMode.Mask);
    }

    [Fact]
    public void RedactionLevelHeader_TryExtractFromHeaders_UsesCustomHeaderName()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Custom"] = "Omit";

        var mode = RedactionLevelHeader.TryExtractFromHeaders(context.Request, "X-Custom");

        mode.Should().Be(JsonRedactionMode.Omit);
    }

    [Fact]
    public void RedactionLevelHeader_StoreInContext_SavesValueInItems()
    {
        var context = new DefaultHttpContext();
        RedactionLevelHeader.StoreInContext(context, JsonRedactionMode.Mask);

        var stored = RedactionLevelHeader.TryGetFromContext(context);

        stored.Should().Be(JsonRedactionMode.Mask);
    }

    [Fact]
    public void RedactionLevelHeader_StoreInContext_UsesCustomKey()
    {
        var context = new DefaultHttpContext();
        var customKey = "Custom.Key";
        RedactionLevelHeader.StoreInContext(context, JsonRedactionMode.None, customKey);

        var stored = RedactionLevelHeader.TryGetFromContext(context, customKey);

        stored.Should().Be(JsonRedactionMode.None);
    }

    [Fact]
    public void RedactionLevelHeader_TryGetFromContext_ReturnsNullWhenNotStored()
    {
        var context = new DefaultHttpContext();

        var stored = RedactionLevelHeader.TryGetFromContext(context);

        stored.Should().BeNull();
    }

    // Performance Metrics Tests
    [Fact]
    public void RedactionMetricsCollector_RecordOperation_TracksMetrics()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 1, 5);

        collector.TotalOperations.Should().Be(1);
        collector.TotalFieldsRedacted.Should().Be(1);
        collector.TotalTimeMs.Should().Be(5);
    }

    [Fact]
    public void RedactionMetricsCollector_RecordOperation_AggregatesMultipleOperations()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 1, 5);
        collector.RecordOperation("Email", 1, 3);

        var metric = collector.GetMetric("Email");

        metric.Should().NotBeNull();
        metric!.Count.Should().Be(2);
        metric.TotalTimeMs.Should().Be(8);
        metric.AverageTimeMs.Should().Be(4);
    }

    [Fact]
    public void RedactionMetricsCollector_AverageTimeMs_CalculatesCorrectly()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 1, 10);
        collector.RecordOperation("Email", 1, 20);

        collector.AverageTimeMs.Should().Be(15);
    }

    [Fact]
    public void RedactionMetricsCollector_GetAllMetrics_ReturnsAllRecordedMetrics()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 1, 5);
        collector.RecordOperation("Phone", 1, 3);

        var metrics = collector.GetAllMetrics();

        metrics.Should().HaveCount(2);
        metrics.Keys.Should().Contain("Email", "Phone");
    }

    [Fact]
    public void RedactionMetricsCollector_Clear_ResetsAllMetrics()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 1, 5);

        collector.Clear();

        collector.TotalOperations.Should().Be(0);
        collector.TotalFieldsRedacted.Should().Be(0);
        collector.TotalTimeMs.Should().Be(0);
        collector.GetAllMetrics().Should().BeEmpty();
    }

    [Fact]
    public void RedactionMetricsCollector_GetSummary_ReturnsSummaryString()
    {
        var collector = new RedactionMetricsCollector();
        collector.RecordOperation("Email", 2, 10);
        collector.RecordOperation("Phone", 1, 5);

        var summary = collector.GetSummary();

        summary.Should().Contain("Operations: 2");
        summary.Should().Contain("Fields: 3");
        summary.Should().Match("*AvgTime: 7*ms");
    }

    // Test entities
    private class TestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private class TestDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
