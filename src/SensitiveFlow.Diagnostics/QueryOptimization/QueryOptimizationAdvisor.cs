using SensitiveFlow.Core.Enums;

namespace SensitiveFlow.Diagnostics.QueryOptimization;

/// <summary>
/// Analyzes query patterns and suggests index optimizations.
/// </summary>
public sealed class QueryOptimizationAdvisor
{
    private readonly Dictionary<string, QueryPattern> _patterns = new();

    /// <summary>
    /// Record a query pattern for analysis.
    /// </summary>
    public void RecordQueryPattern(string entity, AuditOperation? operation = null, string? dataSubjectId = null)
    {
        var key = GeneratePatternKey(entity, operation, dataSubjectId);

        if (_patterns.TryGetValue(key, out var pattern))
        {
            pattern.ExecutionCount++;
            pattern.LastExecuted = DateTimeOffset.UtcNow;
        }
        else
        {
            _patterns[key] = new QueryPattern
            {
                Entity = entity,
                Operation = operation,
                DataSubjectIdQueried = dataSubjectId != null,
                ExecutionCount = 1,
                FirstExecuted = DateTimeOffset.UtcNow,
                LastExecuted = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>
    /// Get optimization recommendations based on query patterns.
    /// </summary>
    public List<IndexRecommendation> GetIndexRecommendations()
    {
        var recommendations = new List<IndexRecommendation>();

        // Group by entity
        var byEntity = _patterns
            .GroupBy(p => p.Value.Entity)
            .OrderByDescending(g => g.Sum(p => p.Value.ExecutionCount));

        foreach (var entityGroup in byEntity)
        {
            var entity = entityGroup.Key;
            var patterns = entityGroup.ToList();

            var totalExecutions = patterns.Sum(p => p.Value.ExecutionCount);
            var dataSubjectPatterns = patterns.Where(p => p.Value.DataSubjectIdQueried).ToList();
            var operationPatterns = patterns.Where(p => p.Value.Operation.HasValue).ToList();

            // Most common pattern: queries on DataSubjectId + Timestamp
            if (dataSubjectPatterns.Any() && dataSubjectPatterns.Sum(p => p.Value.ExecutionCount) > 10)
            {
                recommendations.Add(new IndexRecommendation
                {
                    Entity = entity,
                    Columns = new[] { "DataSubjectId", "Timestamp DESC" },
                    Priority = RecommendationPriority.High,
                    Reason = "Most queries filter by DataSubjectId; index improves query performance",
                    EstimatedImprovement = "30-50% faster queries"
                });
            }

            // Pattern: queries by Entity + Operation
            if (operationPatterns.Any() && operationPatterns.Sum(p => p.Value.ExecutionCount) > 5)
            {
                recommendations.Add(new IndexRecommendation
                {
                    Entity = entity,
                    Columns = new[] { "Entity", "Operation", "Timestamp DESC" },
                    Priority = RecommendationPriority.Medium,
                    Reason = "Filters by entity type and operation frequently",
                    EstimatedImprovement = "20-30% faster filtering"
                });
            }

            // Pattern: Actor tracking queries
            var actorQueries = dataSubjectPatterns.Sum(p => p.Value.ExecutionCount);
            if (actorQueries > 5)
            {
                recommendations.Add(new IndexRecommendation
                {
                    Entity = entity,
                    Columns = new[] { "ActorId", "Timestamp DESC" },
                    Priority = RecommendationPriority.Medium,
                    Reason = "Actor ID filtering detected; helps with compliance audits",
                    EstimatedImprovement = "15-25% faster actor lookups"
                });
            }
        }

        return recommendations
            .OrderByDescending(r => r.Priority)
            .ToList();
    }

    /// <summary>
    /// Get statistics about query patterns.
    /// </summary>
    public QueryPatternStatistics GetStatistics()
    {
        var totalQueries = _patterns.Values.Sum(p => p.ExecutionCount);
        var uniquePatterns = _patterns.Count;
        var mostCommon = _patterns
            .OrderByDescending(p => p.Value.ExecutionCount)
            .FirstOrDefault();

        return new QueryPatternStatistics
        {
            TotalQueries = totalQueries,
            UniquePatterns = uniquePatterns,
            MostCommonPattern = mostCommon.Value,
            AverageExecutionsPerPattern = totalQueries / Math.Max(1, uniquePatterns),
            QueriesByEntity = _patterns
                .GroupBy(p => p.Value.Entity)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Value.ExecutionCount))
        };
    }

    /// <summary>
    /// Clear recorded patterns.
    /// </summary>
    public void Clear()
    {
        _patterns.Clear();
    }

    private static string GeneratePatternKey(string entity, AuditOperation? operation, string? dataSubjectId)
        => $"{entity}|{operation?.ToString() ?? "ANY"}|{(dataSubjectId != null ? "SUBJECT" : "NOSUBJECT")}";
}

/// <summary>
/// Represents a query pattern extracted from audit operations.
/// </summary>
public class QueryPattern
{
    /// <summary>Entity being queried.</summary>
    public required string Entity { get; set; }
    /// <summary>Audit operation type, if specific.</summary>
    public AuditOperation? Operation { get; set; }
    /// <summary>Indicates if pattern queries by data subject ID.</summary>
    public bool DataSubjectIdQueried { get; set; }
    /// <summary>Number of times this pattern was executed.</summary>
    public int ExecutionCount { get; set; }
    /// <summary>First time this pattern was observed.</summary>
    public DateTimeOffset FirstExecuted { get; set; }
    /// <summary>Most recent time this pattern was executed.</summary>
    public DateTimeOffset LastExecuted { get; set; }
}

/// <summary>
/// Index creation recommendation.
/// </summary>
public class IndexRecommendation
{
    /// <summary>Entity for which to create the index.</summary>
    public required string Entity { get; set; }
    /// <summary>Columns to include in the index.</summary>
    public required string[] Columns { get; set; }
    /// <summary>Priority level of this recommendation.</summary>
    public required RecommendationPriority Priority { get; set; }
    /// <summary>Reason for the recommendation.</summary>
    public required string Reason { get; set; }
    /// <summary>Estimated performance improvement from this index.</summary>
    public required string EstimatedImprovement { get; set; }

    /// <summary>Generates a SQL CREATE INDEX statement for this recommendation.</summary>
    public override string ToString()
        => $"CREATE INDEX IX_{Entity}_{string.Join("_", Columns.Select(c => c.Replace(" DESC", "")))} " +
           $"ON {Entity} ({string.Join(", ", Columns)}) -- {Reason}";
}

/// <summary>
/// Priority level for index recommendations.
/// </summary>
public enum RecommendationPriority
{
    /// <summary>Low priority index recommendation.</summary>
    Low,
    /// <summary>Medium priority index recommendation.</summary>
    Medium,
    /// <summary>High priority index recommendation.</summary>
    High,
    /// <summary>Critical priority index recommendation.</summary>
    Critical
}

/// <summary>
/// Statistics about recorded query patterns.
/// </summary>
public class QueryPatternStatistics
{
    /// <summary>Total number of queries executed.</summary>
    public int TotalQueries { get; set; }
    /// <summary>Number of unique query patterns.</summary>
    public int UniquePatterns { get; set; }
    /// <summary>Most frequently executed query pattern.</summary>
    public QueryPattern? MostCommonPattern { get; set; }
    /// <summary>Average executions per pattern.</summary>
    public int AverageExecutionsPerPattern { get; set; }
    /// <summary>Query counts grouped by entity.</summary>
    public Dictionary<string, int> QueriesByEntity { get; set; } = new();
}
