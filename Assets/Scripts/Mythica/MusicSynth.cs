using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // Generates every creature's looping song procedurally, so the game ships with no audio files.
    // All loops share one tempo, length and chord progression (Am | F | C | G), so any mix of
    // creatures stays in time and in key.
    public static class MusicSynth
    {
        public const int SampleRate = 32000;
        public const float Bpm = 100f;
        public const int StepsPerBar = 16;
        public const int Bars = 4;
        public const int Steps = StepsPerBar * Bars;
        public const double StepSeconds = 60.0 / Bpm / 4.0;
        public const double LoopSeconds = StepSeconds * Steps;
        public static readonly int LoopSamples = (int)Math.Round(LoopSeconds * SampleRate);

        static readonly int[] Roots = { 45, 41, 48, 43 };            // A2 F2 C3 G2
        static readonly int[][] Chords =
        {
            new[] { 57, 60, 64 },   // Am
            new[] { 53, 57, 60 },   // F
            new[] { 60, 64, 67 },   // C
            new[] { 55, 59, 62 },   // G
        };

        public class Song
        {
            public AudioClip Clip;
            public float[] Onsets;   // per step: 0 = silent, otherwise note velocity (drives the bounce)
        }

        struct Note
        {
            public int Step;
            public float Len;   // in steps
            public int Midi;    // ignored by drums
            public float Vel;
            public Note(int step, float len, int midi, float vel) { Step = step; Len = len; Midi = midi; Vel = vel; }
        }

        struct Env
        {
            public float Attack, Decay, Sustain, Release;
            public Env(float a, float d, float s, float r) { Attack = a; Decay = d; Sustain = s; Release = r; }

            float Held(float t)
            {
                if (t < Attack) return t / Attack;
                return Sustain + (1f - Sustain) * Mathf.Exp(-(t - Attack) / Decay);
            }

            public float At(float t, float dur)
            {
                if (t < dur) return Held(t);
                float r = 1f - (t - dur) / Release;
                return r <= 0f ? 0f : Held(dur) * r;
            }
        }

        static readonly Dictionary<string, Song> cache = new Dictionary<string, Song>();
        static readonly Dictionary<Magic, float[]> tables = new Dictionary<Magic, float[]>();
        const int TableSize = 2048;

        public static Song Get(Species s, Magic m)
        {
            string key = MythicaDB.VariantKey(s.Id, m);
            Song song;
            if (cache.TryGetValue(key, out song)) return song;

            var notes = Pattern(s.Part);
            var buf = new float[LoopSamples];
            var rng = new System.Random(key.GetHashCode());
            foreach (var n in notes) Render(buf, s.Part, m, n, rng);
            Normalize(buf, PartLevel(s.Part));

            var clip = AudioClip.Create(key, LoopSamples, 1, SampleRate, false);
            clip.SetData(buf, 0);

            var onsets = new float[Steps];
            foreach (var n in notes) onsets[n.Step % Steps] = Mathf.Max(onsets[n.Step % Steps], n.Vel);

            song = new Song { Clip = clip, Onsets = onsets };
            cache[key] = song;
            return song;
        }

        // ---------------------------------------------------------------- patterns

        static int Bar(int step) { return (step / StepsPerBar) % Bars; }

        static List<Note> Pattern(Part part)
        {
            var n = new List<Note>();
            switch (part)
            {
                case Part.Kick:
                    for (int b = 0; b < Bars; b++)
                    {
                        n.Add(new Note(b * 16 + 0, 1, 0, 1f));
                        n.Add(new Note(b * 16 + 8, 1, 0, 0.9f));
                        if (b % 2 == 1) n.Add(new Note(b * 16 + 14, 1, 0, 0.6f));
                    }
                    break;
                case Part.Snare:
                    for (int b = 0; b < Bars; b++)
                    {
                        n.Add(new Note(b * 16 + 4, 1, 0, 0.9f));
                        n.Add(new Note(b * 16 + 12, 1, 0, 1f));
                    }
                    n.Add(new Note(63, 1, 0, 0.5f));
                    break;
                case Part.Hat:
                    for (int i = 0; i < Steps; i += 2) n.Add(new Note(i, 1, 0, i % 4 == 2 ? 0.9f : 0.5f));
                    n.Add(new Note(61, 1, 0, 0.4f));
                    n.Add(new Note(63, 1, 0, 0.6f));
                    break;
                case Part.Tom:
                    for (int b = 0; b < Bars; b++) n.Add(new Note(b * 16 + 6, 1, 0, 0.7f));
                    n.Add(new Note(60, 1, 0, 0.9f)); n.Add(new Note(61, 1, 0, 0.8f));
                    n.Add(new Note(62, 1, 0, 0.9f)); n.Add(new Note(63, 1, 0, 1f));
                    break;
                case Part.Click:
                    for (int b = 0; b < Bars; b++)
                        foreach (int s in new[] { 3, 6, 11, 14 }) n.Add(new Note(b * 16 + s, 1, 0, s == 6 ? 1f : 0.7f));
                    break;
                case Part.Bass:
                    for (int b = 0; b < Bars; b++) n.Add(new Note(b * 16, 15, Roots[b], 1f));
                    break;
                case Part.SubBass:
                    for (int b = 0; b < Bars; b++)
                    {
                        int r = Roots[b] - 12;
                        n.Add(new Note(b * 16 + 0, 3, r, 1f));
                        n.Add(new Note(b * 16 + 3, 3, r, 0.8f));
                        n.Add(new Note(b * 16 + 6, 2, r + 12, 0.7f));
                        n.Add(new Note(b * 16 + 10, 4, r, 0.9f));
                    }
                    break;
                case Part.WalkBass:
                    for (int b = 0; b < Bars; b++)
                    {
                        int r = Roots[b];
                        int third = b == 0 ? 3 : 4;   // minor third over Am, major over F, C, G
                        int[] walk = { r, r + 12, r + 7, r + 12, r, r + 7, r + third, r + 2 };
                        for (int i = 0; i < 8; i++) n.Add(new Note(b * 16 + i * 2, 1.6f, walk[i], i % 2 == 0 ? 1f : 0.75f));
                    }
                    break;
                case Part.Riff:
                    for (int b = 0; b < Bars; b++)
                    {
                        int r = Roots[b];
                        foreach (int s in new[] { 0, 2, 3, 6, 8, 10, 11, 14 })
                            n.Add(new Note(b * 16 + s, 1.5f, (s == 6 || s == 14) ? r + 12 : r, s % 8 == 0 ? 1f : 0.7f));
                    }
                    break;
                case Part.Pad:
                    for (int b = 0; b < Bars; b++)
                        foreach (int t in Chords[b]) n.Add(new Note(b * 16, 15, t, 0.7f));
                    break;
                case Part.LowPad:
                    for (int b = 0; b < Bars; b++)
                    {
                        n.Add(new Note(b * 16, 15, Roots[b] + 12, 0.8f));
                        n.Add(new Note(b * 16, 15, Roots[b] + 19, 0.6f));
                    }
                    break;
                case Part.Arp:
                    for (int b = 0; b < Bars; b++)
                    {
                        var c = Chords[b];
                        int[] seq = { c[0], c[1], c[2], c[0] + 12, c[2], c[1], c[0], c[1] };
                        for (int i = 0; i < 8; i++) n.Add(new Note(b * 16 + i * 2, 1.5f, seq[i], i == 0 ? 1f : 0.75f));
                    }
                    break;
                case Part.HighArp:
                    for (int b = 0; b < Bars; b++)
                    {
                        var c = Chords[b];
                        int[] seq = { c[0] + 12, c[1] + 12, c[2] + 12, c[1] + 12 };
                        foreach (int s in new[] { 0, 3, 6, 8, 11, 14 })
                            n.Add(new Note(b * 16 + s, 1f, seq[s % 4], 0.8f));
                    }
                    break;
                case Part.Harp:
                    for (int b = 0; b < Bars; b++)
                    {
                        var c = Chords[b];
                        int[] gliss = { c[0], c[1], c[2], c[0] + 12, c[1] + 12, c[2] + 12 };
                        for (int i = 0; i < gliss.Length; i++) n.Add(new Note(b * 16 + i, 4f, gliss[i], 0.6f + i * 0.07f));
                        n.Add(new Note(b * 16 + 10, 4f, c[2] + 12, 0.7f));
                    }
                    break;
                case Part.Stabs:
                    // Three heads, three stabs per bar.
                    for (int b = 0; b < Bars; b++)
                        foreach (int s in new[] { 0, 3, 10 })
                            foreach (int t in Chords[b]) n.Add(new Note(b * 16 + s, 1f, t, s == 0 ? 1f : 0.8f));
                    break;
                case Part.Skank:
                    for (int b = 0; b < Bars; b++)
                        foreach (int s in new[] { 4, 12 })
                            foreach (int t in Chords[b]) n.Add(new Note(b * 16 + s, 1.2f, t + 12, 0.8f));
                    break;
                case Part.Bells:
                    for (int b = 0; b < Bars; b++)
                    {
                        var c = Chords[b];
                        n.Add(new Note(b * 16 + 0, 8, c[2] + 12, 0.9f));
                        n.Add(new Note(b * 16 + 10, 6, c[0] + 24, 0.7f));
                    }
                    break;
                case Part.Counter:
                    for (int b = 0; b < Bars; b++)
                    {
                        var c = Chords[b];
                        n.Add(new Note(b * 16 + 2, 1.5f, c[1] + 12, 0.8f));
                        n.Add(new Note(b * 16 + 7, 1.5f, c[2] + 12, 0.7f));
                        n.Add(new Note(b * 16 + 10, 1.5f, c[1] + 12, 0.8f));
                        n.Add(new Note(b * 16 + 15, 1f, c[0] + 12, 0.6f));
                    }
                    break;
                case Part.Lead:
                    AddMelody(n, new[]
                    {
                        0, 4, 69,  4, 2, 72,  6, 2, 74,  8, 6, 76,  14, 2, 74,
                        16, 4, 72, 20, 4, 69, 24, 2, 72, 26, 2, 69, 28, 4, 67,
                        32, 2, 67, 34, 2, 69, 36, 4, 72, 40, 4, 76, 44, 4, 79,
                        48, 6, 74, 54, 2, 72, 56, 4, 71, 60, 4, 74,
                    });
                    break;
                case Part.HighLead:
                    AddMelody(n, new[]
                    {
                        12, 2, 84, 14, 2, 81,
                        16, 6, 84, 22, 2, 81, 24, 8, 79,
                        44, 2, 88, 46, 2, 86,
                        48, 4, 86, 52, 4, 83, 56, 8, 79,
                    });
                    break;
            }
            return n;
        }

        static void AddMelody(List<Note> n, int[] triples)
        {
            for (int i = 0; i + 2 < triples.Length; i += 3)
                n.Add(new Note(triples[i], triples[i + 1] - 0.3f, triples[i + 2], 0.85f));
        }

        static float PartLevel(Part p)
        {
            switch (p)
            {
                case Part.Kick: return 0.8f;
                case Part.Bass: case Part.SubBass: case Part.WalkBass: return 0.6f;
                case Part.Pad: case Part.LowPad: return 0.35f;
                case Part.Hat: case Part.Click: return 0.3f;
                case Part.Lead: case Part.HighLead: return 0.4f;
                default: return 0.45f;
            }
        }

        static Env PartEnv(Part p)
        {
            switch (p)
            {
                case Part.Bass: case Part.SubBass: return new Env(0.01f, 0.6f, 0.75f, 0.12f);
                case Part.WalkBass: return new Env(0.005f, 0.2f, 0.5f, 0.06f);
                case Part.Riff: return new Env(0.003f, 0.12f, 0.2f, 0.06f);
                case Part.Pad: case Part.LowPad: return new Env(0.35f, 1.5f, 0.8f, 0.7f);
                case Part.Bells: return new Env(0.002f, 0.9f, 0f, 0.6f);
                case Part.Harp: return new Env(0.002f, 0.5f, 0f, 0.4f);
                case Part.Lead: case Part.HighLead: return new Env(0.03f, 0.4f, 0.7f, 0.15f);
                case Part.Stabs: case Part.Skank: return new Env(0.003f, 0.1f, 0.1f, 0.05f);
                default: return new Env(0.003f, 0.18f, 0f, 0.1f);   // plucks
            }
        }

        // ---------------------------------------------------------------- rendering

        static float Hz(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        static void Render(float[] buf, Part part, Magic m, Note n, System.Random rng)
        {
            int start = (int)Math.Round(n.Step * StepSeconds * SampleRate);
            float dur = (float)(n.Len * StepSeconds);
            float pm = DrumPitch(m);
            switch (part)
            {
                case Part.Kick: Kick(buf, start, n.Vel, pm, rng); return;
                case Part.Snare: Snare(buf, start, n.Vel, pm, m, rng); return;
                case Part.Hat: Hat(buf, start, n.Vel, m, rng); return;
                case Part.Tom: Tom(buf, start, n.Vel, pm, n.Step); return;
                case Part.Click: Click(buf, start, n.Vel, pm, rng); return;
            }
            bool vibrato = part == Part.Lead || part == Part.HighLead || part == Part.LowPad;
            Tone(buf, start, dur, Hz(n.Midi), n.Vel, PartEnv(part), m, vibrato);
        }

        static void Add(float[] buf, int index, float v)
        {
            buf[index % buf.Length] += v;   // wrap tails around so the loop is seamless
        }

        static void Tone(float[] buf, int start, float dur, float hz, float vel, Env env, Magic m, bool vibrato)
        {
            float[] table = Table(m);
            int total = (int)((dur + env.Release) * SampleRate);
            double phase = 0, phase2 = 0;
            float detune = m == Magic.Shadow ? 1.008f : 1f;
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / SampleRate;
                float e = env.At(t, dur);
                if (e <= 0f) continue;
                float f = hz;
                if (vibrato && t > 0.25f) f *= 1f + 0.005f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                phase += f / SampleRate;
                float s = Lookup(table, phase);
                if (m == Magic.Shadow)
                {
                    phase2 += f * detune / SampleRate;
                    s = 0.6f * (s + Lookup(table, phase2));
                }
                else if (m == Magic.Light)
                {
                    // Inharmonic bell partial that fades fast.
                    s += 0.45f * Mathf.Sin((float)(2.0 * Math.PI * phase * 3.5)) * Mathf.Exp(-t * 7f);
                }
                Add(buf, start + i, s * e * vel);
            }
        }

        static float DrumPitch(Magic m)
        {
            switch (m)
            {
                case Magic.Fire: return 1.1f;
                case Magic.Water: return 0.85f;
                case Magic.Earth: return 0.75f;
                case Magic.Sky: return 1.3f;
                case Magic.Light: return 1.5f;
                default: return 0.65f;
            }
        }

        static float Noise(System.Random rng) { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        static void Kick(float[] buf, int start, float vel, float pm, System.Random rng)
        {
            int n = (int)(0.45f * SampleRate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = 45f * pm + 150f * pm * Mathf.Exp(-t * 25f);
                phase += f / SampleRate;
                float s = Mathf.Sin((float)(2.0 * Math.PI * phase)) * Mathf.Exp(-t * 7f);
                s += Noise(rng) * 0.25f * Mathf.Exp(-t * 200f);
                Add(buf, start + i, s * vel);
            }
        }

        static void Snare(float[] buf, int start, float vel, float pm, Magic m, System.Random rng)
        {
            int n = (int)(0.3f * SampleRate);
            float noiseDecay = m == Magic.Shadow ? 8f : m == Magic.Water ? 20f : 14f;
            float noiseAmt = m == Magic.Water ? 0.4f : 0.8f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float tone = Mathf.Sin(2f * Mathf.PI * 190f * pm * t) * Mathf.Exp(-t * 20f) * 0.6f;
                float noise = Noise(rng) * Mathf.Exp(-t * noiseDecay) * noiseAmt;
                Add(buf, start + i, (tone + noise) * vel);
            }
        }

        static void Hat(float[] buf, int start, float vel, Magic m, System.Random rng)
        {
            int n = (int)(0.15f * SampleRate);
            float decay = m == Magic.Shadow ? 18f : m == Magic.Earth ? 35f : 50f;
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float x = Noise(rng);
                float hp = x - prev;   // crude high-pass for a sizzly tick
                prev = x;
                Add(buf, start + i, hp * 0.5f * Mathf.Exp(-t * decay) * vel);
            }
        }

        static void Tom(float[] buf, int start, float vel, float pm, int step)
        {
            int n = (int)(0.4f * SampleRate);
            float basePitch = step >= 60 ? 140f - (step - 60) * 15f : 120f;   // descending fill
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = basePitch * pm + 80f * pm * Mathf.Exp(-t * 12f);
                phase += f / SampleRate;
                Add(buf, start + i, Mathf.Sin((float)(2.0 * Math.PI * phase)) * Mathf.Exp(-t * 7f) * vel);
            }
        }

        static void Click(float[] buf, int start, float vel, float pm, System.Random rng)
        {
            int n = (int)(0.08f * SampleRate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float s = Mathf.Sin(2f * Mathf.PI * 850f * pm * t) * Mathf.Exp(-t * 60f);
                s += Noise(rng) * 0.4f * Mathf.Exp(-t * 90f);
                Add(buf, start + i, s * vel);
            }
        }

        static float Lookup(float[] table, double phase)
        {
            double p = (phase - Math.Floor(phase)) * TableSize;
            int i = (int)p;
            float frac = (float)(p - i);
            return table[i] + (table[(i + 1) % TableSize] - table[i]) * frac;
        }

        // One cycle per magic type: this is what gives each magic its voice.
        static float[] Table(Magic m)
        {
            float[] t;
            if (tables.TryGetValue(m, out t)) return t;
            float[] h;   // harmonic amplitudes, index 0 = fundamental
            switch (m)
            {
                case Magic.Fire: h = Harmonics(12, k => 1f / k); break;                                   // bright saw
                case Magic.Water: h = new[] { 1f, 0.2f, 0.05f }; break;                                     // soft, round
                case Magic.Earth: h = Harmonics(9, k => k % 2 == 1 ? 1f / (k * k) : 0.05f / k); break;     // warm triangle
                case Magic.Sky: h = Harmonics(11, k => k % 2 == 1 ? 1f / k : 0f); break;                   // airy square
                case Magic.Light: h = new[] { 1f, 0.3f, 0f, 0.15f }; break;                                // bell (plus partial in Tone)
                default: h = Harmonics(8, k => 1f / Mathf.Pow(k, 1.4f)); break;                            // dark detuned saw
            }
            t = new float[TableSize];
            float peak = 0f;
            for (int i = 0; i < TableSize; i++)
            {
                float x = 2f * Mathf.PI * i / TableSize;
                float v = 0f;
                for (int k = 0; k < h.Length; k++) v += h[k] * Mathf.Sin(x * (k + 1));
                t[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            for (int i = 0; i < TableSize; i++) t[i] /= peak;
            tables[m] = t;
            return t;
        }

        static float[] Harmonics(int count, Func<int, float> amp)
        {
            var h = new float[count];
            for (int k = 1; k <= count; k++) h[k - 1] = amp(k);
            return h;
        }

        static void Normalize(float[] buf, float target)
        {
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            if (peak < 1e-5f) return;
            float g = target / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        // ---------------------------------------------------------------- shared clock

        static double clockStart = -1;

        public static double ClockStart { get { return clockStart; } }

        // Starts a creature's loop on the next beat, at the matching point in the shared loop.
        public static void PlaySynced(AudioSource src)
        {
            double now = AudioSettings.dspTime;
            if (clockStart < 0) clockStart = now + 0.1;
            double beat = StepSeconds * 4;
            double since = now - clockStart;
            double next = clockStart + Math.Max(0, Math.Ceiling((since + 0.05) / beat)) * beat;
            double offset = (next - clockStart) % LoopSeconds;
            src.time = (float)offset;
            src.PlayScheduled(next);
        }

        // Where the shared loop should be right now, in seconds. Negative before the clock starts.
        public static double LoopPosition()
        {
            if (clockStart < 0) return -1;
            double since = AudioSettings.dspTime - clockStart;
            if (since < 0) return -1;
            return since % LoopSeconds;
        }

        public static int CurrentStep()
        {
            double pos = LoopPosition();
            return pos < 0 ? -1 : (int)(pos / StepSeconds) % Steps;
        }
    }
}
