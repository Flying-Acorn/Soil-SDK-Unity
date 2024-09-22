using UnityEngine;

namespace FlyingAcorn.Soil.RemoteConfig
{
    internal static class RemoteConfigPlayerPrefs
    {
        public static bool DevMode
        {
            get => PlayerPrefs.GetInt("dev_mode", 0) == 1;
            set => PlayerPrefs.SetInt("dev_mode", value ? 1 : 0);
        }
    }
}