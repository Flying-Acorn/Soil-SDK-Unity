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
        private static bool _eventsSubscribed;
        private static readonly string PurchasingBaseUrl = $"{Core.Data.Constants.ApiUrl}/iap";
        private static readonly string PurchaseBaseUrl = $"{PurchasingBaseUrl}/purchase";

        private static readonly string ItemsUrl = $"{PurchasingBaseUrl}/items/";
        private static readonly string CreatePurchaseUrl = $"{PurchaseBaseUrl}/";
        private static readonly string VerifyPurchaseUrl = $"{PurchaseBaseUrl}/verify/";
        private static readonly string PurchaseInvoiceUrl = PurchaseBaseUrl + "{purchase_id}/invoice/";

        [UsedImplicitly] public static Action<Item> OnPurchaseStart;
        [UsedImplicitly] public static Action<Purchase> OnPurchaseSuccessful;
        [UsedImplicitly] public static Action<List<Item>> OnItemsReceived;
        [UsedImplicitly] public static Action OnPurchasingInitialized;
        [UsedImplicitly] public static List<Item> AvailableItems => AllItems.FindAll(item => item.enabled);

        private static Action<CreateResponse> _onCreatePurchaseResponse;

        private static List<Item> AllItems
        {
            get => PurchasingPlayerPrefs.CachedItems;
            set => PurchasingPlayerPrefs.CachedItems = value;
        }

        public static bool Ready { get; private set; }

        public static async Task Initialize()
        {
            await SoilServices.Initialize();
            
            if (_eventsSubscribed)
                return;
            _eventsSubscribed = true;
            OnPurchasingInitialized -= SafeVerifyAllPurchases;
            OnPurchasingInitialized += SafeVerifyAllPurchases;
            OnItemsReceived -= PurchasingInitialized;
            OnItemsReceived += PurchasingInitialized;
            _ = QueryItems();
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

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, ItemsUrl);

            var response = await client.SendAsync(request);
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
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response is not { IsSuccessStatusCode: true })
                throw new Exception($"FlyingAcorn ====> Failed to buy item. Error: {responseString}");

            var createResponse = JsonConvert.DeserializeObject<CreateResponse>(responseString);
            OnPurchaseCreated(createResponse);
        }

        private static async Task VerifyPurchase(string purchaseId)
        {
            await Initialize();

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
            var response = await client.SendAsync(request);
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
                OnPurchaseSuccessful?.Invoke(purchase);
        }

        [UsedImplicitly]
        public static async void SafeVerifyAllPurchases()
        {
            foreach (var purchaseId in PurchasingPlayerPrefs.UnverifiedPurchaseIds)
            {
                try
                {
                    await VerifyPurchase(purchaseId);
                }
                catch (Exception e)
                {
                    var message = $"Failed to verify {purchaseId} - {e} - {e.StackTrace}";
                    MyDebug.LogWarning(message);
                }
            }
        }
    }
}