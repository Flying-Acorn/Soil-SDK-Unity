using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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
        private static bool _eventsSubscribed;
        private static readonly string PurchasingBaseUrl = $"{Constants.ApiUrl}/iap";
        private static readonly string PurchaseBaseUrl = $"{PurchasingBaseUrl}/purchase";

        private static readonly string ItemsUrl = $"{PurchasingBaseUrl}/items/";
        private static readonly string CreatePurchaseUrl = $"{PurchaseBaseUrl}/";
        private static readonly string VerifyPurchaseUrl = $"{PurchaseBaseUrl}/verify/";
        private static readonly string BatchVerifyPurchaseUrl = $"{PurchaseBaseUrl}/batchverify/";
        private static readonly string PurchaseInvoiceUrl = PurchaseBaseUrl + "{purchase_id}/invoice/";

        [UsedImplicitly] public static Action<Item> OnPurchaseStart;
        [UsedImplicitly] public static Action<Purchase> OnPurchaseSuccessful;
        [UsedImplicitly] public static Action<List<Item>> OnItemsReceived;
        [UsedImplicitly] public static Action OnPurchasingInitialized;
        [UsedImplicitly] public static Action<Dictionary<string, string>> OnDeeplinkActivated;
        private static Task _verifyTask;

        [UsedImplicitly]
        public static List<Item> AvailableItems => PurchasingPlayerPrefs.CachedItems.FindAll(item => item.enabled);

        public static bool Ready { get; private set; }

        public static async Task Initialize(bool verifyOnInitialize = true)
        {
            await SoilServices.Initialize();

            if (_eventsSubscribed)
                return;
            _eventsSubscribed = true;
            OnPaymentDeeplinkActivated -= OpenInvoice;
            OnPaymentDeeplinkActivated += OpenInvoice;
            if (verifyOnInitialize)
            {
                OnPurchasingInitialized -= SafeVerifyAllPurchases;
                OnPurchasingInitialized += SafeVerifyAllPurchases;
            }

            OnItemsReceived -= PurchasingInitialized;
            OnItemsReceived += PurchasingInitialized;
            try
            {
                await QueryItems();
            }
            catch (Exception e)
            {
                _eventsSubscribed = false;
                throw new SoilException($"Failed to initialize purchasing: {e.Message}",
                    SoilExceptionErrorCode.ServiceUnavailable);
            }
        }

        public static void DeInitialize()
        {
            _eventsSubscribed = false;
            OnPaymentDeeplinkActivated = null;
            OnPurchasingInitialized = null;
            OnItemsReceived = null;
        }

        private static void OpenInvoice(Dictionary<string, string> obj)
        {
            var parametersString = string.Join("&", obj.Select(pair => $"{pair.Key}={pair.Value}"));
            MyDebug.Info($"User returned from payment. Parameters: {parametersString}");
            OnDeeplinkActivated?.Invoke(obj);
        }

        private static void PurchasingInitialized(List<Item> items)
        {
            OnItemsReceived -= PurchasingInitialized;
            Ready = true;
            OnPurchasingInitialized?.Invoke();
        }

        private static async Task QueryItems()
        {
            using var queryItemsClient = new HttpClient();
            queryItemsClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            queryItemsClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            queryItemsClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, ItemsUrl);

            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await queryItemsClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while querying items", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while querying items: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while querying items: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            try
            {
                PurchasingPlayerPrefs.CachedItems = JsonConvert.DeserializeObject<ItemsResponse>(responseString).items;
            }
            catch (Exception)
            {
                throw new SoilException($"Failed to deserialize items. Response: {responseString}",
                    SoilExceptionErrorCode.InvalidResponse);
            }

            OnItemsReceived?.Invoke(AvailableItems);
        }

        [UsedImplicitly]
        public static async Task BuyItem(string sku)
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

            using var buyClient = new HttpClient();
            buyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            buyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, CreatePurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await buyClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while buying item", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while buying item: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while buying item: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);

            var createResponse = JsonConvert.DeserializeObject<CreateResponse>(responseString);
            OnPurchaseCreated(createResponse);
        }

        [UsedImplicitly]
        public static async Task VerifyPurchase(string purchaseId)
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

            using var verifyClient = new HttpClient();
            verifyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            verifyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, VerifyPurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await verifyClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while verifying purchase", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while verifying purchase: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while verifying purchase: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    OnVerificationResponse(new VerifyResponse
                    {
                        purchase = new Purchase
                        {
                            purchase_id = purchaseId,
                            expired = true
                        }
                    });
                    return;
                }

                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var verifyResponse = JsonConvert.DeserializeObject<VerifyResponse>(responseString);
            OnVerificationResponse(verifyResponse);
        }

        [UsedImplicitly]
        public static async Task BatchVerifyPurchases(List<string> purchaseIds)
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

            using var verifyClient = new HttpClient();
            verifyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            verifyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, BatchVerifyPurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await verifyClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while batch verifying purchases", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while batch verifying purchases: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while batch verifying purchases: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var batchVerifyResponse = JsonConvert.DeserializeObject<BatchVerifyResponse>(responseString);
            foreach (var purchase in batchVerifyResponse.purchases)
            {
                OnVerificationResponse(new VerifyResponse
                {
                    purchase = purchase
                });
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

        // Use this method with caution, it will undo all unpaid purchases
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
                if (_verifyTask is { IsCompleted: false })
                {
                    MyDebug.Info("FlyingAcorn ====> Verify task is already running");
                    return;
                }

                var idsToVerify = PurchasingPlayerPrefs.UnverifiedPurchaseIds;
                _verifyTask = idsToVerify.Count == 1
                    ? VerifyPurchase(idsToVerify[0])
                    : BatchVerifyPurchases(PurchasingPlayerPrefs.UnverifiedPurchaseIds);
                await _verifyTask;
            }
            catch (Exception e)
            {
                var message = $"Failed to batch verify purchases - {e} - {e.StackTrace}";
                MyDebug.LogWarning(message);
            }
            _verifyTask = null;
        }
    }
}