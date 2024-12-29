using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Analytics;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    public class DeepLinkHandler : MonoBehaviour
    {
        private static DeepLinkHandler Instance { get; set; }
        public string deeplinkURL;
        [UsedImplicitly] public static Action<Uri> OnDeepLinkActivated;

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
            deeplinkURL = url;

            var uri = new Uri(url);

            MyDebug.Verbose(
                $"Deep link activated: {uri.GetLeftPart(UriPartial.Path)} with key values: " +
                $"{string.Join(", ", uri.Query.Split('&').Select(x => x.Split('=')).Select(x => $"{x[0]}={x[1]}"))}");
            OnDeepLinkActivated?.Invoke(uri);
        }
    }
}