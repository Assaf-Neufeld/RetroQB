using Raylib_cs;

namespace RetroQB.Audio;

public sealed class RetroMidiMusicController : IDisposable
{
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;
    private const float TempoBpm = 124f;
    private const int StepsPerBar = 8;
    private const int TotalBars = 16;
    private const int TotalSteps = StepsPerBar * TotalBars;

    private static readonly int[] BassPattern =
    [
        40, 0, 47, 0, 40, 0, 47, 0,
        36, 0, 43, 0, 36, 0, 43, 0,
        43, 0, 50, 0, 43, 0, 50, 0,
        38, 0, 45, 0, 38, 0, 45, 0
    ];

    private static readonly int[][] PadChords =
    [
        [64, 67, 71, 74],
        [60, 64, 67, 71],
        [55, 59, 62, 67],
        [57, 62, 64, 69]
    ];

    private readonly byte[] _wavData;
    private readonly Music _music;
    private readonly float _musicLengthSeconds;
    private float _currentVolume;
    private bool _disposed;

    public RetroMidiMusicController()
    {
        _wavData = BuildWaveFile();
        _music = Raylib.LoadMusicStreamFromMemory(".wav", _wavData);
        _musicLengthSeconds = Raylib.GetMusicTimeLength(_music);
        _currentVolume = 0f;

        Raylib.SetMusicVolume(_music, _currentVolume);
        Raylib.PlayMusicStream(_music);
    }

