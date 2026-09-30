namespace AgeOfSakura.Game
{
    public enum AudioCue
    {
        ButtonTap,
        BuildingPlaced,
        ProductionStarted,
        ProductionCollected,
        CurrencyGain,
        InvalidAction,
        AmbientLoop
    }

    /// <summary>
    /// Audio is not part of Prototype 0.01; gameplay/UI only call this interface so sounds can be
    /// added later without touching them. Wire a real implementation in <see cref="GameRoot"/>.
    /// </summary>
    public interface IAudioService
    {
        void Play(AudioCue cue);
    }

    public sealed class NullAudioService : IAudioService
    {
        public void Play(AudioCue cue) { }
    }
}
