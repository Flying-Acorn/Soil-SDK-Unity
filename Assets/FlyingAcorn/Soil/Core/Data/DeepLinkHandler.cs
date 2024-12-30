using System;
using System.Linq;
using FlyingAcorn.Analytics;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    public class DeepLinkHandler : MonoBehaviour
    {
        private static DeepLinkHandler Instance { get; set; }
        public static Uri LastDeeplinkURL;
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
            Application.deepLinkActivated -= DeepLinkActivated;
            Application.deepLinkActivated += DeepLinkActivated;
            DeepLinkActivated(Application.absoluteURL);
        }

        private static void DeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            var uri = new Uri(url);
            LastDeeplinkURL = uri;

            MyDebug.Verbose(
                $"Deep link activated: {uri.GetLeftPart(UriPartial.Path)} with key values: " +
                $"{string.Join(", ", uri.Query.Split('&').Select(x => x.Split('=')).Select(x => $"{x[0]}={x[1]}"))}");
            OnDeepLinkActivated?.Invoke(uri);
        }
    }
}