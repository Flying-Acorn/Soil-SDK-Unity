using System.Collections.Generic;
using FlyingAcorn.Soil.CloudSave.Data;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FlyingAcorn.Soil.CloudSave
{
    /// <summary>
    /// Static class for managing cloud save data in local player preferences for caching and offline access.
    /// </summary>
    public static class CloudSavePlayerPrefs
    {
        private static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}cloudsave_";

        // Scoped to the signed-in user: a device-wide cache let one account's saves pass for another's after a
        // user change, so "unchanged since last upload" checks skipped writes the new account never received.
        private static string UserID => UserPlayerPrefs.UserInfoInstance?.uuid;
        private static string SavesKey(string userID) => $"{PrefsPrefix}{userID}_saves";
        private const string LegacySavesKey = "cloudsave_saves";
        private static bool _legacyCleaned;

        /// <summary>
        /// Gets all saved keys and their data from local cache. Empty until a user is signed in.
        /// </summary>
        public static List<SaveModel> Saves => GetSaves(UserID);

        private static List<SaveModel> GetSaves(string userID)
        {
            CleanLegacyCache();
            if (string.IsNullOrEmpty(userID))
                return new List<SaveModel>();
            var saves = PlayerPrefs.GetString(SavesKey(userID), "[]");
            return JsonConvert.DeserializeObject<List<SaveModel>>(saves);
        }

        private static void SetSaves(string userID, List<SaveModel> saves)
        {
            if (string.IsNullOrEmpty(userID))
                return;
            PlayerPrefs.SetString(SavesKey(userID), JsonConvert.SerializeObject(saves));
        }

        // The device-wide cache has no known owner, so it is dropped rather than migrated
        private static void CleanLegacyCache()
        {
            if (_legacyCleaned) return;
            _legacyCleaned = true;
            if (PlayerPrefs.HasKey(LegacySavesKey))
                PlayerPrefs.DeleteKey(LegacySavesKey);
        }

        internal static void Save(string key, object value)
        {
            var saveModel = new SaveModel
            {
                key = key,
                value = JToken.FromObject(value)
            };
            Save(saveModel, UserID);
        }

        /// <param name="userID">The user the request was made for, which can differ from the current user
        /// when the user changed while the request was in flight.</param>
        internal static void Save(SaveModel saveModel, string userID)
        {
            var saves = GetSaves(userID);
            var index = saves.FindIndex(s => s.key == saveModel.key);
            if (index >= 0)
            {
                saves[index] = saveModel;
            }
            else
            {
                saves.Add(saveModel);
            }

            SetSaves(userID, saves);
        }

        /// <summary>
        /// Loads cached value for a specific key.
        /// </summary>
        /// <param name="key">The key to load cached data for.</param>
        /// <returns>The cached value, or null if not found.</returns>
        public static object Load(string key)
        {
            var saveModel = Saves.Find(s => s.key == key);
            return saveModel?.value;
        }
    }
}
