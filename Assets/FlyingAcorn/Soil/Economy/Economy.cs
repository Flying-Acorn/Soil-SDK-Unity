using FlyingAcorn.Soil.Core;
using System;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Economy.Models.Responses;
using FlyingAcorn.Soil.Economy.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;

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

        /// <summary>
        /// Gets whether the Economy service is ready for use.
        /// </summary>
        public static bool Ready => SoilServices.Ready && _initialized;

        /// <summary>
        /// Event fired when the Economy service is successfully initialized.
        /// </summary>
        public static Action OnEconomyInitialized { get; set; }

        /// <summary>
        /// Event fired when Economy service initialization fails.
        /// </summary>
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
                await GetSummary();
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
                throw new SoilException("SoilServices is not initialized. Cannot get economy summary.",
                    SoilExceptionErrorCode.NotReady);
            }

            var summary = await ExecuteRequest<EconomySummarySuccess>(SummaryUrl, UnityWebRequest.kHttpVerbGET);

            // Centralized cache update
            EconomyPlayerPrefs.SetVirtualCurrencies(summary.virtual_currencies);
            EconomyPlayerPrefs.SetInventoryItems(summary.inventory_items);

            return summary;
        }

        private static async UniTask<T> ExecuteRequest<T>(string url, string method, object payload = null)
        {
            using var request = new UnityWebRequest(url, method);

            if (payload != null)
            {
                var stringBody = JsonConvert.SerializeObject(payload, Formatting.None);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(stringBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error during economy request: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new SoilException($"Request failed: {request.error}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                EconomyError error = null;
                try
                {
                    var errorText = request.downloadHandler?.text;
                    if (!string.IsNullOrEmpty(errorText))
                    {
                        error = JsonConvert.DeserializeObject<EconomyError>(errorText);
                    }
                }
                catch { }

                if (error != null)
                {
                    var economyErrorCode = Enum.IsDefined(typeof(EconomyErrorCode), (int)request.responseCode)
                        ? (EconomyErrorCode)request.responseCode
                        : EconomyErrorCode.InternalError;
                    throw new EconomyException(error.GetFullErrorMessage(), SoilExceptionErrorCode.InvalidResponse, error, economyErrorCode);
                }
                else
                {
                    throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
                }
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format. Response: {request.downloadHandler?.text}", SoilExceptionErrorCode.InvalidResponse);
            }
        }

        /// <summary>
        /// Sets the balance of a virtual currency to an absolute value.
        /// </summary>
        /// <param name="currencyID">The identifier of the virtual currency.</param>
        /// <param name="newBalance">The new balance to set.</param>
        /// <returns>The updated virtual currency object.</returns>
        public static async UniTask<UserVirtualCurrency> SetVirtualCurrency(string currencyID, int newBalance)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying virtual currency balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", currencyID },
                { "balance", newBalance }
            };

            var currencyResponse = await ExecuteRequest<UserVirtualCurrency>(VirtualCurrencyBaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetVirtualCurrency(currencyResponse);
            return currencyResponse;
        }

        /// <summary>
        /// Sets the balance of an inventory item to an absolute value.
        /// </summary>
        /// <param name="itemID">The identifier of the inventory item.</param>
        /// <param name="newBalance">The new balance to set.</param>
        /// <returns>The updated inventory item object.</returns>
        public static async UniTask<UserInventoryItem> SetInventoryItem(string itemID, int newBalance)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying inventory item balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", itemID },
                { "balance", newBalance }
            };

            var itemResponse = await ExecuteRequest<UserInventoryItem>(InventoryBaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetInventoryItem(itemResponse);
            return itemResponse;
        }

        /// <summary>
        /// Increases the balance of a virtual currency by a specified amount.
        /// </summary>
        /// <param name="currencyID">The identifier of the virtual currency.</param>
        /// <param name="amountToAdd">The amount to add to the current balance.</param>
        /// <returns>The updated virtual currency object.</returns>
        public static async UniTask<UserVirtualCurrency> IncreaseVirtualCurrency(string currencyID, int amountToAdd)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying virtual currency balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", currencyID },
                { "amount", amountToAdd }
            };

            var currencyResponse = await ExecuteRequest<UserVirtualCurrency>(VirtualCurrencyIncreaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetVirtualCurrency(currencyResponse);
            return currencyResponse;
        }

        /// <summary>
        /// Decreases the balance of a virtual currency by a specified amount.
        /// </summary>
        /// <param name="currencyID">The identifier of the virtual currency.</param>
        /// <param name="amountToSubtract">The amount to subtract from the current balance.</param>
        /// <returns>The updated virtual currency object.</returns>
        public static async UniTask<UserVirtualCurrency> DecreaseVirtualCurrency(string currencyID, int amountToSubtract)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying virtual currency balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", currencyID },
                { "amount", amountToSubtract }
            };

            var currencyResponse = await ExecuteRequest<UserVirtualCurrency>(VirtualCurrencyDecreaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetVirtualCurrency(currencyResponse);
            return currencyResponse;
        }

        /// <summary>
        /// Increases the balance of an inventory item by a specified amount.
        /// </summary>
        /// <param name="itemID">The identifier of the inventory item.</param>
        /// <param name="amountToAdd">The amount to add to the current balance.</param>
        /// <returns>The updated inventory item object.</returns>
        public static async UniTask<UserInventoryItem> IncreaseInventoryItem(string itemID, int amountToAdd)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying inventory item balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", itemID },
                { "amount", amountToAdd }
            };

            var itemResponse = await ExecuteRequest<UserInventoryItem>(InventoryIncreaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetInventoryItem(itemResponse);
            return itemResponse;
        }

        /// <summary>
        /// Decreases the balance of an inventory item by a specified amount.
        /// </summary>
        /// <param name="itemID">The identifier of the inventory item.</param>
        /// <param name="amountToSubtract">The amount to subtract from the current balance.</param>
        /// <returns>The updated inventory item object.</returns>
        public static async UniTask<UserInventoryItem> DecreaseInventoryItem(string itemID, int amountToSubtract)
        {
            if (!Ready)
            {
                throw new SoilException("Economy service is not initialized. Initialize before modifying inventory item balances.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "identifier", itemID },
                { "amount", amountToSubtract }
            };

            var itemResponse = await ExecuteRequest<UserInventoryItem>(InventoryDecreaseUrl, UnityWebRequest.kHttpVerbPOST, payload);
            EconomyPlayerPrefs.SetInventoryItem(itemResponse);
            return itemResponse;
        }
    }
}