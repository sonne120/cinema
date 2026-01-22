using Cinema.MasterNode.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cinema.MasterNode.UnitTests.Services;

public class MasterNodeWorkerTests
{
    private readonly Mock<IOutboxProcessor> _outboxProcessorMock;
    private readonly Mock<ILogger<MasterNodeWorker>> _loggerMock;

    public MasterNodeWorkerTests()
    {
        _outboxProcessorMock = new Mock<IOutboxProcessor>();
        _loggerMock = new Mock<ILogger<MasterNodeWorker>>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStartOutboxProcessor()
    {
        // Arrange
        var worker = new MasterNodeWorker(_outboxProcessorMock.Object, _loggerMock.Object);
        var cancellationTokenSource = new CancellationTokenSource();

        _outboxProcessorMock
            .Setup(x => x.StartProcessingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await worker.StartAsync(cancellationTokenSource.Token);

        // Allow some time for the background service to start
        await Task.Delay(100);

        cancellationTokenSource.Cancel();

        // Assert
        _outboxProcessorMock.Verify(
            x => x.StartProcessingAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        // Act
        var action = () => new MasterNodeWorker(_outboxProcessorMock.Object, _loggerMock.Object);

        // Assert
        action.Should().NotThrow();
    }
}
