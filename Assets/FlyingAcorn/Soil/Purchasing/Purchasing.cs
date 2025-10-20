using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Purchasing.Models;
using FlyingAcorn.Soil.Purchasing.Models.Responses;
using static FlyingAcorn.Soil.Purchasing.DeeplinkHandler;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class Purchasing
    {
        private static bool _isInitializing;
        private static bool _initialized;
        public static bool Ready => _initialized && SoilServices.Ready;
        private static string PurchasingAPIUrl => PurchasingPlayerPrefs.SavedSettings?.api ?? Constants.ApiUrl;
        private static string PurchaseBaseUrl => $"{PurchasingAPIUrl}/purchase";
        private static string ItemsUrl => $"{PurchasingAPIUrl}/items/";
        private static string CreatePurchaseUrl => $"{PurchaseBaseUrl}/";
        private static string VerifyPurchaseUrl => $"{PurchaseBaseUrl}/verify/";
        private static string BatchVerifyPurchaseUrl => $"{PurchaseBaseUrl}/batchverify/";
        private static string PurchaseInvoiceUrl => PurchaseBaseUrl + "/{purchase_id}/invoice/";

        private static string _apiAtItemsFetch;                // API base used when we first (or last) fetched items
        private static string _lastItemsQueryApi;              // Last API actually used inside QueryItems()
        private static bool _itemsQueried;                     // Whether we have successfully performed at least one QueryItems()

        [UsedImplicitly] public static Action<Item> OnPurchaseStart;
        [UsedImplicitly] public static Action<Purchase> OnPurchaseSuccessful;
        [UsedImplicitly] public static Action<SoilException> OnItemsFailed;
        [UsedImplicitly] public static Action<List<Item>> OnItemsReceived;
        [UsedImplicitly] public static Action OnPurchasingInitialized;
        [UsedImplicitly] public static Action<SoilException> OnInitializationFailed;
        [UsedImplicitly] public static Action<Dictionary<string, string>> OnDeeplinkActivated;
        private static UniTask? _verifyTask;
        private static bool _verifyOnInitialize;

        private static UniTaskCompletionSource<bool> _remoteConfigCompletionSource;

        [UsedImplicitly]
        public static List<Item> AvailableItems => PurchasingPlayerPrefs.CachedItems.FindAll(item => item.enabled);

        public static void Initialize(bool verifyOnInitialize = true)
        {
            if (_initialized && !SoilServices.Ready)
            {
                _initialized = false; // allow re-initialization events to fire again
            }

            if (Ready || _isInitializing)
                return;
            _isInitializing = true;

            _verifyOnInitialize = verifyOnInitialize;

            if (SoilServices.Ready)
            {
                InitializeInternal();
                return;
            }
            UnsubscribeFromCore();
            SoilServices.OnServicesReady += InitializeInternal;
            SoilServices.OnInitializationFailed += InitFailed;
            SoilServices.InitializeAsync();
        }

        private static void InitFailed(SoilException exception)
        {
            _isInitializing = false;
            MyDebug.Info($"FlyingAcorn ====> Purchasing initialization failed: {exception.Message}");
            UnsubscribeFromCore();
            OnInitializationFailed?.Invoke(exception);
        }

        private static void OnPurchasingInitializeFailed(SoilException exception)
        {
            InitFailed(exception);
        }

        private static void UnsubscribeFromCore()
        {
            SoilServices.OnServicesReady -= InitializeInternal;
            SoilServices.OnInitializationFailed -= InitFailed;
        }

        private static void UnsubscribeFromRemoteConfig()
        {
            RemoteConfig.RemoteConfig.OnServerAnswer -= OnRemoteConfigAnswer;
            RemoteConfig.RemoteConfig.OnSuccessfulFetch -= OnRemoteConfigSuccess;
        }

        private static void OnRemoteConfigAnswer(bool success)
        {
            MyDebug.Verbose($"[Purchasing] Remote config fetch completed with success: {success}");
            _remoteConfigCompletionSource?.TrySetResult(success);
            _remoteConfigCompletionSource = null;
            try
            {
                // Apply settings only on success; if we previously timed out and already queried items with fallback API,
                // we may need to re-query if API changed.
                if (success)
                {
                    ApplyPurchasingSettingsFromRemoteConfigAndMaybeRequery("OnServerAnswer-success");
                }
            }
            finally
            {
                UnsubscribeFromRemoteConfig();
            }
        }

        private static void OnRemoteConfigSuccess(Newtonsoft.Json.Linq.JObject config)
        {
            MyDebug.Verbose("[Purchasing] Remote config successfully fetched and applied");
        }

        private static async UniTaskVoid EnsureRemoteConfigAndProceed()
        {
            if (RemoteConfig.RemoteConfig.IsFetchedAndReady)
            {
                SetSettingsFromRemoteConfig();
                MyDebug.Info("[Purchasing] Remote config already ready, applying settings and proceeding with QueryItems");
                QueryItems().Forget();
                return;
            }

            try
            {
                await EnsureRemoteConfigReady();
                MyDebug.Verbose("[Purchasing] Remote config fetch completed, proceeding with QueryItems");
                QueryItems().Forget();
            }
            catch (Exception ex)
            {
                MyDebug.Info($"[Purchasing] Failed to ensure remote config: {ex.Message}, proceeding with defaults");
                QueryItems().Forget();
            }
        }

        private static void SetSettingsFromRemoteConfig()
        {
            try
            {
                PurchasingSettings fetchedPurchasingSettings = null;
                if (RemoteConfig.RemoteConfigPlayerPrefs.CachedRemoteConfigData != null &&
                    RemoteConfig.RemoteConfigPlayerPrefs.CachedRemoteConfigData.ContainsKey(RemoteConfig.Constants.PurchasingSettingsKey))
                {
                    fetchedPurchasingSettings = JsonConvert.DeserializeObject<PurchasingSettings>(
                        RemoteConfig.RemoteConfigPlayerPrefs.CachedRemoteConfigData[RemoteConfig.Constants.PurchasingSettingsKey].ToString());
                }
                _ = PurchasingPlayerPrefs.SetAlternateSettings(fetchedPurchasingSettings);
            }
            catch (Exception e)
            {
                try
                {
                    _ = PurchasingPlayerPrefs.SetAlternateSettings(null);

                    try
                    {
                        MyDebug.LogError($"Failed to parse remote config purchasing settings. Exception: {e.GetType().Name}: {e.Message}");
                    }
                    catch
                    {
                        MyDebug.LogError($"Failed to log error for remote config purchasing settings. Exception: {e}");
                    }
                }
                catch
                {
                    MyDebug.LogError($"Failed to set alternate purchasing settings to null. Response: {e}");
                }
            }
        }

        private static async UniTask EnsureRemoteConfigReady()
        {
            // Fast path: already fetched
            if (RemoteConfig.RemoteConfig.IsFetchedAndReady)
            {
                MyDebug.Verbose("[Purchasing] Remote config already available (Scenario 1: already ready)");
                return;
            }

            // If another fetch is in progress, just attach
            if (RemoteConfig.RemoteConfig.IsFetching)
            {
                MyDebug.Verbose("[Purchasing] Remote config fetch in progress elsewhere, subscribing (Scenario: parallel fetch)");
            }
            else
            {
                MyDebug.Verbose("[Purchasing] Remote config not ready, initiating fetch (Scenario: cold start)");
                RemoteConfig.RemoteConfig.FetchConfig();
            }

            if (_remoteConfigCompletionSource == null)
            {
                _remoteConfigCompletionSource = new UniTaskCompletionSource<bool>();
            }

            // Ensure we have only one subscription set
            UnsubscribeFromRemoteConfig();
            RemoteConfig.RemoteConfig.OnServerAnswer += OnRemoteConfigAnswer;
            RemoteConfig.RemoteConfig.OnSuccessfulFetch += OnRemoteConfigSuccess; // kept for potential future use / diagnostics

            try
                {
                    var timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
                    var start = DateTime.UtcNow;
                    while ((DateTime.UtcNow - start) < timeout && _remoteConfigCompletionSource != null && !_remoteConfigCompletionSource.Task.Status.IsCompleted())
                    {
                        // small yield slices to avoid busy wait (no Thread.Sleep to keep compatibility with player loop)
                        await UniTask.Delay(100);
                    }
                    if (_remoteConfigCompletionSource != null && !_remoteConfigCompletionSource.Task.Status.IsCompleted())
                    {
                        MyDebug.Info("[Purchasing] Remote config fetch timed out, continuing with defaults while awaiting late response");
                    }
                    else
                    {
                        MyDebug.Verbose("[Purchasing] Remote config wait completed (event fired before timeout)");
                    }
            }
            catch (Exception ex)
            {
                MyDebug.Info($"[Purchasing] Error waiting for remote config: {ex.Message}; proceeding with defaults");
            }
            // Do NOT unsubscribe here; allow late success to arrive and trigger re-query if needed.
        }

        private static void InitializeInternal()
        {
            UnsubscribeFromCore();
            OnPaymentDeeplinkActivated -= OpenInvoice;
            OnPaymentDeeplinkActivated += OpenInvoice;
            if (_verifyOnInitialize)
            {
                OnPurchasingInitialized -= SafeVerifyAllPurchases;
                OnPurchasingInitialized += SafeVerifyAllPurchases;
            }

            OnItemsFailed -= OnPurchasingInitializeFailed;
            OnItemsFailed += OnPurchasingInitializeFailed;

            EnsureRemoteConfigAndProceed().Forget();
        }

        public static void DeInitialize()
        {
            UnsubscribeFromCore();
            UnsubscribeFromRemoteConfig();
            _isInitializing = false;
            _initialized = false;
            _verifyTask = null;
            _verifyOnInitialize = false;
            _remoteConfigCompletionSource?.TrySetResult(false);
            _remoteConfigCompletionSource = null;
            OnPaymentDeeplinkActivated = null;
            OnPurchasingInitialized = null;
            OnItemsReceived = null;
            OnItemsFailed = null;
            OnInitializationFailed = null;
            OnPurchaseStart = null;
            OnPurchaseSuccessful = null;
        }

        private static void OpenInvoice(Dictionary<string, string> obj)
        {
            var parametersString = string.Join("&", obj.Select(pair => $"{pair.Key}={pair.Value}"));
            MyDebug.Info($"User returned from payment. Parameters: {parametersString}");
            OnDeeplinkActivated?.Invoke(obj);
        }

        private static void CompleteInitialization()
        {
            if (_initialized)
                return;
            _isInitializing = false;
            _initialized = true;
            OnPurchasingInitialized?.Invoke();
        }

        private static async UniTaskVoid QueryItems()
        {
            try
            {
                _apiAtItemsFetch = PurchasingAPIUrl; // Snapshot of API when starting this query
                _lastItemsQueryApi = _apiAtItemsFetch;
                using var request = UnityWebRequest.Get(ItemsUrl);
                var authHeader = Authenticate.GetAuthorizationHeaderString();
                if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
                request.SetRequestHeader("Accept", "application/json");
                try
                {
                    await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
                }
                catch (SoilException) { throw; }
                catch (Exception ex)
                {
                    throw new SoilException($"Unexpected error while querying items: {ex.Message}", SoilExceptionErrorCode.TransportError);
                }

                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    var body = request.downloadHandler?.text ?? string.Empty;
                    throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
                }

                var responseString = request.downloadHandler?.text ?? string.Empty;
                try
                {
                    PurchasingPlayerPrefs.CachedItems = JsonConvert.DeserializeObject<ItemsResponse>(responseString).items;
                }
                catch (Exception)
                {
                    throw new SoilException($"Failed to deserialize items. Response: {responseString}", SoilExceptionErrorCode.InvalidResponse);
                }

                CompleteInitialization();
                _itemsQueried = true;
                OnItemsReceived?.Invoke(AvailableItems);
            }
            catch (SoilException ex)
            {
                MyDebug.Info($"FlyingAcorn ====> Failed to query items: {ex.Message}");
                OnItemsFailed?.Invoke(ex);
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Unexpected error while querying items: {ex.Message}");
                var soilEx = new SoilException($"Unexpected error while querying items: {ex.Message}", SoilExceptionErrorCode.TransportError);
                OnItemsFailed?.Invoke(soilEx);
            }
        }

        // Applies remote config purchasing settings and triggers a re-query if API base changed after a timeout fallback.
        private static void ApplyPurchasingSettingsFromRemoteConfigAndMaybeRequery(string source)
        {
            try
            {
                SetSettingsFromRemoteConfig();
                var currentApi = PurchasingAPIUrl;
                var baselineApi = _lastItemsQueryApi ?? _apiAtItemsFetch;
                if (_itemsQueried && !string.IsNullOrEmpty(baselineApi) && baselineApi != currentApi)
                {
                    MyDebug.Verbose($"[Purchasing] Purchasing API changed from {baselineApi} to {currentApi} after remote config ({source}), re-querying items");
                    _apiAtItemsFetch = currentApi;
                    _lastItemsQueryApi = currentApi;
                    QueryItems().Forget();
                }
                else
                {
                    MyDebug.Verbose($"[Purchasing] Remote config applied ({source}); no re-query needed (baseline={baselineApi}, current={currentApi}, itemsQueried={_itemsQueried})");
                }
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Purchasing] Exception while applying remote config purchasing settings: {e.Message}");
            }
        }

        [UsedImplicitly]
        public static async UniTask BuyItem(string sku)
        {
            if (!Ready)
            {
                MyDebug.Info("FlyingAcorn ====> Purchasing not ready yet");
                return;
            }

            var extraData = new Dictionary<string, object>()
            {
                { "registered_deep_link", PurchasingPlayerPrefs.GetPurchaseDeeplink() }
            };
            var payload = new Dictionary<string, object>
            {
                { "sku", sku },
                { "preferred_currency", null },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() },
                { "extra_data", extraData }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            using var request = new UnityWebRequest(CreatePurchaseUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while buying item: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var body = request.downloadHandler?.text ?? string.Empty;
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
            }
            var createResponse = JsonConvert.DeserializeObject<CreateResponse>(request.downloadHandler.text);
            OnPurchaseCreated(createResponse);
        }

        [UsedImplicitly]
        public static async UniTask VerifyPurchase(string purchaseId)
        {
            if (!Ready)
            {
                MyDebug.Info("FlyingAcorn ====> Purchasing not ready yet");
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "purchase_id", purchaseId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);
            using var request = new UnityWebRequest(VerifyPurchaseUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while verifying purchase: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }
            if (request.responseCode == (long)System.Net.HttpStatusCode.NotFound)
            {
                OnVerificationResponse(new VerifyResponse { purchase = new Purchase { purchase_id = purchaseId, expired = true } });
                return;
            }
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var body = request.downloadHandler?.text ?? string.Empty;
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
            }
            var verifyResponse = JsonConvert.DeserializeObject<VerifyResponse>(request.downloadHandler.text);
            OnVerificationResponse(verifyResponse);
        }

        [UsedImplicitly]
        public static async UniTask BatchVerifyPurchases(List<string> purchaseIds)
        {
            if (!Ready)
            {
                MyDebug.Info("FlyingAcorn ====> Purchasing not ready yet");
                return;
            }

            if (purchaseIds.Count == 0)
                return;
            var payload = new Dictionary<string, object>
            {
                { "purchase_ids", purchaseIds },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);
            using var request = new UnityWebRequest(BatchVerifyPurchaseUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout * 2);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while batch verifying purchases: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var body = request.downloadHandler?.text ?? string.Empty;
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
            }
            var batchVerifyResponse = JsonConvert.DeserializeObject<BatchVerifyResponse>(request.downloadHandler.text);
            foreach (var purchase in batchVerifyResponse.purchases)
            {
                OnVerificationResponse(new VerifyResponse { purchase = purchase });
            }
        }

        private static void OnPurchaseCreated(CreateResponse response)
        {
            var purchase = response.purchase;
            PurchasingPlayerPrefs.AddUnverifiedPurchaseId(purchase.purchase_id);
            Application.OpenURL(purchase.pay_url);
            OnPurchaseStart?.Invoke(PurchasingPlayerPrefs.CachedItems.Find(item => item.sku == purchase.sku));
        }

        [UsedImplicitly]
        public static void OpenInvoice(string purchaseId)
        {
            Application.OpenURL(PurchaseInvoiceUrl.Replace("{purchase_id}", purchaseId));
        }

        private static void OnVerificationResponse(VerifyResponse response)
        {
            var purchase = response.purchase;
            if (!PurchasingPlayerPrefs.UnverifiedPurchaseIds.Contains(purchase.purchase_id))
            {
                MyDebug.Info($"FlyingAcorn ====> Purchase {purchase.purchase_id} already verified");
                return;
            }
            if (purchase.paid || purchase.expired)
            {
                MyDebug.Info($"FlyingAcorn ====> Removing purchase {purchase.purchase_id} cause it's paid or expired");
                PurchasingPlayerPrefs.RemoveUnverifiedPurchaseId(purchase.purchase_id);
            }
            if (!PurchasingPlayerPrefs.CachedItems.Exists(item => item.sku == purchase.sku))
            {
                MyDebug.Info($"FlyingAcorn ====> Removing purchase {purchase.purchase_id} cause item {purchase.sku} not found");
                PurchasingPlayerPrefs.RemoveUnverifiedPurchaseId(purchase.purchase_id);
            }
            if (purchase.paid)
                OnPurchaseSuccessful?.Invoke(purchase);
        }

        public static void RollbackUnpaidPurchases()
        {
            MyDebug.Info("FlyingAcorn ====> Rolling back unpaid purchases");
            var unverifiedPurchaseIds = PurchasingPlayerPrefs.UnverifiedPurchaseIds;
            foreach (var purchaseId in unverifiedPurchaseIds)
                PurchasingPlayerPrefs.RemoveUnverifiedPurchaseId(purchaseId);
        }

        [UsedImplicitly]
        public static async void SafeVerifyAllPurchases()
        {
            try
            {
                if (_verifyTask?.AsTask() is { IsCompleted: false })
                {
                    MyDebug.Info("FlyingAcorn ====> Verify task is already running");
                    return;
                }

                var idsToVerify = PurchasingPlayerPrefs.UnverifiedPurchaseIds;
                _verifyTask = idsToVerify.Count == 1
                    ? VerifyPurchase(idsToVerify[0])
                    : BatchVerifyPurchases(PurchasingPlayerPrefs.UnverifiedPurchaseIds);
                await _verifyTask.Value;
            }
            catch (Exception e)
            {
                var message = $"Failed to batch verify purchases - {e} - {e.StackTrace}";
                MyDebug.LogWarning(message);
            }
            _verifyTask = null;
        }

        internal static async UniTask<bool> HealthCheck(string apiUrl)
        {
            var appID = UserPlayerPrefs.AppID;
            var sdkToken = UserPlayerPrefs.SDKToken;

            var payload = new Dictionary<string, string>
            {
                { "iss", appID }
            };
            var bearerToken = Core.JWTTools.JwtUtils.GenerateJwt(payload, sdkToken);

            try
            {
                using var request = UnityWebRequest.Get($"{apiUrl}/health/");
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
                request.SetRequestHeader("Accept", "application/json");
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
                if (request.responseCode >= 200 && request.responseCode < 300)
                    return true;
                MyDebug.LogWarning($"Purchasing Health Check - Server returned error {(System.Net.HttpStatusCode)request.responseCode} for URL: {apiUrl}");
                return false;
            }
            catch (SoilException sx) when (sx.ErrorCode == SoilExceptionErrorCode.Timeout)
            {
                MyDebug.LogWarning($"Purchasing Health Check - Request timed out for URL: {apiUrl} - {sx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning($"Purchasing Health Check - Unexpected error for URL: {apiUrl} - {ex.Message}");
                return false;
            }
        }
    }
}