    public void Update(GameState state, bool isPaused, float dt)
    {
        if (_disposed)
        {
            return;
        }

        bool shouldPlayMenuMusic = state == GameState.MainMenu;

        if (shouldPlayMenuMusic && !Raylib.IsMusicStreamPlaying(_music))
        {
            Raylib.SeekMusicStream(_music, 0f);
            Raylib.PlayMusicStream(_music);
        }

        if (Raylib.IsMusicStreamPlaying(_music))
        {
            Raylib.UpdateMusicStream(_music);

            if (_musicLengthSeconds > 0f && Raylib.GetMusicTimePlayed(_music) >= _musicLengthSeconds - 0.005f)
            {
                Raylib.SeekMusicStream(_music, 0f);
            }
        }

        float targetVolume = GetTargetVolume(state, isPaused);
        _currentVolume = MoveTowards(_currentVolume, targetVolume, dt * 0.28f);
        Raylib.SetMusicVolume(_music, _currentVolume);

        if (!shouldPlayMenuMusic && _currentVolume <= 0.001f && Raylib.IsMusicStreamPlaying(_music))
        {
            Raylib.StopMusicStream(_music);
            Raylib.SeekMusicStream(_music, 0f);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Raylib.StopMusicStream(_music);
        Raylib.UnloadMusicStream(_music);
        _disposed = true;
    }

    private static byte[] BuildWaveFile()
    {
        short[] pcmSamples = BuildPcmSamples();
        int dataSize = pcmSamples.Length * sizeof(short);
        byte[] wav = new byte[44 + dataSize];

        WriteAscii(wav, 0, "RIFF");
        WriteInt32(wav, 4, 36 + dataSize);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt32(wav, 16, 16);
        WriteInt16(wav, 20, 1);
        WriteInt16(wav, 22, Channels);
        WriteInt32(wav, 24, SampleRate);
        WriteInt32(wav, 28, SampleRate * Channels * (BitsPerSample / 8));
        WriteInt16(wav, 32, (short)(Channels * (BitsPerSample / 8)));
        WriteInt16(wav, 34, BitsPerSample);
        WriteAscii(wav, 36, "data");
        WriteInt32(wav, 40, dataSize);

        Buffer.BlockCopy(pcmSamples, 0, wav, 44, dataSize);
        return wav;
    }

    private static short[] BuildPcmSamples()
    {
        int samplesPerStep = (int)(SampleRate * 30f / TempoBpm);
        int samplesPerBeat = samplesPerStep * 2;
        int samplesPerHalfStep = Math.Max(1, samplesPerStep / 2);
        int totalSamples = samplesPerStep * TotalSteps;
        short[] pcm = new short[totalSamples];
        float previousSmoothed = 0f;
        const float smoothing = 0.12f;

        for (int sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            float t = sampleIndex / (float)SampleRate;
            int stepIndex = (sampleIndex / samplesPerStep) % TotalSteps;
            float stepProgress = (sampleIndex % samplesPerStep) / (float)samplesPerStep;
            int barIndex = (stepIndex / StepsPerBar) % PadChords.Length;
            float beatProgress = (sampleIndex % samplesPerBeat) / (float)samplesPerBeat;
            float halfStepProgress = (sampleIndex % samplesPerHalfStep) / (float)samplesPerHalfStep;
            bool offBeatHat = ((sampleIndex / samplesPerHalfStep) & 1) == 1;

            float bass = RenderBass(t, BassPattern[stepIndex % BassPattern.Length], stepProgress);
            float pad = RenderPad(t, PadChords[barIndex], stepProgress);
            float kick = RenderKick(t, beatProgress);
            float hat = RenderHat(t, halfStepProgress, offBeatHat, sampleIndex);
            float air = SineWave(t, 0.18f) * 0.03f + SineWave(t, 0.07f) * 0.02f;

            float kickDuck = 1f - MathF.Exp(-beatProgress * 26f) * 0.14f;
            float musicalBed = (bass * 0.75f + pad * 0.33f + air) * kickDuck;

            float mixed = musicalBed + kick * 0.22f + hat * 0.07f;
            float shaped = MathF.Tanh(mixed * 1.02f) * 0.82f;
            float smoothed = previousSmoothed + smoothing * (shaped - previousSmoothed);
            previousSmoothed = smoothed;
            int pcmValue = (int)(smoothed * short.MaxValue * 0.64f);
            pcm[sampleIndex] = (short)Math.Clamp(pcmValue, short.MinValue, short.MaxValue);
        }

        return pcm;
    }

    private static float RenderBass(float t, int midiNote, float stepProgress)
    {
        if (midiNote <= 0)
        {
            return 0f;
        }

        float frequency = MidiToFrequency(midiNote);
        float triangle = TriangleWave(t, frequency);
        float sine = SineWave(t, frequency * 0.5f) * 0.24f;
        float envelope = GetEnvelope(stepProgress, 0.04f, 0.86f);
        return (triangle * 0.84f + sine) * envelope;
    }

    private static float RenderPad(float t, int[] chord, float stepProgress)
    {
        float envelope = 0.45f + GetEnvelope(stepProgress, 0.12f, 0.96f) * 0.35f;
        float total = 0f;

        foreach (int midiNote in chord)
        {
            float frequency = MidiToFrequency(midiNote);
            total += SineWave(t, frequency);
            total += SineWave(t, frequency * 1.0025f) * 0.28f;
        }

        return total / chord.Length * envelope;
    }

    private static float RenderKick(float t, float beatProgress)
    {
        float ampEnv = MathF.Exp(-beatProgress * 10f);
        float pitchEnv = MathF.Exp(-beatProgress * 7.5f);
        float frequency = 46f + pitchEnv * 88f;
        float tone = SineWave(t, frequency);
        float click = MathF.Exp(-beatProgress * 72f) * 0.08f;
        return tone * ampEnv * 0.72f + click;
    }

    private static float RenderHat(float t, float halfStepProgress, bool offBeatHat, int sampleIndex)
    {
        float envelope = offBeatHat ? MathF.Exp(-halfStepProgress * 28f) : MathF.Exp(-halfStepProgress * 46f) * 0.18f;
        float noise = HashNoise(sampleIndex * 0.071f);
        float shimmer = SineWave(t, 6200f) * 0.1f + SineWave(t, 4300f) * 0.06f;
        return (noise * 0.52f + shimmer) * envelope;
    }

    private static float GetTargetVolume(GameState state, bool isPaused)
    {
        if (state != GameState.MainMenu)
        {
            return 0f;
        }

        const float menuVolume = 0.08f;
        return isPaused ? menuVolume * 0.55f : menuVolume;
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(target - current) * maxDelta;
    }

    private static float GetEnvelope(float progress, float attackPortion, float releaseStart)
    {
        float attack = attackPortion <= 0f ? 1f : MathF.Min(1f, progress / attackPortion);
        float release = progress <= releaseStart ? 1f : 1f - ((progress - releaseStart) / MathF.Max(0.001f, 1f - releaseStart));
        return MathF.Max(0f, attack * release);
    }

    private static float MidiToFrequency(int midiNote)
    {
        return 440f * MathF.Pow(2f, (midiNote - 69) / 12f);
    }

    private static float SineWave(float t, float frequency)
    {
        return MathF.Sin(2f * MathF.PI * frequency * t);
    }

    private static float TriangleWave(float t, float frequency)
    {
        float phase = t * frequency;
        phase -= MathF.Floor(phase);
        return 1f - 4f * MathF.Abs(phase - 0.5f);
    }

    private static float HashNoise(float x)
    {
        float value = MathF.Sin(x * 12.9898f) * 43758.547f;
        float fract = value - MathF.Floor(value);
        return fract * 2f - 1f;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xff);
        buffer[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xff);
        buffer[offset + 1] = (byte)((value >> 8) & 0xff);
        buffer[offset + 2] = (byte)((value >> 16) & 0xff);
        buffer[offset + 3] = (byte)((value >> 24) & 0xff);
    }
}
