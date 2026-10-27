using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    // The six powers of Mythica. Opposing pairs: Fire/Water, Sky/Earth, Light/Shadow.
    public enum Magic { Fire, Water, Earth, Sky, Light, Shadow }

    // The musical part a species sings. The magic type changes how it sounds, not what it plays.
    public enum Part
    {
        Kick, Snare, Hat, Tom, Click,
        Bass, SubBass, WalkBass, Riff,
        Pad, LowPad, Arp, HighArp, Harp, Stabs, Skank, Bells,
        Counter, Lead, HighLead
    }

    public enum BodyKind { Beast, SmallBeast, Horse, Bird, Fish, Tentacled, Giant, Turtle, Lizard }

    [System.Flags]
    public enum Feature
    {
        None = 0,
        Wings = 1 << 0,
        Horns = 1 << 1,
        Antlers = 1 << 2,
        ThreeHeads = 1 << 3,
        Mane = 1 << 4,
        Stinger = 1 << 5,
        Spikes = 1 << 6,
        Skull = 1 << 7,
        BigEars = 1 << 8,
        Legs = 1 << 9,
        Beak = 1 << 10,
        Crest = 1 << 11,
        Whiskers = 1 << 12,
        LongNecks = 1 << 13,
        Stone = 1 << 14,
        Hover = 1 << 15,
        BushyTail = 1 << 16,
    }

    public class Species
    {
        public string Id;
        public string Name;
        public Magic Native;
        public int Tier;          // 1..4, drives price, income and hatch time
        public Part Part;
        public BodyKind Body;
        public Feature Features;
        public Color Accent;
        public string Blurb;

        public bool Has(Feature f) { return (Features & f) != 0; }
        public int Cost { get { return MythicaDB.TierCost[Tier - 1]; } }
        public int HatchSeconds { get { return MythicaDB.TierHatchSeconds[Tier - 1]; } }
    }

    public static class MythicaDB
    {
        public static readonly int[] TierCost = { 100, 300, 800, 2000 };
        public static readonly float[] TierCoinsPerMinute = { 12f, 20f, 32f, 50f };
        public static readonly int[] TierHatchSeconds = { 10, 30, 60, 120 };
        // Creatures needed on the island before each shop tier opens.
        public static readonly int[] TierUnlockCount = { 0, 3, 6, 9 };

        // Variants with a magic type other than the species' native one are bred, rarer, and earn more.
        public const float BredIncomeBonus = 1.5f;
        public const float CoinCapMinutes = 60f;

        public static readonly Magic[] AllMagic =
            { Magic.Fire, Magic.Water, Magic.Earth, Magic.Sky, Magic.Light, Magic.Shadow };

        // Roster: the Top 20 Story Highlight Creatures from Alex's "Mythica Story Creature List".
        public static readonly List<Species> All = new List<Species>
        {
            // Tier 1
            S("golem", "Golem", Magic.Earth, 1, Part.Kick, BodyKind.Giant, Feature.Stone,
              new Color(0.55f, 0.52f, 0.48f), "A stone guardian, magically formed. Not alive, but it never loses the beat."),
            S("jackalope", "Jackalope", Magic.Light, 1, Part.Arp, BodyKind.SmallBeast, Feature.Horns | Feature.BigEars,
              new Color(0.95f, 0.85f, 0.6f), "A horned rabbit. Arko's primary companion."),
            S("ratatoskr", "Ratatoskr", Magic.Earth, 1, Part.Hat, BodyKind.SmallBeast, Feature.Horns | Feature.BushyTail,
              new Color(0.85f, 0.5f, 0.2f), "Horned squirrel with glowing stripes. A born forest guide."),
            S("aspidochelone", "Aspidochelone", Magic.Earth, 1, Part.Bass, BodyKind.Turtle, Feature.None,
              new Color(0.35f, 0.6f, 0.3f), "An island turtle. Some carry whole forests on their backs."),
            S("yeti", "Yeti", Magic.Earth, 1, Part.Snare, BodyKind.Giant, Feature.Horns,
              new Color(0.92f, 0.92f, 0.95f), "Horned yeti. Some white, some brown, all loud."),

            // Tier 2
            S("landshark", "Land Shark", Magic.Water, 2, Part.WalkBass, BodyKind.Fish, Feature.Legs,
              new Color(0.6f, 0.65f, 0.7f), "A shark on stubby legs. Most are dog sized."),
            S("firesalamander", "Fire Salamander", Magic.Fire, 2, Part.Click, BodyKind.Lizard, Feature.None,
              new Color(1f, 0.75f, 0.1f), "Comes in as many colours as the phoenix. Ancient ones grow large."),
            S("enfield", "Enfield", Magic.Sky, 2, Part.Counter, BodyKind.Beast, Feature.Wings | Feature.BigEars,
              new Color(0.9f, 0.45f, 0.2f), "A forearm-winged fox. It can't truly fly, but it pounces spectacularly."),
            S("dragonkoi", "Dragon Koi", Magic.Water, 2, Part.HighArp, BodyKind.Fish, Feature.Whiskers | Feature.Hover,
              new Color(1f, 0.55f, 0.35f), "Starts as a koi. Becomes a sea serpent dragon when ancient."),
            S("drake", "Drake", Magic.Fire, 2, Part.Riff, BodyKind.Lizard, Feature.Spikes | Feature.Horns,
              new Color(0.45f, 0.2f, 0.15f), "A wingless dragon, in lots of styles."),

            // Tier 3
            S("griffin", "Griffin", Magic.Light, 3, Part.Lead, BodyKind.Beast, Feature.Wings | Feature.Beak,
              new Color(1f, 0.85f, 0.35f), "Eagle up front, great cat behind."),
            S("peryton", "Peryton", Magic.Sky, 3, Part.Bells, BodyKind.Horse, Feature.Wings | Feature.Antlers,
              new Color(0.7f, 0.55f, 0.4f), "A deer with wings."),
            S("soleo", "Soleo", Magic.Fire, 3, Part.Skank, BodyKind.Beast, Feature.Mane,
              new Color(1f, 0.6f, 0.1f), "A fire cat, in many styles."),
            S("cerberus", "Cerberus", Magic.Shadow, 3, Part.Stabs, BodyKind.Beast, Feature.ThreeHeads | Feature.BigEars,
              new Color(0.6f, 0.1f, 0.15f), "Three-headed hound. Three heads, three voices."),
            S("kraken", "Kraken", Magic.Shadow, 3, Part.SubBass, BodyKind.Tentacled, Feature.None,
              new Color(0.5f, 0.2f, 0.6f), "A giant squid or octopus from the deep."),

            // Tier 4
            S("phoenix", "Phoenix", Magic.Fire, 4, Part.HighLead, BodyKind.Bird, Feature.Wings | Feature.Crest,
              new Color(1f, 0.85f, 0.2f), "A fire bird. Its fire can be any magic type."),
            S("pegasus", "Pegasus", Magic.Sky, 4, Part.Harp, BodyKind.Horse, Feature.Wings | Feature.Mane,
              new Color(0.95f, 0.95f, 1f), "The winged horse."),
            S("hydra", "Hydra", Magic.Light, 4, Part.Pad, BodyKind.Lizard, Feature.ThreeHeads | Feature.LongNecks,
              new Color(0.4f, 0.8f, 0.6f), "Many-headed serpent. Its heads sing in harmony."),
            S("manticore", "Manticore", Magic.Shadow, 4, Part.Tom, BodyKind.Beast, Feature.Mane | Feature.Stinger | Feature.Wings,
              new Color(0.2f, 0.15f, 0.25f), "A scorpion-tailed cat. Some have bat wings."),
            S("wendigo", "Wendigo", Magic.Shadow, 4, Part.LowPad, BodyKind.Beast, Feature.Skull | Feature.Antlers,
              new Color(0.9f, 0.88f, 0.8f), "A bone-headed wolf. Its howl hangs in the air."),
        };

        static Dictionary<string, Species> byId;

        static Species S(string id, string name, Magic native, int tier, Part part, BodyKind body, Feature f, Color accent, string blurb)
        {
            return new Species { Id = id, Name = name, Native = native, Tier = tier, Part = part, Body = body, Features = f, Accent = accent, Blurb = blurb };
        }

        public static Species Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Species>();
                foreach (var s in All) byId[s.Id] = s;
            }
            Species result;
            return byId.TryGetValue(id, out result) ? result : null;
        }

        public static int VariantCount { get { return All.Count * AllMagic.Length; } }

        public static string VariantName(Species s, Magic m)
        {
            return m == s.Native ? s.Name : s.Name + " (" + m + ")";
        }

        public static string VariantKey(string speciesId, Magic m) { return speciesId + ":" + m; }

        public static float CoinsPerMinute(Species s, Magic m)
        {
            float rate = TierCoinsPerMinute[s.Tier - 1];
            return m == s.Native ? rate : rate * BredIncomeBonus;
        }

        public static Color MagicColor(Magic m)
        {
            switch (m)
            {
                case Magic.Fire: return new Color(0.95f, 0.35f, 0.15f);
                case Magic.Water: return new Color(0.2f, 0.5f, 0.95f);
                case Magic.Earth: return new Color(0.45f, 0.7f, 0.25f);
                case Magic.Sky: return new Color(0.55f, 0.85f, 0.95f);
                case Magic.Light: return new Color(1f, 0.93f, 0.55f);
                default: return new Color(0.3f, 0.2f, 0.4f);
            }
        }

        public static Magic Opposite(Magic m)
        {
            switch (m)
            {
                case Magic.Fire: return Magic.Water;
                case Magic.Water: return Magic.Fire;
                case Magic.Earth: return Magic.Sky;
                case Magic.Sky: return Magic.Earth;
                case Magic.Light: return Magic.Shadow;
                default: return Magic.Light;
            }
        }

        public struct BreedOutcome
        {
            public Species Species;
            public Magic Magic;
            public float Chance;
        }

        // Breeding rule from the creature list doc: parents with different magic give
        // 50% parent A's creature with B's magic, 50% parent B's creature with A's magic.
        public static List<BreedOutcome> BreedOutcomes(Species a, Magic am, Species b, Magic bm)
        {
            var list = new List<BreedOutcome>();
            if (am == bm)
            {
                if (a == b) list.Add(new BreedOutcome { Species = a, Magic = am, Chance = 1f });
                else
                {
                    list.Add(new BreedOutcome { Species = a, Magic = am, Chance = 0.5f });
                    list.Add(new BreedOutcome { Species = b, Magic = am, Chance = 0.5f });
                }
                return list;
            }
            if (a == b)
            {
                list.Add(new BreedOutcome { Species = a, Magic = am, Chance = 0.5f });
                list.Add(new BreedOutcome { Species = a, Magic = bm, Chance = 0.5f });
                return list;
            }
            list.Add(new BreedOutcome { Species = a, Magic = bm, Chance = 0.5f });
            list.Add(new BreedOutcome { Species = b, Magic = am, Chance = 0.5f });
            return list;
        }
    }
}
