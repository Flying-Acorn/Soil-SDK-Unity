using FlyingAcorn.Soil.Core;
using System;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Economy.Models.Responses;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Economy
{
    public class Economy
    {
        private static string _economyBaseUrl => $"{Core.Data.Constants.ApiUrl}/economy/";
        private static string SummaryUrl => $"{_economyBaseUrl}summary/"; // Get
        private static string InventoryBaseUrl => $"{_economyBaseUrl}inventory/"; // Get/Post(Set)
        private static string VirtualCurrencyBaseUrl => $"{_economyBaseUrl}currencies/"; // Get/Post(Set)
        private static string InventoryIncreaseUrl => $"{InventoryBaseUrl}increase/"; // Post
        private static string InventoryDecreaseUrl => $"{InventoryBaseUrl}decrease/"; // Post
        private static string VirtualCurrencyIncreaseUrl => $"{VirtualCurrencyBaseUrl}increase/"; // Post
        private static string VirtualCurrencyDecreaseUrl => $"{VirtualCurrencyBaseUrl}decrease/"; // Post

        public static bool Ready => SoilServices.Ready && _initialized;

        public static Action OnEconomyInitialized { get; set; }
        public static Action<SoilException> OnEconomyInitializationFailed { get; set; }

        private static bool _isInitializing;
        private static bool _initialized;

        /// <summary>
        /// Initializes the Economy service. Call this to initialize SoilServices and fetch the economy summary.
        /// </summary>
        public static void Initialize()
        {
            if (Ready)
                return;
            if (_isInitializing)
                return;

            _isInitializing = true;
            _initialized = false;

            if (SoilServices.Ready)
            {
                EconomyInitSuccess().Forget();
                return;
            }
            UnlistenCore();
            SoilServices.OnInitializationFailed += SoilInitFailed;
            SoilServices.OnServicesReady += SoilInitSuccess;

            // Initialize SoilServices if not ready
            SoilServices.InitializeAsync();
        }

        private static void UnlistenCore()
        {
            SoilServices.OnInitializationFailed -= SoilInitFailed;
            SoilServices.OnServicesReady -= SoilInitSuccess;
        }

        private static void SoilInitFailed(SoilException exception)
        {
            UnlistenCore();
            _isInitializing = false;
            OnEconomyInitializationFailed?.Invoke(exception);
        }

        private static void SoilInitSuccess()
        {
            UnlistenCore();
            EconomyInitSuccess().Forget();
        }

        private static async UniTask EconomyInitSuccess()
        {
            try
            {
                var summary = await GetSummary();
                EconomyPlayerPrefs.SetVirtualCurrencies(summary.virtual_currencies);
                EconomyPlayerPrefs.SetInventoryItems(summary.inventory_items);
                _isInitializing = false;
                _initialized = true;
                OnEconomyInitialized?.Invoke();
            }
            catch (SoilException ex)
            {
                _isInitializing = false;
                _initialized = false;
                OnEconomyInitializationFailed?.Invoke(ex);
            }
        }

        /// <summary>
        /// Fetches the economy summary for the current user.
        /// </summary>
        /// <returns>The economy summary success response.</returns>
        public static async UniTask<EconomySummarySuccess> GetSummary()
        {
            if (!SoilServices.Ready)
            {
                throw new SoilException("Economy service is not initialized. Cannot get economy summary.",
                    SoilExceptionErrorCode.NotReady);
            }

            using var request = UnityWebRequest.Get(SummaryUrl);
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while getting economy summary: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                EconomyError error = null;
                try
                {
                    error = JsonConvert.DeserializeObject<EconomyError>(request.downloadHandler?.text);
                }
                catch { }

                if (error != null)
                {
                    throw new SoilException($"Economy error: {error.detail}", SoilExceptionErrorCode.InvalidResponse);
                }
                else
                {
                    throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
                }
            }

            EconomySummarySuccess summary;
            try
            {
                summary = JsonConvert.DeserializeObject<EconomySummarySuccess>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while getting economy summary. Response: {request.downloadHandler?.text}", SoilExceptionErrorCode.InvalidResponse);
            }

            return summary;
        }
    }
}