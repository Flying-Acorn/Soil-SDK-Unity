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

        [UsedImplicitly]
        public static List<Item> AvailableItems => PurchasingPlayerPrefs.CachedItems.FindAll(item => item.enabled);

        private static Action<CreateResponse> _onCreatePurchaseResponse;
        private static HttpClient _buyClient;
        private static HttpClient _verifyClient;
        private static HttpClient _queryItemsClient;

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
            _ = QueryItems();
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
            await Initialize();

            _queryItemsClient?.Dispose();
            _queryItemsClient = new HttpClient();
            _queryItemsClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _queryItemsClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _queryItemsClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, ItemsUrl);

            var response = await _queryItemsClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new Exception($"FlyingAcorn ====> Failed to query items. Error: {responseString}");
            }

            try
            {
                PurchasingPlayerPrefs.CachedItems = JsonConvert.DeserializeObject<ItemsResponse>(responseString).items;
            }
            catch (Exception)
            {
                throw new Exception($"FlyingAcorn ====> Failed to deserialize items: {responseString}");
            }

            OnItemsReceived?.Invoke(AvailableItems);
        }

        public static async Task BuyItem(string sku)
        {
            await Initialize();

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

            _buyClient?.Dispose();
            _buyClient = new HttpClient();
            _buyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _buyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, CreatePurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _buyClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response is not { IsSuccessStatusCode: true })
                throw new Exception($"FlyingAcorn ====> Failed to buy item. Error: {responseString}");

            var createResponse = JsonConvert.DeserializeObject<CreateResponse>(responseString);
            OnPurchaseCreated(createResponse);
        }

        public static async Task VerifyPurchase(string purchaseId)
        {
            await Initialize();

            var payload = new Dictionary<string, object>
            {
                { "purchase_id", purchaseId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            _verifyClient?.Dispose();
            _verifyClient = new HttpClient();
            _verifyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _verifyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, VerifyPurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _verifyClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

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

                throw new Exception($"FlyingAcorn ====> Failed to verify item. Error: {responseString}");
            }

            var verifyResponse = JsonConvert.DeserializeObject<VerifyResponse>(responseString);
            OnVerificationResponse(verifyResponse);
        }

        public static async Task BatchVerifyPurchases(List<string> purchaseIds)
        {
            if (purchaseIds.Count == 0)
                return;
            await Initialize();

            var payload = new Dictionary<string, object>
            {
                { "purchase_ids", purchaseIds },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            _verifyClient?.Dispose();
            _verifyClient = new HttpClient();
            _verifyClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _verifyClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, BatchVerifyPurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _verifyClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"FlyingAcorn ====> Failed to batch verify items. Error: {responseString}",
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
                return;
            if (purchase.paid || purchase.expired ||
                !PurchasingPlayerPrefs.CachedItems.Exists(item => item.sku == purchase.sku))
                PurchasingPlayerPrefs.RemoveUnverifiedPurchaseId(purchase.purchase_id);
            if (purchase.paid)
                OnPurchaseSuccessful?.Invoke(purchase);
        }

        [UsedImplicitly]
        public static async void SafeVerifyAllPurchases()
        {
            try
            {
                await BatchVerifyPurchases(PurchasingPlayerPrefs.UnverifiedPurchaseIds);
            }
            catch (Exception e)
            {
                var message = $"Failed to batch verify purchases - {e} - {e.StackTrace}";
                MyDebug.LogWarning(message);
            }
        }
    }
}