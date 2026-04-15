namespace Rok.Application.Player;

public interface IPlayerSleepModeService
{
    bool IsSleepTimerActive { get; }

    event EventHandler<bool> SleepTimerStateChanged;

    int GetRemainingSleepTimeInSeconds();

    void StartSleepTimer(int minutes);

    void StopSleepTimer();
}