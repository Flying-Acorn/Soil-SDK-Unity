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

        /// <summary>
        /// Gets all saved keys and their data from local cache.
        /// </summary>
        public static List<SaveModel> Saves
        {
            get
            {
                var saves = PlayerPrefs.GetString("cloudsave_saves", "[]");
                return JsonConvert.DeserializeObject<List<SaveModel>>(saves);
            }
            private set
            {
                var saves = JsonConvert.SerializeObject(value);
                PlayerPrefs.SetString("cloudsave_saves", saves);
            }
        }

        internal static void Save(string key, object value)
        {
            var saveModel = new SaveModel
            {
                key = key,
                value = JToken.FromObject(value)
            };
            Save(saveModel);
        }

        internal static void Save(SaveModel saveModel)
        {
            var saves = Saves;
            var index = saves.FindIndex(s => s.key == saveModel.key);
            if (index >= 0)
            {
                saves[index] = saveModel;
            }
            else
            {
                saves.Add(saveModel);
            }

            Saves = saves;
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