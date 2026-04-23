using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Player;

namespace Rok.ApplicationTests;

public class PlayerSleepModeServiceTests
{
    private readonly Mock<IPlayerService> _mockPlayer;
    private readonly FakeTimeProvider _fakeTime;
    private readonly PlayerSleepModeService _service;

    public PlayerSleepModeServiceTests()
    {
        _mockPlayer = new Mock<IPlayerService>();
        _fakeTime = new FakeTimeProvider();
        _service = new PlayerSleepModeService(_mockPlayer.Object, _fakeTime);
    }

    [Fact]
    public void StartSleepTimer_ShouldNotStart_WhenMinutesIsZeroOrNegative()
    {
        _service.StartSleepTimer(0);

        Assert.False(_service.IsSleepTimerActive);
    }

    [Fact]
    public void StartSleepTimer_ShouldActivateTimer()
    {
        _service.StartSleepTimer(5);

        Assert.True(_service.IsSleepTimerActive);
    }

    [Fact]
    public void StopPlayer_ShouldBeCalled_WhenTimerExpires()
    {
        _service.StartSleepTimer(1);

        _fakeTime.Advance(TimeSpan.FromMinutes(1));

        _mockPlayer.Verify(p => p.Stop(true), Times.Once);
        Assert.False(_service.IsSleepTimerActive);
    }

    [Fact]
    public void StopPlayer_ShouldNotBeCalled_BeforeTimerExpires()
    {
        _service.StartSleepTimer(5);

        _fakeTime.Advance(TimeSpan.FromMinutes(4));

        _mockPlayer.Verify(p => p.Stop(true), Times.Never);
        Assert.True(_service.IsSleepTimerActive);
    }

    [Fact]
    public void StopSleepTimer_ShouldDeactivateTimer()
    {
        _service.StartSleepTimer(5);
        _service.StopSleepTimer();

        Assert.False(_service.IsSleepTimerActive);
        _mockPlayer.Verify(p => p.Stop(It.IsAny<bool>()), Times.Never);
    }
}