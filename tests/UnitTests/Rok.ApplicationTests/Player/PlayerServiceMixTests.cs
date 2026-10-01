using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Player;

public class PlayerServiceMixTests
{
    private const double TrackLength = 200;

    private const double MusicEnd = 198;

    private const double IncomingStart = 2;

    private const int SliderSeconds = 6;

    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IAppOptions> _appOptions = new();
    private readonly Mock<ICallDetectionService> _callDetection = new();
    private readonly Mock<IAlbumPicture> _albumPicture = new();
    private readonly Mock<IMixCueProvider> _cues = new();

    public PlayerServiceMixTests()
    {
        _engine.Setup(o => o.SetTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.QueueNextTrack(It.IsAny<TrackDto>(), It.IsAny<float>())).Returns(true);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _engine.SetupGet(o => o.Position).Returns(150);
        _engine.SetupGet(o => o.Length).Returns(TrackLength);
        _appOptions.SetupGet(o => o.CrossFade).Returns(true);
        _appOptions.SetupGet(o => o.MixMode).Returns(true);
        _appOptions.SetupGet(o => o.CrossfadeDurationSeconds).Returns(SliderSeconds);
        SetupCues(new OutroCues(MusicEnd, 0), new IntroCues(IncomingStart));
    }

    private void SetupCues(OutroCues? outro, IntroCues? intro)
    {
        _cues.Setup(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(outro);
        _cues.Setup(c => c.GetIntroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(intro);
    }

    private PlayerService BuildService() => new(_callDetection.Object, _engine.Object, _appOptions.Object, null, null, _albumPicture.Object, TimeProvider.System, new Messenger(), _cues.Object, NullLogger<PlayerService>.Instance);

    private static TrackDto BuildTrack(long id, long? albumId = null, int? trackNumber = null) => new() { Id = id, Title = $"t{id}", AlbumId = albumId, TrackNumber = trackNumber, Duration = (long)TrackLength };

    private PlayerService BuildLoadedService(params TrackDto[] tracks)
    {
        PlayerService sut = BuildService();
        sut.LoadPlaylist([.. tracks]);

        return sut;
    }

    private void RaiseCue() => _engine.Raise(m => m.OnTransitionCue += null, EventArgs.Empty);

    private void RaiseMediaAboutToEnd() => _engine.Raise(m => m.OnMediaAboutToEnd += null, _engine.Object, EventArgs.Empty);

    private void VerifyMixCrossfade(Times times) => _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), times);

