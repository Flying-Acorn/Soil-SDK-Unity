using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    public class DeepLinkHandler : MonoBehaviour
    {
        private static DeepLinkHandler Instance { get; set; }
        public string deeplinkURL;
        [UsedImplicitly] public Action<string, Dictionary<string, string>> OnDeepLinkActivated;

        private void Awake()
        {
            if (Instance is not null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.deepLinkActivated += DeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                DeepLinkActivated(Application.absoluteURL);
            else
            {
                MyDebug.Info("Absolute URL is empty");
                deeplinkURL = "[none]";
            }
        }

        private void DeepLinkActivated(string url)
        {
            MyDebug.Info("Deep link activated: " + url);
            deeplinkURL = url;

            var deeplinkRightSide = url.Split('?')[1];
            if (string.IsNullOrEmpty(deeplinkRightSide))
                return;
            var path = deeplinkRightSide.Split('/')[2];
            var equations = deeplinkRightSide.Split('&');
            var keyValues = new Dictionary<string, string>();
            foreach (var equation in equations)
            {
                try
                {
                    var key = equation.Split('=')[0];
                    var value = equation.Split('=')[1];
                    keyValues.Add(key, value);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning("Error parsing deep link: " + e.Message);
                }
            }

            OnDeepLinkActivated?.Invoke(path, keyValues);
        }
    }
}