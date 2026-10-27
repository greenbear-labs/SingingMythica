using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // A creature living on the island: sings its loop, bounces and opens its mouth on its notes, and earns coins.
    public class Creature : MonoBehaviour
    {
        public OwnedCreature Data;
        public Species Species { get; private set; }
        public Magic Magic { get { return Data.magic; } }
        public float Height;

        Transform body;
        Transform ring;
        List<Transform> mouths;
        List<Vector3> mouthScales;
        AudioSource source;
        float[] onsets;
        int lastStep = -1;
        float pulse;
        float hop;
        float syncCheckAt;
        float phaseOffset;
        bool soloMuted;

        public static Creature Spawn(OwnedCreature data, Transform parent)
        {
            var species = MythicaDB.Get(data.species);
            var built = CreatureBuilder.Build(species, data.magic);
            built.Root.transform.SetParent(parent, false);
            var c = built.Root.AddComponent<Creature>();
            c.Data = data;
            c.Species = species;
            c.Height = built.Height;
            c.body = built.Body;
            c.mouths = built.Mouths;
            c.mouthScales = new List<Vector3>();
            foreach (var m in c.mouths) c.mouthScales.Add(m.localScale);
            c.phaseOffset = Random.value * 10f;

            c.ring = CreatureBuilder.Prim(PrimitiveType.Cylinder, built.Root.transform, new Vector3(0, 0.02f, 0),
                new Vector3(1.9f, 0.01f, 1.9f), new Color(1f, 0.95f, 0.5f));
            c.ring.gameObject.SetActive(false);
            // A soft blob shadow keeps creatures grounded, even when they hop or hover.
            CreatureBuilder.Prim(PrimitiveType.Cylinder, built.Root.transform, new Vector3(0, 0.015f, 0),
                new Vector3(1.5f, 0.005f, 1.3f), new Color(0.2f, 0.38f, 0.16f));

            c.source = built.Root.AddComponent<AudioSource>();
            c.source.playOnAwake = false;
            c.source.loop = true;
            c.source.spatialBlend = 0f;
            c.source.volume = 0.55f;
            c.ApplyLevelScale();
            return c;
        }

        public int Level { get { return Mathf.Max(1, Data.level); } }

        public void ApplyLevelScale()
        {
            transform.localScale = Vector3.one * Progression.LevelScale(Level);
        }

        public void StartSinging()
        {
            var song = MusicSynth.Get(Species, Data.magic);
            onsets = song.Onsets;
            source.clip = song.Clip;
            UpdateMute();
            MusicSynth.PlaySynced(source);
            syncCheckAt = Time.realtimeSinceStartup + 2f;
        }

        public bool IsSilent { get { return Data.muted || soloMuted; } }

        public void SetMuted(bool muted)
        {
            Data.muted = muted;
            UpdateMute();
        }

        // Solo mutes everyone else without touching their saved mute setting.
        public void SetSoloMuted(bool muted)
        {
            soloMuted = muted;
            UpdateMute();
        }

        void UpdateMute() { if (source != null) source.mute = IsSilent; }

        public void SetSelected(bool selected) { ring.gameObject.SetActive(selected); }

        public void Hop() { hop = 1f; }

        public float CoinsPerMinute { get { return MythicaDB.CoinsPerMinute(Species, Data.magic, Level); } }

        public int CoinCap { get { return Mathf.FloorToInt(CoinsPerMinute * MythicaDB.CoinCapMinutes); } }

        public int StoredCoins(long now)
        {
            float minutes = Mathf.Min((now - Data.lastCollect) / 60f, MythicaDB.CoinCapMinutes);
            return Mathf.FloorToInt(Mathf.Max(0f, minutes) * CoinsPerMinute);
        }

        void Update()
        {
            if (onsets != null)
            {
                int step = MusicSynth.CurrentStep();
                if (step >= 0 && step != lastStep)
                {
                    lastStep = step;
                    if (!IsSilent && source.isPlaying && onsets[step] > 0f) pulse = onsets[step];
                }
            }
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 6f);
            hop = Mathf.MoveTowards(hop, 0f, Time.deltaTime * 2.5f);

            float t = Time.time + phaseOffset;
            float idle = Mathf.Sin(t * 1.7f) * 0.02f;
            body.localScale = new Vector3(1f - pulse * 0.06f, 1f + pulse * 0.14f + idle, 1f - pulse * 0.06f);
            float hover = Species.Has(Feature.Hover) ? Mathf.Sin(t * 1.3f) * 0.12f : 0f;
            float jump = Mathf.Sin(hop * Mathf.PI) * 0.6f;
            body.localPosition = new Vector3(0, hover + jump, 0);

            // Silent creatures keep their mouths shut; singers open wide on every note.
            float open = IsSilent ? 0f : pulse;
            for (int i = 0; i < mouths.Count; i++)
            {
                var b = mouthScales[i];
                mouths[i].localScale = new Vector3(b.x * (1f + open * 0.2f), b.y * (1f + open * 5f), b.z);
            }

            KeepInSync();
        }

        // Loops are identical in length, so they only drift if a browser or platform starts one late.
        // Nudge it back to the shared clock if that happens.
        void KeepInSync()
        {
            if (!source.isPlaying || Time.realtimeSinceStartup < syncCheckAt) return;
            syncCheckAt = Time.realtimeSinceStartup + 3f;
            double pos = MusicSynth.LoopPosition();
            if (pos < 0) return;
            double diff = System.Math.Abs(source.time - pos);
            diff = System.Math.Min(diff, MusicSynth.LoopSeconds - diff);
            if (diff > 0.08) source.time = (float)pos;
        }
    }
}
