using UnityEngine;

namespace Mythica
{
    // A creature living on the island: sings its loop, bounces on its notes, and earns coins.
    public class Creature : MonoBehaviour
    {
        public OwnedCreature Data;
        public Species Species;
        public Magic Magic { get { return Data.magic; } }
        public float Height;

        Transform body;
        Transform ring;
        AudioSource source;
        float[] onsets;
        int lastStep = -1;
        float pulse;
        float syncCheckAt;
        float phaseOffset;

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
            c.phaseOffset = Random.value * 10f;

            c.ring = CreatureBuilder.Prim(PrimitiveType.Cylinder, built.Root.transform, new Vector3(0, 0.02f, 0),
                new Vector3(1.9f, 0.01f, 1.9f), new Color(1f, 0.95f, 0.5f));
            c.ring.gameObject.SetActive(false);

            c.source = built.Root.AddComponent<AudioSource>();
            c.source.playOnAwake = false;
            c.source.loop = true;
            c.source.spatialBlend = 0f;
            c.source.volume = 0.55f;
            return c;
        }

        public void StartSinging()
        {
            var song = MusicSynth.Get(Species, Data.magic);
            onsets = song.Onsets;
            source.clip = song.Clip;
            source.mute = Data.muted;
            MusicSynth.PlaySynced(source);
            syncCheckAt = Time.realtimeSinceStartup + 2f;
        }

        public void SetMuted(bool muted)
        {
            Data.muted = muted;
            source.mute = muted;
        }

        public void SetSelected(bool selected) { ring.gameObject.SetActive(selected); }

        public float CoinsPerMinute { get { return MythicaDB.CoinsPerMinute(Species, Data.magic); } }

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
                    if (!Data.muted && source.isPlaying && onsets[step] > 0f) pulse = onsets[step];
                }
            }
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 6f);

            float t = Time.time + phaseOffset;
            float idle = Mathf.Sin(t * 1.7f) * 0.02f;
            body.localScale = new Vector3(1f - pulse * 0.06f, 1f + pulse * 0.14f + idle, 1f - pulse * 0.06f);
            float hover = Species.Has(Feature.Hover) ? Mathf.Sin(t * 1.3f) * 0.12f : 0f;
            body.localPosition = new Vector3(0, hover, 0);

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
