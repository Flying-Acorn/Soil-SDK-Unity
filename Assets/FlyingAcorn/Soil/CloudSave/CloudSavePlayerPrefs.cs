using System.Collections.Generic;
using FlyingAcorn.Soil.CloudSave.Data;
using FlyingAcorn.Soil.Core.User;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FlyingAcorn.Soil.CloudSave
{
    public static class CloudSavePlayerPrefs
    {
        private static string PrefsPrefix => $"{UserPlayerPrefs.GetKeysPrefix()}cloudsave_";

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

        public static object Load(string key)
        {
            var saveModel = Saves.Find(s => s.key == key);
            return saveModel?.value;
        }
    }
}