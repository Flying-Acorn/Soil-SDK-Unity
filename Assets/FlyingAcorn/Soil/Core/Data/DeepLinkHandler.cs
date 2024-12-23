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
            // "com.flyingacorn.soilsdk:/google_callback?state=a53f4e03-c2c0-4d95-9c59-1ab81b13a09a&code=4/0AanRRruWyxI8d7oJSkre_RBK6uDkTZ-8xyVGwpra9VvOmQGHn0Kk7cpbrGQnV_-S2C7lhw&scope=email%20profile%20openid%20https://www.googleapis.com/auth/userinfo.profile%20https://www.googleapis.com/auth/userinfo.email&authuser=0&prompt=consent"
            MyDebug.Info("Deep link activated: " + url);
            // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
            deeplinkURL = url;

            // Decode the URL to determine action. 
            // In this example, the application expects a link formatted like this:
            // unitydl://mylink?scene1=dasd&adsa=sdas
            var deeplinkRightSide = url.Split('?')[1];
            MyDebug.Info("Deep link right side: " + deeplinkRightSide);
            if (string.IsNullOrEmpty(deeplinkRightSide))
                return;
            // get characters after //
            var path = deeplinkRightSide.Split('/')[2];
            MyDebug.Info("Deep link path: " + path);
            var equations = deeplinkRightSide.Split('&');
            MyDebug.Info("Deep link equations: " + equations);
            var keyValues = new Dictionary<string, string>();
            foreach (var equation in equations)
            {
                try
                {
                    var key = equation.Split('=')[0];
                    var value = equation.Split('=')[1];
                    keyValues.Add(key, value);
                    MyDebug.Info("Deep link key: " + key + " value: " + value);
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