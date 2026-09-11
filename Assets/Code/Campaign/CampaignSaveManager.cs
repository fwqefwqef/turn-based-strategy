using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Windy.Srpg.Game.Campaign
{
    public enum CampaignSaveSlot
    {
        Debug,
        Campaign
    }

    public static class CampaignSaveManager
    {
        private const string CampaignSaveFileName = "campaign_save.json";
        private const string DebugSaveFileName = "debug_save.json";
        private const string SaveSlotMigrationMarkerFileName = "save_slots_v2.migrated";

        public static CampaignSaveSlot CurrentSlot => ResolveSlot(SceneManager.GetActiveScene().path);
        public static string CampaignSavePath => Path.Combine(Application.persistentDataPath, CampaignSaveFileName);
        public static string DebugSavePath => Path.Combine(Application.persistentDataPath, DebugSaveFileName);
        public static string SavePath
        {
            get
            {
                EnsureLegacySaveMigratedToDebugSlot();
                return GetSavePath(CurrentSlot);
            }
        }
        public static bool SaveExists => File.Exists(SavePath);

        public static string GetSavePath(CampaignSaveSlot slot)
        {
            return slot == CampaignSaveSlot.Campaign ? CampaignSavePath : DebugSavePath;
        }

        public static CampaignSaveSlot ResolveSlot(string scenePath)
        {
            string normalizedPath = (scenePath ?? string.Empty).Replace('\\', '/');
            bool isCampaignLevel = normalizedPath.IndexOf("/Scenes/Level/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/Scenes/Levels/", StringComparison.OrdinalIgnoreCase) >= 0;
            return isCampaignLevel ? CampaignSaveSlot.Campaign : CampaignSaveSlot.Debug;
        }

        private static void EnsureLegacySaveMigratedToDebugSlot()
        {
            string markerPath = Path.Combine(Application.persistentDataPath, SaveSlotMigrationMarkerFileName);
            if (File.Exists(markerPath))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                if (File.Exists(CampaignSavePath) && !File.Exists(DebugSavePath))
                {
                    File.Move(CampaignSavePath, DebugSavePath);
                    Debug.Log($"CampaignSaveManager: Migrated the existing save to the debug slot at '{DebugSavePath}'.");
                }

                File.WriteAllText(markerPath, "The pre-slot campaign_save.json was migrated as a debug save.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to migrate the existing save into the debug slot. {ex.Message}");
            }
        }

        public static CampaignSaveData Load()
        {
            return Load(CurrentSlot);
        }

        public static CampaignSaveData Load(CampaignSaveSlot slot)
        {
            EnsureLegacySaveMigratedToDebugSlot();
            string savePath = GetSavePath(slot);
            if (!File.Exists(savePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(savePath);
                json = MigrateLegacyFieldNames(json);
                CampaignSaveData save = JsonUtility.FromJson<CampaignSaveData>(json);
                return save ?? new CampaignSaveData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to load save file '{savePath}'. {ex.Message}");
                return null;
            }
        }

        private static string MigrateLegacyFieldNames(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            string oldClassPassiveIdsFieldName = "\"" + "Uni" + "quePassiveIds\"";
            const string classPassiveIdsFieldName = "\"ClassPassiveIds\"";
            return json.Replace(oldClassPassiveIdsFieldName, classPassiveIdsFieldName);
        }

        public static void Save(CampaignSaveData save)
        {
            Save(save, CurrentSlot);
        }

        public static void Save(CampaignSaveData save, CampaignSaveSlot slot)
        {
            if (save == null)
            {
                Debug.LogWarning("CampaignSaveManager: Ignored save request because the save data was null.");
                return;
            }

            try
            {
                EnsureLegacySaveMigratedToDebugSlot();
                string savePath = GetSavePath(slot);
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(savePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to write {slot} save file. {ex.Message}");
            }
        }

        public static bool Delete(CampaignSaveSlot slot)
        {
            try
            {
                EnsureLegacySaveMigratedToDebugSlot();
                string savePath = GetSavePath(slot);
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    Debug.Log($"CampaignSaveManager: Deleted the {slot} save at '{savePath}'.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"CampaignSaveManager: Failed to delete the {slot} save. {ex.Message}");
                return false;
            }
        }
    }
}

