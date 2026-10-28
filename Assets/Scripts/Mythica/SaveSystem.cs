using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythica
{
    [Serializable]
    public class OwnedCreature
    {
        public string species;
        public Magic magic;
        public int slot;
        public long lastCollect;   // unix seconds; coins accrue from here, even while the game is closed
        public bool muted;
        public int level = 1;
        public int feeds;          // feeds toward the next level
    }

    [Serializable]
    public class PlacedDecoration
    {
        public string id;
        public int slot;
    }

    [Serializable]
    public class BakeJob
    {
        public bool active;
        public int recipe;
        public long doneAt;
    }

    [Serializable]
    public class Egg
    {
        public string species;
        public Magic magic;
        public long hatchAt;
    }

    [Serializable]
    public class BreedJob
    {
        public bool active;
        public string species;
        public Magic magic;
        public long doneAt;
        public string parents;   // display text only
    }

    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public long coins;
        public long food;
        public long crystals;
        public int level = 1;
        public int xp;              // toward the next level
        public List<OwnedCreature> creatures = new List<OwnedCreature>();
        public List<PlacedDecoration> decorations = new List<PlacedDecoration>();
        public List<Egg> eggs = new List<Egg>();
        public BreedJob breeding = new BreedJob();
        public BakeJob bake = new BakeJob();
        public int nurseryLevel = 1;   // each level holds one more egg
        public int groveLevel = 1;     // each level breeds faster
        public List<string> discovered = new List<string>();
        public bool soundOn = true;
    }

    // PlayerPrefs maps to IndexedDB on WebGL, so saves survive page reloads on the arcade site.
    public static class SaveSystem
    {
        const string Key = "singing_mythica_save_v1";

        public static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); } }

        public static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                if (data == null) return null;
                if (data.breeding == null) data.breeding = new BreedJob();
                if (data.bake == null) data.bake = new BakeJob();
                if (data.decorations == null) data.decorations = new List<PlacedDecoration>();
                data.nurseryLevel = Mathf.Max(1, data.nurseryLevel);
                data.groveLevel = Mathf.Max(1, data.groveLevel);
                if (data.version < 2)
                {
                    // Saves from the first version had no food, crystals or levels.
                    data.version = 2;
                    data.level = 1;
                    data.food = Progression.StartFood;
                    data.crystals = Progression.StartCrystals;
                    foreach (var c in data.creatures) c.level = 1;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Mythica: could not read save, starting fresh. " + e.Message);
                return null;
            }
        }

        public static void Save(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
