using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Interfaces;

namespace SensitiveFlow.Retention.Services;

/// <summary>
/// Background service that periodically evaluates and applies retention policies to entities
/// with expired <see cref="SensitiveFlow.Core.Attributes.RetentionDataAttribute"/> fields.
/// </summary>
/// <remarks>
/// <para>
/// This service runs on a configurable interval and queries all entities via
/// <see cref="RetentionExecutor"/> to apply expiration policies automatically.
/// It is intended to run in production environments where retention compliance is required.
/// </para>
/// <para>
/// <b>Entity Discovery:</b> The scheduler uses reflection to discover all DbSet properties
/// in the provided DbContext type and processes each one. Entities without
/// <see cref="SensitiveFlow.Core.Attributes.RetentionDataAttribute"/>-decorated properties
/// are skipped.
/// </para>
/// <para>
/// <b>Error Handling:</b> If the scheduler encounters an error, it logs the exception and
/// continues to the next interval. No exceptions are thrown to avoid crashing the host.
/// </para>
/// </remarks>
public sealed class RetentionSchedulerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RetentionSchedulerOptions _options;
    private readonly ILogger<RetentionSchedulerHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RetentionSchedulerHostedService"/>.
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve DbContext and RetentionExecutor.</param>
    /// <param name="options">Configuration for the scheduler.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public RetentionSchedulerHostedService(
        IServiceProvider serviceProvider,
        RetentionSchedulerOptions options,
        ILogger<RetentionSchedulerHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetentionSchedulerHostedService starting; will run every {Interval}",
            _options.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Initial delay: wait before first execution
                if (_options.InitialDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_options.InitialDelay, stoppingToken);
                }

                _logger.LogInformation("Starting retention evaluation cycle");

                await ExecuteRetentionAsync(stoppingToken);

                _logger.LogInformation("Retention evaluation cycle completed successfully");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("RetentionSchedulerHostedService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retention evaluation cycle");
            }

            // Wait for next interval
            try
            {
                await Task.Delay(_options.Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ExecuteRetentionAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateAsyncScope();

        var dbContextType = _options.DbContextType;
        var dbContext = scope.ServiceProvider.GetService(dbContextType) as DbContext;

        if (dbContext is null)
        {
            _logger.LogWarning(
                "Could not resolve DbContext of type {DbContextType}",
                dbContextType.Name);
            return;
        }

        var executor = scope.ServiceProvider.GetRequiredService<RetentionExecutor>();

        // Discover all DbSet<T> properties
        var dbSetProperties = dbContextType
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

        _logger.LogInformation("Found {DbSetCount} DbSet properties to evaluate", dbSetProperties.Count);

        var totalExpired = 0;
        var totalAnonymized = 0;
        var totalDeleted = 0;

        foreach (var dbSetProperty in dbSetProperties)
        {
            try
            {
                var entityType = dbSetProperty.PropertyType.GetGenericArguments().First();
                var dbSet = dbSetProperty.GetValue(dbContext);

                if (dbSet is null)
                {
                    continue;
                }

                // Convert DbSet to IQueryable and retrieve all entities
                var method = typeof(Queryable).GetMethod("AsQueryable", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, [typeof(System.Collections.IEnumerable)], null);

                if (method is null)
                {
                    continue;
                }

                var queryableMethod = method.MakeGenericMethod(entityType);
                var queryable = (IQueryable?)queryableMethod.Invoke(null, [dbSet]);

                if (queryable is null)
                {
                    continue;
                }

                // Execute async enumeration
                var castMethod = typeof(Enumerable).GetMethod("Cast", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, [typeof(System.Collections.IEnumerable)], null);

                if (castMethod is null)
                {
                    continue;
                }

                var castMethod2 = castMethod.MakeGenericMethod(typeof(object));
                var entities = (IEnumerable<object>?)castMethod2.Invoke(null, [queryable]);

                if (entities is null)
                {
                    continue;
                }

                // Alternative: use dynamic approach with ToListAsync
                var toListAsync = typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions)
                    .GetMethod("ToListAsync", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null, [typeof(System.Linq.IQueryable<>), typeof(System.Threading.CancellationToken)], null);

                if (toListAsync is null)
                {
                    continue;
                }

                var toListMethod = toListAsync.MakeGenericMethod(entityType);
                var entitiesTask = toListMethod.Invoke(null, [queryable, cancellationToken]) as Task;

                if (entitiesTask is null)
                {
                    continue;
                }

                await entitiesTask;

                // Get result from task
                var resultProperty = entitiesTask.GetType().GetProperty("Result");
                var result = resultProperty?.GetValue(entitiesTask) as System.Collections.IEnumerable;

                if (result is null)
                {
                    continue;
                }

                var entitiesList = result.Cast<object>().ToList();

                _logger.LogDebug(
                    "Evaluating {EntityCount} entities of type {EntityType}",
                    entitiesList.Count,
                    entityType.Name);

                if (entitiesList.Count == 0)
                {
                    continue;
                }

                // Execute retention policy
                var report = await executor.ExecuteAsync(
                    entitiesList,
                    entity => GetRetentionReferenceDate(entity),
                    cancellationToken);

                totalExpired += report.Entries.Count;
                totalAnonymized += report.Entries.Count(e =>
                    e.Action == RetentionAction.Anonymized);
                totalDeleted += report.Entries.Count(e =>
                    e.Action == RetentionAction.DeletePending);

                _logger.LogDebug(
                    "Retention evaluation for {EntityType}: {Anonymized} anonymized, {Deleted} marked for deletion",
                    entityType.Name,
                    totalAnonymized,
                    totalDeleted);

                // For DeletePending entries, remove from DbContext
                foreach (var entry in report.Entries.Where(e =>
                    e.Action == RetentionAction.DeletePending))
                {
                    dbContext.Remove(entry.Entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating retention for DbSet property {PropertyName}",
                    dbSetProperty.Name);
            }
        }

        // Save changes if any entities were modified
        if (dbContext.ChangeTracker.HasChanges())
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Retention cycle completed: {Total} entities expired, {Anonymized} anonymized, {Deleted} deleted",
                    totalExpired,
                    totalAnonymized,
                    totalDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving retention changes to database");
                throw;
            }
        }
        else
        {
            _logger.LogInformation("No retention changes needed");
        }
    }

    private static DateTimeOffset GetRetentionReferenceDate(object entity)
    {
        // Try to find CreatedAt, CreatedDate, or DateCreated property
        var type = entity.GetType();
        var properties = type.GetProperties();

        var dateProperty = properties.FirstOrDefault(p =>
            (p.Name.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
             p.Name.Equals("CreatedDate", StringComparison.OrdinalIgnoreCase) ||
             p.Name.Equals("DateCreated", StringComparison.OrdinalIgnoreCase)) &&
            (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTimeOffset)));

        if (dateProperty is not null && dateProperty.GetValue(entity) is DateTimeOffset dateOffset)
        {
            return dateOffset;
        }

        if (dateProperty is not null && dateProperty.GetValue(entity) is DateTime dateTime)
        {
            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        // Fallback: use current time (conservative approach - no expiration)
        return DateTimeOffset.UtcNow;
    }
}
