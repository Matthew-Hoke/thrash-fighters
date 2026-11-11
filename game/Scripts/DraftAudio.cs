using System;
using System.Collections.Generic;
using Godot;
using ThrashFighters.Sim;

namespace ThrashFighters;

internal partial class DraftAudio : Node
{
    private const int Rate = 22050;
    private readonly Dictionary<EventKind, AudioStreamWav> _tones = new();
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[5];
    private AudioStreamPlayer _victory = null!;
    private int _next;

    public override void _Ready()
    {
        _tones[EventKind.Jump] = Tone(0.075, 240, 510, 0.03);
        _tones[EventKind.Dodge] = Tone(0.09, 480, 220, 0.48);
        _tones[EventKind.Land] = Tone(0.06, 130, 70, 0.64);
        _tones[EventKind.Cancel] = Tone(0.12, 680, 940, 0.01);
        _tones[EventKind.Hit] = Tone(0.14, 177, 62, 0.48);
        _tones[EventKind.Block] = Tone(0.095, 740, 328, 0.24);
        _tones[EventKind.Grab] = Tone(0.085, 320, 188, 0.27);
        _tones[EventKind.Knockout] = Tone(0.31, 380, 72, 0.58);
        for (int i = 0; i < _voices.Length; i++)
        {
            var voice = new AudioStreamPlayer { VolumeDb = -13f };
            _voices[i] = voice;
            AddChild(voice);
        }
        _victory = new AudioStreamPlayer { VolumeDb = -12f, Stream = VictoryFanfare() };
        AddChild(_victory);
    }

    public void PlayVictory() => _victory.Play();
    public void StopVictory() => _victory.Stop();

    public void Play(EventKind kind)
    {
        AudioStreamPlayer voice = _voices[_next++ % _voices.Length];
        voice.Stream = _tones[kind];
        voice.Play();
    }

    private static AudioStreamWav Tone(double duration, double start, double end, double noise)
    {
        int samples = (int)(duration * Rate);
        var bytes = new byte[samples * 2];
        var random = new Random(19);
        double phase = 0;
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / samples;
            double envelope = Math.Min(1, t * 32) * (1 - t) * (1 - t);
            phase += (start + (end - start) * t) * Math.Tau / Rate;
            double sample = Math.Sin(phase) * (1 - noise) + (random.NextDouble() * 2 - 1) * noise;
            short value = (short)(sample * envelope * short.MaxValue);
            bytes[i * 2] = (byte)value;
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Data = bytes };
    }

    private static AudioStreamWav VictoryFanfare()
    {
        double[] notes = { 523.25, 659.25, 783.99, 1046.50 };
        int noteLength = Rate / 6;
        var data = new byte[noteLength * (notes.Length + 1) * 2];
        for (int note = 0; note < notes.Length; note++)
        {
            int duration = note == notes.Length - 1 ? noteLength * 2 : noteLength;
            for (int i = 0; i < duration; i++)
            {
                double phase = i * notes[note] * Math.Tau / Rate;
                double envelope = Math.Min(1, i / 120.0) * Math.Pow(1 - (double)i / duration, 1.4);
                short value = (short)((Math.Sin(phase) + 0.25 * Math.Sin(phase * 2)) * envelope * 22000);
                int offset = (note * noteLength + i) * 2;
                data[offset] = (byte)value;
                data[offset + 1] = (byte)(value >> 8);
            }
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Data = data };
    }
}
