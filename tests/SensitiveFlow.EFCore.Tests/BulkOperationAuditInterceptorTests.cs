using FluentAssertions;
using Moq;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SensitiveFlow.Core.Interfaces;
using SensitiveFlow.EFCore.Interceptors;

namespace SensitiveFlow.EFCore.Tests;

public sealed class BulkOperationAuditInterceptorTests
{
    private readonly Mock<IAuditStore> _auditStoreMock;
    private readonly Mock<IAuditContext> _auditContextMock;
    private readonly BulkOperationAuditInterceptor _interceptor;

    public BulkOperationAuditInterceptorTests()
    {
        _auditStoreMock = new Mock<IAuditStore>();
        _auditContextMock = new Mock<IAuditContext>();

        _auditContextMock.Setup(c => c.ActorId).Returns("test-actor");
        _auditContextMock.Setup(c => c.IpAddressToken).Returns("token-ip");

        _interceptor = new BulkOperationAuditInterceptor(_auditStoreMock.Object, _auditContextMock.Object);
    }

    [Fact]
    public void Constructor_ThrowsWhenAuditStoreNull()
    {
        var action = () => new BulkOperationAuditInterceptor(null!, _auditContextMock.Object);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsWhenAuditContextNull()
    {
        var action = () => new BulkOperationAuditInterceptor(_auditStoreMock.Object, null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CommandCreatedAsync_DetectsUpdateCommand()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns("UPDATE Users SET Email = 'new@example.com' WHERE Id = 1");

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        var result = await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        result.Should().Be(command.Object);
        _auditStoreMock.Verify(
            s => s.AppendAsync(
                It.Is<SensitiveFlow.Core.Models.AuditRecord>(r =>
                    r.DataSubjectId == "BULK_OPERATION" &&
                    r.Operation == SensitiveFlow.Core.Enums.AuditOperation.Update),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommandCreatedAsync_DetectsDeleteCommand()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns("DELETE FROM Users WHERE Status = 'Inactive'");

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        var result = await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        result.Should().Be(command.Object);
        _auditStoreMock.Verify(
            s => s.AppendAsync(
                It.Is<SensitiveFlow.Core.Models.AuditRecord>(r =>
                    r.Operation == SensitiveFlow.Core.Enums.AuditOperation.Delete),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommandCreatedAsync_IgnoresSelectCommand()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns("SELECT * FROM Users WHERE Id = 1");

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        var result = await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        result.Should().Be(command.Object);
        _auditStoreMock.Verify(
            s => s.AppendAsync(It.IsAny<SensitiveFlow.Core.Models.AuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommandCreatedAsync_HandlesNullCommandText()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns((string)null!);

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        var result = await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        result.Should().Be(command.Object);
        _auditStoreMock.Verify(
            s => s.AppendAsync(It.IsAny<SensitiveFlow.Core.Models.AuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommandCreatedAsync_IncludesActorAndIpInAuditRecord()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns("UPDATE Users SET Email = 'new@example.com'");

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        _auditStoreMock.Verify(
            s => s.AppendAsync(
                It.Is<SensitiveFlow.Core.Models.AuditRecord>(r =>
                    r.ActorId == "test-actor" &&
                    r.IpAddressToken == "token-ip"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommandCreatedAsync_CaseInsensitiveCommandDetection()
    {
        var command = new Mock<DbCommand>();
        command.Setup(c => c.CommandText).Returns("update users set email = 'new@example.com'");

        var eventData = new CommandEndEventData(
            null,
            null,
            null,
            command.Object,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        // Act
        await _interceptor.CommandCreatedAsync(eventData, command.Object);

        // Assert
        _auditStoreMock.Verify(
            s => s.AppendAsync(
                It.Is<SensitiveFlow.Core.Models.AuditRecord>(r =>
                    r.Operation == SensitiveFlow.Core.Enums.AuditOperation.Update),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
