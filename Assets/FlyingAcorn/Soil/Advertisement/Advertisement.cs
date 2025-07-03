using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using FlyingAcorn.Soil.Advertisement.Models;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using System.Linq;
using FlyingAcorn.Soil.Advertisement.Data;

namespace FlyingAcorn.Soil.Advertisement
{
    public class Advertisement
    {
        [UsedImplicitly]
        public static bool Ready => SoilServices.Ready;
        private static readonly string AdvertisementBaseUrl = $"{Core.Data.Constants.ApiUrl}/advertisement/";
        private static readonly string CampaignsUrl = $"{AdvertisementBaseUrl}campaigns/";
        private static readonly string CampaignsSelectUrl = $"{CampaignsUrl}select/";
        private static bool _campaignRequested;
        private static Campaign availableCampaign = null;

        public static async void InitializeAsync(List<AdFormat> adFormats)
        {
            if (adFormats == null || adFormats.Count == 0)
                throw new SoilException("Ad formats cannot be null or empty.", SoilExceptionErrorCode.InvalidRequest);

            if (availableCampaign != null)
                return;

            adFormats = adFormats?.Distinct().ToList() ?? new List<AdFormat>();

            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Events.InvokeOnInitializeFailed(e.Message);
                return;
            }

            if (_campaignRequested)
                return;

            try
            {
                _campaignRequested = true;
                availableCampaign = (await SelectCampaignAsync()).campaign;
                CacheAds(availableCampaign, adFormats);
                Events.InvokeOnInitialized();
            }
            catch (SoilException ex)
            {
                _campaignRequested = false;
                Events.InvokeOnInitializeFailed($"Failed to select campaign: {ex.Message} - {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                _campaignRequested = false;
                Events.InvokeOnInitializeFailed($"Unexpected error while selecting campaign: {ex.Message}");
            }
            _campaignRequested = false;
        }

        // Downloads and caches ads for the selected campaign.
        // This method should be implemented to handle the actual caching logic.
        // It might involve downloading ad assets, storing them locally, etc.
        // Use AdvertisementPlayerPrefs to store cached campaigns.
        private static void CacheAds(Campaign availableCampaign, List<AdFormat> adFormats)
        {
            AdvertisementPlayerPrefs.CachedCampaign = availableCampaign;
            return;
        }

        private static async Task<CampaignSelectResponse> SelectCampaignAsync()
        {
            if (!Ready)
            {
                throw new SoilException("Soil services are not ready. Please initialize Soil first.",
                    SoilExceptionErrorCode.NotReady);
            }

            using var campaignRequest = new HttpClient();
            campaignRequest.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            campaignRequest.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            campaignRequest.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, CampaignsSelectUrl);

            var body = new
            {
                previous_campaigns = new List<string>() // TODO: Get previous campaigns from user preferences or session
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string content;

            try
            {
                response = await campaignRequest.SendAsync(request);
                content = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while selecting campaign",
                    SoilExceptionErrorCode.Timeout);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while selecting campaign: {ex.Message}",
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while selecting campaign: {ex.Message}",
                    SoilExceptionErrorCode.Unknown);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => SoilExceptionErrorCode.InvalidToken,
                    System.Net.HttpStatusCode.Forbidden => SoilExceptionErrorCode.Forbidden,
                    System.Net.HttpStatusCode.NotFound => SoilExceptionErrorCode.NotFound,
                    System.Net.HttpStatusCode.BadRequest => SoilExceptionErrorCode.InvalidRequest,
                    System.Net.HttpStatusCode.ServiceUnavailable => SoilExceptionErrorCode.ServiceUnavailable,
                    _ => SoilExceptionErrorCode.TransportError
                };

                throw new SoilException($"Failed to select campaign: {response.StatusCode} - {content}", errorCode);
            }

            try
            {
                return JsonConvert.DeserializeObject<CampaignSelectResponse>(content);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while selecting campaign. Response: {content}",
                    SoilExceptionErrorCode.InvalidResponse);
            }
        }
    }
}