using System;
using System.Net.Http;
using Cysharp.Threading.Tasks;

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [Serializable]
    public class PurchasingSettings
    {
        public string api;

        public PurchasingSettings(string apiUrl)
        {
            api = apiUrl;
        }

        internal static async UniTask Validate(PurchasingSettings settingsToValidate)
        {
            if (settingsToValidate == null)
                throw new ArgumentNullException(nameof(settingsToValidate), "incoming purchasing settings cannot be null.");

            if (string.IsNullOrWhiteSpace(settingsToValidate.api))
                throw new ArgumentException("API URL in incoming purchasing settings cannot be null or empty.", nameof(settingsToValidate.api));

            if (!Uri.IsWellFormedUriString(settingsToValidate.api, UriKind.Absolute))
                throw new ArgumentException("API URL in incoming purchasing settings is not a valid absolute URL.", nameof(settingsToValidate.api));

            try
            {
                var uri = new Uri(settingsToValidate.api);
                if (uri.Scheme != Uri.UriSchemeHttps)
                    throw new ArgumentException("API URL in incoming purchasing settings must use HTTPS.", nameof(settingsToValidate.api));
            }
            catch (UriFormatException ex)
            {
                throw new ArgumentException("API URL in incoming purchasing settings is not a valid URI.", nameof(settingsToValidate.api), ex);
            }

            if (settingsToValidate.api.EndsWith("/"))
            {
                settingsToValidate.api = settingsToValidate.api.TrimEnd('/');
            }
            
            if (!await Purchasing.HealthCheck(settingsToValidate.api))
                throw new ArgumentException("API URL in incoming purchasing settings is not reachable. Health check failed.", nameof(settingsToValidate.api));
        }
    }
}