    private void VerifyClassicCrossfade(Times times) => _engine.Verify(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), times);

    private void VerifyCueSet(Times times) => _engine.Verify(o => o.SetTransitionCue(It.IsAny<long>(), It.IsAny<double>()), times);

    [Fact(DisplayName = "mix_off_keeps_the_classic_crossfade")]
    public void Mix_off_keeps_the_classic_crossfade()
    {
        // Arrange
        _appOptions.SetupGet(o => o.MixMode).Returns(false);
        _engine.SetupGet(o => o.Position).Returns(195);
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        _cues.Verify(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _cues.Verify(c => c.GetIntroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyCueSet(Times.Never());
        _engine.Verify(o => o.ClearTransitionCue(), Times.Never);
        VerifyClassicCrossfade(Times.Once());
        VerifyMixCrossfade(Times.Never());
    }

    [Fact(DisplayName = "mix_is_never_armed_on_an_exclusive_output")]
    public void Mix_is_never_armed_on_an_exclusive_output()
    {
        // Arrange
        _appOptions.SetupGet(o => o.OutputMode).Returns(EAudioOutputMode.Exclusive);

        // Act
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Assert
        VerifyCueSet(Times.Never());
        _cues.Verify(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "consecutive_album_tracks_stay_gapless_in_mix_mode")]
    public void Consecutive_album_tracks_stay_gapless_in_mix_mode()
    {
        // Arrange
        BuildLoadedService(BuildTrack(1, albumId: 10, trackNumber: 1), BuildTrack(2, albumId: 10, trackNumber: 2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        VerifyCueSet(Times.Never());
        _engine.Verify(o => o.QueueNextTrack(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>()), Times.Once);
        VerifyMixCrossfade(Times.Never());
    }

    [Fact(DisplayName = "mix_arms_the_cue_at_the_planned_start")]
    public void Mix_arms_the_cue_at_the_planned_start()
    {
        // Arrange
        // Act
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Assert
        _engine.Verify(o => o.SetTransitionCue(1, MusicEnd - SliderSeconds), Times.Once);
    }

    [Fact(DisplayName = "cue_starts_the_mix_with_the_planned_duration_and_incoming_start")]
    public void Cue_starts_the_mix_with_the_planned_duration_and_incoming_start()
    {
        // Arrange
        _engine.SetupGet(o => o.Position).Returns(MusicEnd - SliderSeconds);
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        RaiseCue();

        // Assert
        _engine.Verify(o => o.CrossfadeToAsync(It.Is<TrackDto>(t => t.Id == 2), It.IsAny<float>(), SliderSeconds, IncomingStart, It.IsAny<CancellationToken>()), Times.Once);
        VerifyClassicCrossfade(Times.Never());
        Assert.Equal(2, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "about_to_end_waits_for_the_cue_when_a_plan_is_armed")]
    public void About_to_end_waits_for_the_cue_when_a_plan_is_armed()
    {
        // Arrange
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        VerifyClassicCrossfade(Times.Never());
        VerifyMixCrossfade(Times.Never());
    }

    [Fact(DisplayName = "about_to_end_is_ignored_while_a_mix_runs")]
    public void About_to_end_is_ignored_while_a_mix_runs()
    {
        // Arrange
        TaskCompletionSource running = new();
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>())).Returns(running.Task);
        _engine.SetupGet(o => o.Position).Returns(MusicEnd - SliderSeconds);
        BuildLoadedService(BuildTrack(1), BuildTrack(2));
        RaiseCue();

        // Act
        RaiseMediaAboutToEnd();
        RaiseCue();

        // Assert
        VerifyMixCrossfade(Times.Once());
        VerifyClassicCrossfade(Times.Never());

        running.SetResult();
    }

    [Fact(DisplayName = "missing_cues_fall_back_to_the_classic_crossfade")]
    public void Missing_cues_fall_back_to_the_classic_crossfade()
    {
        // Arrange
        SetupCues(null, null);
        _engine.SetupGet(o => o.Position).Returns(195);
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        VerifyCueSet(Times.Never());
        VerifyClassicCrossfade(Times.Once());
    }

    [Fact(DisplayName = "intro_only_cues_fall_back_to_the_classic_crossfade")]
    public void Intro_only_cues_fall_back_to_the_classic_crossfade()
    {
        // Arrange
        SetupCues(null, new IntroCues(IncomingStart));
        _engine.SetupGet(o => o.Position).Returns(195);
        BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        RaiseMediaAboutToEnd();

        // Assert
        VerifyCueSet(Times.Never());
        VerifyClassicCrossfade(Times.Once());
    }

    [Fact(DisplayName = "provider_exception_falls_back_without_throwing")]
    public void Provider_exception_falls_back_without_throwing()
    {
        // Arrange
        _cues.Setup(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        _engine.SetupGet(o => o.Position).Returns(195);
        PlayerService sut = BuildService();

        // Act
        Exception? loadError = Record.Exception(() => sut.LoadPlaylist([BuildTrack(1), BuildTrack(2)]));
        Exception? endError = Record.Exception(RaiseMediaAboutToEnd);

        // Assert
        Assert.Null(loadError);
        Assert.Null(endError);
        VerifyCueSet(Times.Never());
        VerifyClassicCrossfade(Times.Once());
    }

    [Fact(DisplayName = "changing_the_next_track_reanalyses_its_intro")]
    public void Changing_the_next_track_reanalyses_its_intro()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));
        _engine.Invocations.Clear();
        _cues.Invocations.Clear();

        // Act
        sut.InsertTracksToPlaylist([BuildTrack(3)], 1);

        // Assert
        _engine.Verify(o => o.ClearTransitionCue(), Times.AtLeastOnce);
        _cues.Verify(c => c.GetIntroAsync(It.Is<TrackDto>(t => t.Id == 3), It.IsAny<CancellationToken>()), Times.Once);
        _engine.Verify(o => o.SetTransitionCue(1, MusicEnd - SliderSeconds), Times.Once);
    }

    [Fact(DisplayName = "shuffle_that_changes_the_next_track_rearms_the_mix")]
    public void Shuffle_that_changes_the_next_track_rearms_the_mix()
    {
        // Arrange
        List<TrackDto> tracks = [BuildTrack(1)];
        tracks.AddRange(Enumerable.Range(2, 30).Select(id => BuildTrack(id)));
        PlayerService sut = BuildService();
        sut.LoadPlaylist(tracks);
        long nextBefore = sut.GetQueue()[0].Id;
        _engine.Invocations.Clear();

        // Act
        bool nextChanged = false;

        for (int attempt = 0; attempt < 20 && !nextChanged; attempt++)
        {
            sut.ShuffleTracks();
            nextChanged = sut.GetQueue()[0].Id != nextBefore;
        }

        // Assert
        Assert.True(nextChanged);
        _engine.Verify(o => o.ClearTransitionCue(), Times.AtLeastOnce);
        _engine.Verify(o => o.SetTransitionCue(1, MusicEnd - SliderSeconds), Times.AtLeastOnce);
    }

    [Fact(DisplayName = "stale_cue_after_a_queue_change_is_ignored")]
    public void Stale_cue_after_a_queue_change_is_ignored()
    {
        // Arrange
        SetupCues(new OutroCues(MusicEnd, 0), new IntroCues(IncomingStart));
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));
        SetupCues(null, null);
        sut.InsertTracksToPlaylist([BuildTrack(3)], 1);

        // Act
        RaiseCue();

        // Assert
        VerifyMixCrossfade(Times.Never());
        Assert.Equal(1, sut.CurrentTrack?.Id);
    }

    [Fact(DisplayName = "seek_keeps_the_mix_plan")]
    public async Task Seek_keeps_the_mix_plan()
    {
        // Arrange
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));
        TaskCompletionSource seeked = new();
        _engine.Setup(o => o.SetPosition(It.IsAny<double>())).Callback(() => seeked.SetResult());
        _engine.Invocations.Clear();
        _cues.Invocations.Clear();

        // Act
        sut.Position = 30;
        await seeked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _engine.Verify(o => o.ClearTransitionCue(), Times.Never);
        _cues.Verify(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _cues.Verify(c => c.GetIntroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "pause_cancels_a_running_mix")]
    public void Pause_cancels_a_running_mix()
    {
        // Arrange
        CancellationToken captured = default;
        TaskCompletionSource running = new();
        _engine.Setup(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Callback<TrackDto, float, double, double, CancellationToken>((_, _, _, _, token) => captured = token)
            .Returns(running.Task);
        _engine.SetupGet(o => o.Position).Returns(MusicEnd - SliderSeconds);
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));
        RaiseCue();

        // Act
        sut.Pause();

        // Assert
        Assert.True(captured.CanBeCanceled);
        Assert.True(captured.IsCancellationRequested);

        running.SetResult();
    }

    [Fact(DisplayName = "stopping_the_player_drops_the_mix_plan")]
    public void Stopping_the_player_drops_the_mix_plan()
    {
        // Arrange
        _engine.SetupGet(o => o.Position).Returns(MusicEnd - SliderSeconds);
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));
        sut.Stop(true);

        // Act
        RaiseCue();

        // Assert
        VerifyMixCrossfade(Times.Never());
    }

    [Fact(DisplayName = "plan_of_a_superseded_preparation_is_not_stored")]
    public void Plan_of_a_superseded_preparation_is_not_stored()
    {
        // Arrange
        TaskCompletionSource<OutroCues?> staleOutro = new();
        _cues.SetupSequence(c => c.GetOutroAsync(It.IsAny<TrackDto>(), It.IsAny<CancellationToken>()))
            .Returns(staleOutro.Task)
            .ReturnsAsync(new OutroCues(MusicEnd, 0));
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2));

        // Act
        sut.IsLoopingEnabled = true;
        staleOutro.SetResult(new OutroCues(MusicEnd - 8, 0));

        // Assert
        _engine.Verify(o => o.SetTransitionCue(1, MusicEnd - 8 - SliderSeconds), Times.Never);
        _engine.Verify(o => o.SetTransitionCue(1, MusicEnd - SliderSeconds), Times.Once);
    }

    [Fact(DisplayName = "cancelled_crossfade_does_not_reset_the_running_flag_of_the_next_one")]
    public void Cancelled_crossfade_does_not_reset_the_running_flag_of_the_next_one()
    {
        // Arrange
        TaskCompletionSource first = new();
        TaskCompletionSource second = new();
        _appOptions.SetupGet(o => o.MixMode).Returns(false);
        _engine.SetupGet(o => o.Position).Returns(195);
        _engine.SetupSequence(o => o.CrossfadeToAsync(It.IsAny<TrackDto>(), It.IsAny<float>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(first.Task)
            .Returns(second.Task);
        PlayerService sut = BuildLoadedService(BuildTrack(1), BuildTrack(2), BuildTrack(3), BuildTrack(4), BuildTrack(5));
        RaiseMediaAboutToEnd();
        sut.Next();
        RaiseMediaAboutToEnd();

        // Act
        first.SetResult();
        RaiseMediaAboutToEnd();

        // Assert
        VerifyClassicCrossfade(Times.Exactly(2));

        second.SetResult();
    }
}