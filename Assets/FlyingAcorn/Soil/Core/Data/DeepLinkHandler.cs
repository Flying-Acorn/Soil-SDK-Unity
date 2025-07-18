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

        public static Uri LastDeeplinkURL
        {
            get
            {
                if (Instance == null)
                    MyDebug.Info("DeepLinkHandler is not active");
                return _lastDeepLinkURL;
            }
        }

        private static Uri _lastDeepLinkURL;
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
            MyDebug.Verbose("DeepLinkHandler is active");
        }

        private static void DeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            Uri uri;

            try
            {
                uri = new Uri(url);
            }
            catch (Exception e)
            {
                MyDebug.LogError($"Failed to parse deep link: {url} with error: {e.Message}");
                return;
            }

            var queryDictionary = uri.Query.Split('&').Select(parameter => parameter.Split('=')).Where(pair => pair.Length == 2)
                .ToDictionary(pair => pair[0], pair => pair[1]);

            uri = new UriBuilder(uri) { Query = "" }.Uri;
            uri = queryDictionary.Aggregate(uri,
                (current, pair) => new UriBuilder(current) { Query = $"{current.Query}{pair.Key}={pair.Value}&" }.Uri);

            _lastDeepLinkURL = uri;

            MyDebug.Verbose($"Deep link activated: {uri.GetLeftPart(UriPartial.Path)} with key values: " +
                            queryDictionary.Aggregate("", (current, pair) => current + $"{pair.Key}={pair.Value}&"));
            OnDeepLinkActivated?.Invoke(uri);
        }
    }
}