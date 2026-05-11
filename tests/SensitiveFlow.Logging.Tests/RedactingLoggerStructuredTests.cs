using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SensitiveFlow.Core.Attributes;
using SensitiveFlow.Core.Enums;
using SensitiveFlow.Core.Policies;
using SensitiveFlow.Logging.Configuration;
using SensitiveFlow.Logging.Loggers;
using SensitiveFlow.Logging.Redaction;

namespace SensitiveFlow.Logging.Tests;

public sealed class RedactingLoggerStructuredTests
{
    private static RedactingLogger Make()
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        return new RedactingLogger(inner, new DefaultSensitiveValueRedactor());
    }

    [Fact]
    public void Log_StructuredState_SensitiveKeyIsRedacted()
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var logger = new RedactingLogger(inner, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>>
        {
            new("[Sensitive]Email", "joao@example.com"),
            new("UserId", "user-42"),
        };

        logger.Log(LogLevel.Information, new EventId(0), state, null,
            (s, _) => string.Join(", ", s.Select(kv => $"{kv.Key}={kv.Value}")));

        inner.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<IEnumerable<KeyValuePair<string, object?>>>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<IEnumerable<KeyValuePair<string, object?>>, Exception?, string>>());
    }

    [Fact]
    public void Log_StructuredState_NonSensitiveKeyPreserved()
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        IEnumerable<KeyValuePair<string, object?>>? capturedState = null;
        inner.When(l => l.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<IEnumerable<KeyValuePair<string, object?>>>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<IEnumerable<KeyValuePair<string, object?>>, Exception?, string>>()))
            .Do(ci => capturedState = (IEnumerable<KeyValuePair<string, object?>>)ci[2]);

        var logger = new RedactingLogger(inner, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>>
        {
            new("[Sensitive]Email", "joao@example.com"),
            new("UserId", "user-42"),
        };

        logger.Log(LogLevel.Information, new EventId(0), state, null, (s, _) => "");

        capturedState.Should().NotBeNull();
        capturedState!.Should().Contain(kv => kv.Key == "UserId" && (string?)kv.Value == "user-42");
        capturedState!.Should().Contain(kv => kv.Key == "[Sensitive]Email" && (string?)kv.Value == "[REDACTED]");
    }

    [Fact]
    public void Log_NonStructuredState_StillRedactsTemplate()
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        string? capturedMessage = null;
        inner.When(l => l.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<string>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<string, Exception?, string>>()))
            .Do(ci =>
            {
                var formatter = (Func<string, Exception?, string>)ci[4];
                capturedMessage = formatter((string)ci[2], null);
            });

        var logger = new RedactingLogger(inner, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            "User [Sensitive]Email logged in", null, (s, _) => s);

        capturedMessage.Should().Be("User [REDACTED] logged in");
    }

    [Fact]
    public void Log_Disabled_DoesNotForward()
    {
        var inner = Substitute.For<ILogger>();
        inner.IsEnabled(LogLevel.Debug).Returns(false);
        var logger = new RedactingLogger(inner, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>> { new("Key", "value") };
        logger.Log(LogLevel.Debug, new EventId(0), state, null, (s, _) => "");

        inner.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<IEnumerable<KeyValuePair<string, object?>>>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<IEnumerable<KeyValuePair<string, object?>>, Exception?, string>>());
    }

    [Fact]
    public void Log_StructuredState_DoesNotCorruptOtherFieldsWhenSensitiveValueIsSubstring()
    {
        // §4.1.3: a sensitive value that happened to be a substring of another value
        // would be globally string-replaced — corrupting the unrelated field. With the
        // template-based renderer, only the sensitive placeholder is redacted.
        var spy = new SpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>>
        {
            new("[Sensitive]Email", "a@x"),     // short value that appears as substring elsewhere
            new("Note", "user a@x said hi"),    // unrelated field whose value contains the sensitive substring
            new("{OriginalFormat}", "Email={[Sensitive]Email}, Note={Note}"),
        };

        logger.Log(LogLevel.Information, new EventId(0), state, null,
            (s, _) => string.Join(", ", s.Where(kv => kv.Key != "{OriginalFormat}").Select(kv => $"{kv.Key}={kv.Value}")));

        spy.LastMessage.Should().Be("Email=[REDACTED], Note=user a@x said hi");
    }

    [Fact]
    public void Log_StructuredState_PreservesUnknownTemplatePlaceholder()
    {
        var spy = new SpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>>
        {
            new("Known", "value"),
            new("{OriginalFormat}", "Known={Known}, Missing={Missing}"),
        };

        logger.Log(LogLevel.Information, new EventId(0), state, null,
            (s, _) => string.Join(", ", s.Select(kv => $"{kv.Key}={kv.Value}")));

        spy.LastMessage.Should().Be("Known=value, Missing={Missing}");
    }

    [Fact]
    public void Log_StructuredState_FormatterIsInvokedWithRedactedState()
    {
        // Uses a real spy logger so the formatter lambda inside RedactingLogger
        // is actually invoked — covering the branches at lines 63-70.
        var spy = new SpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        var state = new List<KeyValuePair<string, object?>>
        {
            new("[Sensitive]Email", "joao@example.com"),
            new("UserId", "user-42"),
        };

        logger.Log(LogLevel.Information, new EventId(0), state, null,
            (s, _) => string.Join(", ", s.Select(kv => $"{kv.Key}={kv.Value}")));

        spy.LastMessage.Should().Contain("[REDACTED]");
        spy.LastMessage.Should().Contain("user-42");
        spy.LastMessage.Should().NotContain("joao@example.com");
    }

    [Fact]
    public void Log_StructuredObject_DefaultRedactsAnnotatedMembers()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Customer", new CustomerLogShape()),
            },
            null,
            (s, _) => string.Join(", ", s.Select(kv => $"{kv.Key}={kv.Value}")));

        var customer = spy.LastState.Should().ContainSingle(kv => kv.Key == "Customer").Subject.Value;
        var projected = customer.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected["Email"].Should().Be("[REDACTED]");
        projected["PublicNote"].Should().Be("visible");
    }

    [Fact]
    public void Log_StructuredObject_PolicyMasksCategoryInLogs()
    {
        var policies = new SensitiveFlowPolicyRegistry();
        policies.ForCategory(DataCategory.Contact).MaskInLogs();

        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(
            spy,
            new DefaultSensitiveValueRedactor(),
            new SensitiveLoggingOptions { Policies = policies });

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Customer", new CustomerLogShape()),
            },
            null,
            (s, _) => string.Empty);

        var projected = spy.LastState.Single(kv => kv.Key == "Customer").Value
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected["Email"].Should().Be("m****@example.com");
        projected["TaxId"].Should().Be("[REDACTED]");
    }

    [Fact]
    public void Log_StructuredObject_ContextualLogActionCanOmit()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Customer", new ContextualLogShape()),
            },
            null,
            (s, _) => string.Empty);

        var projected = spy.LastState.Single(kv => kv.Key == "Customer").Value
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected.Should().NotContainKey("TaxId");
        projected["Email"].Should().Be("m****@example.com");
    }

    [Fact]
    public void Log_StructuredObject_AllowSensitiveLoggingPreservesValue()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Customer", new AllowedLogShape()),
            },
            null,
            (s, _) => string.Empty);

        var projected = spy.LastState.Single(kv => kv.Key == "Customer").Value
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected["Email"].Should().Be("maria@example.com");
        projected["TaxId"].Should().Be("[REDACTED]");
    }

    [Fact]
    public void Log_StructuredObject_ExplicitAttributesCoverOmitRedactAndMaskKinds()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Shape", new AttributeLogShape()),
            },
            null,
            (s, _) => string.Empty);

        var projected = spy.LastState.Single(kv => kv.Key == "Shape").Value
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected.Should().NotContainKey("Omitted");
        projected["Redacted"].Should().Be("[REDACTED]");
        projected["Phone"].Should().Be("(**) *****-**89");
        projected["Name"].Should().Be("M**** S****");
        projected["Code"].Should().Be("A***");
        projected["Single"].Should().Be("*");
        projected["Number"].Should().Be("[REDACTED]");
    }

    [Fact]
    public void Log_StructuredObject_ContextualMaskInfersPhoneNameGenericAndShortEmail()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Shape", new InferredMaskLogShape()),
            },
            null,
            (s, _) => string.Empty);

        var projected = spy.LastState.Single(kv => kv.Key == "Shape").Value
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        projected["PhoneNumber"].Should().Be("(**) *****-**89");
        projected["FullName"].Should().Be("M**** S****");
        projected["SecretCode"].Should().Be("A***");
        projected["Email"].Should().Be("a**");
        projected["Empty"].Should().Be(string.Empty);
    }

    [Fact]
    public void Log_StructuredObject_IgnoresNullStringValueTypeAndUnannotatedObjects()
    {
        var spy = new StateSpyLogger();
        var logger = new RedactingLogger(spy, new DefaultSensitiveValueRedactor());

        logger.Log(LogLevel.Information, new EventId(0),
            new List<KeyValuePair<string, object?>>
            {
                new("Null", null),
                new("String", "raw"),
                new("Number", 123),
                new("Object", new UnannotatedLogShape()),
            },
            null,
            (s, _) => string.Empty);

        spy.LastState.Single(kv => kv.Key == "Null").Value.Should().BeNull();
        spy.LastState.Should().Contain(kv => kv.Key == "String" && (string?)kv.Value == "raw");
        spy.LastState.Should().Contain(kv => kv.Key == "Number" && (int?)kv.Value == 123);
        spy.LastState.Single(kv => kv.Key == "Object").Value.Should().BeOfType<UnannotatedLogShape>();
    }

    private sealed class SpyLogger : ILogger
    {
        public string? LastMessage { get; private set; }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LastMessage = formatter(state, exception);
        }
    }

    private sealed class StateSpyLogger : ILogger
    {
        public IReadOnlyList<KeyValuePair<string, object?>> LastState { get; private set; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                LastState = pairs.ToArray();
            }
        }
    }

    private sealed class CustomerLogShape
    {
        [PersonalData(Category = DataCategory.Contact)]
        public string Email { get; set; } = "maria@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string TaxId { get; set; } = "12345678900";

        public string PublicNote { get; set; } = "visible";
    }

    private sealed class ContextualLogShape
    {
        [PersonalData(Category = DataCategory.Contact)]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string Email { get; set; } = "maria@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        [Redaction(Logs = OutputRedactionAction.Omit)]
        public string TaxId { get; set; } = "12345678900";
    }

    private sealed class AllowedLogShape
    {
        [PersonalData(Category = DataCategory.Contact)]
        [AllowSensitiveLogging("Diagnostic correlation value in restricted logs.")]
        public string Email { get; set; } = "maria@example.com";

        [SensitiveData(Category = SensitiveDataCategory.Other)]
        public string TaxId { get; set; } = "12345678900";
    }

    private sealed class AttributeLogShape
    {
        [PersonalData]
        [Omit]
        public string Omitted { get; set; } = "hide";

        [PersonalData]
        [Redact]
        public string Redacted { get; set; } = "secret";

        [PersonalData]
        [Mask(MaskKind.Phone)]
        public string Phone { get; set; } = "(11) 99999-8889";

        [PersonalData]
        [Mask(MaskKind.Name)]
        public string Name { get; set; } = "Maria Silva";

        [PersonalData]
        [Mask]
        public string Code { get; set; } = "ABCD";

        [PersonalData]
        [Mask]
        public string Single { get; set; } = "Z";

        [PersonalData]
        [Mask]
        public int Number { get; set; } = 42;

        public string Public { get; set; } = "visible";
    }

    private sealed class InferredMaskLogShape
    {
        [PersonalData]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string PhoneNumber { get; set; } = "(11) 99999-8889";

        [PersonalData]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string FullName { get; set; } = "Maria Silva";

        [PersonalData]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string SecretCode { get; set; } = "ABCD";

        [PersonalData]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string Email { get; set; } = "a@x";

        [PersonalData]
        [Redaction(Logs = OutputRedactionAction.Mask)]
        public string Empty { get; set; } = string.Empty;
    }

    private sealed class UnannotatedLogShape
    {
        public string Value { get; set; } = "plain";
    }
}
