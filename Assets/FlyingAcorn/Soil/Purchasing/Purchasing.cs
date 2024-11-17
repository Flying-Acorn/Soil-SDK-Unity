using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Purchasing.Models;
using FlyingAcorn.Soil.Purchasing.Models.Responses;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class Purchasing
    {
        private static bool _initCalled;
        private static readonly string PurchasingBaseUrl = $"{Core.Data.Constants.ApiUrl}/iap";
        private static readonly string PurchaseBaseUrl = $"{PurchasingBaseUrl}/purchase";

        private static readonly string ItemsUrl = $"{PurchasingBaseUrl}/items/";
        private static readonly string CreatePurchaseUrl = $"{PurchaseBaseUrl}/";
        private static readonly string VerifyPurchaseUrl = $"{PurchaseBaseUrl}/verify/";
        private static readonly string PurchaseInvoiceUrl = PurchaseBaseUrl + "{purchase_id}/invoice/";

        [UsedImplicitly] public static Action<Item> OnPurchaseStart;
        [UsedImplicitly] public static Action<Item> OnPurchaseSuccessful;
        [UsedImplicitly] public static Action<List<Item>> OnItemsReceived;
        [UsedImplicitly] public static Action OnPurchasingInitialized;
        [UsedImplicitly] public static List<Item> AvailableItems => AllItems.FindAll(item => item.enabled);

        private static Action<CreateResponse> _onCreatePurchaseResponse;

        private static List<Item> AllItems
        {
            get => PurchasingPlayerPrefs.CachedItems;
            set => PurchasingPlayerPrefs.CachedItems = value;
        }

        public static async Task Initialize()
        {
            if (_initCalled)
                return;

            _initCalled = true;
            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                _initCalled = false;
                MyDebug.LogWarning($"Failed to initialize purchasing: {e.Message}");
                return;
            }

            OnItemsReceived += PurchasingInitialized;
            _ = QueryItems();
            OnPurchasingInitialized += VerifyAllPurchases;
        }

        private static void PurchasingInitialized(List<Item> items)
        {
            OnItemsReceived -= PurchasingInitialized;
            OnPurchasingInitialized?.Invoke();
        }

        private static async Task QueryItems()
        {
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to query items: {e.Message}");
                return;
            }


            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, ItemsUrl);

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response is not { IsSuccessStatusCode: true })
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to query items. Error: {responseString}");
                return;
            }

            try
            {
                PurchasingPlayerPrefs.CachedItems = JsonConvert.DeserializeObject<ItemsResponse>(responseString).items;
            }
            catch (Exception)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to deserialize items: {responseString}");
                return;
            }

            OnItemsReceived?.Invoke(AvailableItems);
        }

        public static async Task BuyItem(string sku)
        {
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to buy item: {e.Message}");
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "sku", sku },
                { "preferred_currency", null },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, CreatePurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await client.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to buy item. Error: {e.Message}");

                return;
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to buy item. Error: {responseString}");
                return;
            }

            try
            {
                var createResponse = JsonConvert.DeserializeObject<CreateResponse>(responseString);
                OnPurchaseCreated(createResponse);
            }
            catch (Exception)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to deserialize create response: {responseString}");
            }
        }

        private static async Task VerifyPurchase(string purchaseId)
        {
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to verify purchase: {e.Message}");
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "purchase_id", purchaseId },
                { "preferred_currency", null },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, VerifyPurchaseUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await client.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to verify purchase. Error: {e.Message}");

                return;
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to verify purchase. Error: {responseString}");
                return;
            }

            try
            {
                var verifyResponse = JsonConvert.DeserializeObject<VerifyResponse>(responseString);
                OnVerificationResponse(verifyResponse);
            }
            catch (Exception)
            {
                MyDebug.LogWarning($"FlyingAcorn ====> Failed to deserialize verify response: {responseString}");
            }
        }

        private static void OnPurchaseCreated(CreateResponse response)
        {
            var purchase = response.purchase;
            PurchasingPlayerPrefs.AddUnverifiedPurchaseId(purchase.purchase_id);
            Application.OpenURL(purchase.pay_url);
            OnPurchaseStart?.Invoke(AllItems.Find(item => item.sku == purchase.sku));
        }
        
        [UsedImplicitly]
        public static void OpenInvoice(string purchaseId)
        {
            Application.OpenURL(PurchaseInvoiceUrl.Replace("{purchase_id}", purchaseId));
        }

        private static void OnVerificationResponse(VerifyResponse response)
        {
            var purchase = response.purchase;
            if (purchase.paid || purchase.expired || !AllItems.Exists(item => item.sku == purchase.sku))
                PurchasingPlayerPrefs.RemoveUnverifiedPurchaseId(purchase.purchase_id);
            if (purchase.paid)
                OnPurchaseSuccessful?.Invoke(AllItems.Find(item => item.sku == purchase.sku));
        }

        [UsedImplicitly]
        public static void VerifyAllPurchases()
        {
            foreach (var purchaseId in PurchasingPlayerPrefs.UnverifiedPurchaseIds)
                _ = VerifyPurchase(purchaseId);
        }
    }
}