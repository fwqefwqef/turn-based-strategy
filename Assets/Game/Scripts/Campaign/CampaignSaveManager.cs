using System;
using System.IO;
using UnityEngine;

namespace Windy.Srpg.Game.Campaign
{
    public static class CampaignSaveManager
    {
        private const string SaveFileName = "campaign_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public static bool SaveExists => File.Exists(SavePath);

        public static CampaignSaveData Load()
        {
            if (!SaveExists)
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                CampaignSaveData save = JsonUtility.FromJson<CampaignSaveData>(json);
                return save ?? new CampaignSaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to load save file '{SavePath}'. {ex.Message}");
                return null;
            }
        }

        public static void Save(CampaignSaveData save)
        {
            if (save == null)
            {
                Debug.LogWarning("CampaignSaveManager: Ignored save request because the save data was null.");
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to write save file '{SavePath}'. {ex.Message}");
            }
        }
    }
}
