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
        public int version = 1;
        public long coins;
        public List<OwnedCreature> creatures = new List<OwnedCreature>();
        public List<Egg> eggs = new List<Egg>();
        public BreedJob breeding = new BreedJob();
        public List<string> discovered = new List<string>();
        public bool soundOn = true;
    }

    // PlayerPrefs maps to IndexedDB on WebGL, so saves survive page reloads on the arcade site.
    public static class SaveSystem
    {
        const string Key = "mythica_sanctuary_save_v1";

        public static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); } }

        public static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                if (data == null) return null;
                if (data.breeding == null) data.breeding = new BreedJob();
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
