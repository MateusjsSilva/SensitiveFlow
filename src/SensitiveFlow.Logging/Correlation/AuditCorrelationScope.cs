using Microsoft.Extensions.Logging;
using SensitiveFlow.Core.Correlation;

namespace SensitiveFlow.Logging.Correlation;

/// <summary>
/// <see cref="ILogger"/> decorator that injects audit correlation IDs into every log scope.
/// </summary>
public sealed class AuditCorrelationScope : ILogger
{
    private const string CorrelationIdKey = "AuditCorrelationId";
    private const string RequestIdKey = "AuditRequestId";
    private const string TraceIdKey = "AuditTraceId";

    private readonly ILogger _innerLogger;

    /// <summary>
    /// Initializes a new instance that wraps an inner logger with correlation ID injection.
    /// </summary>
    public AuditCorrelationScope(ILogger innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var current = SensitiveFlowCorrelation.Current;
        if (current == null)
        {
            return _innerLogger.BeginScope(state);
        }

        var scope = _innerLogger.BeginScope(state);
        var innerScope = InjectCorrelationIds(current);
        return new CompositeDisposable(scope, innerScope);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var current = SensitiveFlowCorrelation.Current;
        if (current == null)
        {
            _innerLogger.Log(logLevel, eventId, state, exception, formatter);
            return;
        }

        using var _ = InjectCorrelationIds(current);
        _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }

    private IDisposable? InjectCorrelationIds(IAuditCorrelationContext context)
    {
        var scope = new Dictionary<string, object?>();
        if (context.CorrelationId is not null)
        {
            scope[CorrelationIdKey] = context.CorrelationId;
        }

        if (context.RequestId is not null)
        {
            scope[RequestIdKey] = context.RequestId;
        }

        if (context.TraceId is not null)
        {
            scope[TraceIdKey] = context.TraceId;
        }

        return _innerLogger.BeginScope(scope);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable? _first;
        private readonly IDisposable? _second;

        public CompositeDisposable(IDisposable? first, IDisposable? second)
        {
            _first = first;
            _second = second;
        }

        public void Dispose()
        {
            _first?.Dispose();
            _second?.Dispose();
        }
    }
}
