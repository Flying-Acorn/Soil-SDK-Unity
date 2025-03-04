using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    internal static class ThirdPartyAPIHandler
    {
        private static readonly string SocialBaseUrl = $"{Authenticate.UserBaseUrl}/social";

        private static readonly string LinkUserUrl = $"{SocialBaseUrl}/link/";
        private static readonly string UnlinkUserUrl = $"{SocialBaseUrl}/unlink/";

        private static HttpClient _linkClient;

        private static Dictionary<string, object> GetLinkUserPayload(LinkAccountInfo thirdPartyUser,
            ThirdPartySettings settings)
        {
            var party = GetParty(settings);
            return new Dictionary<string, object>
            {
                { "app_party", party.id },
                { "social_account_info", thirdPartyUser }
            };
        }

        private static AppParty GetParty(ThirdPartySettings settings)
        {
            if (SoilServices.UserInfo.linkable_parties == null)
                throw new SoilException("No parties to link to", SoilExceptionErrorCode.InvalidRequest);
            var party = SoilServices.UserInfo.linkable_parties.Find(p => p.party == settings.ThirdParty);
            if (party == null)
                throw new SoilException("Third party not found in linkable parties",
                    SoilExceptionErrorCode.InvalidRequest);
            return party;
        }

        [UsedImplicitly]
        internal static async Task<LinkPostResponse> Link(LinkAccountInfo thirdPartyUser,
            ThirdPartySettings settings)
        {
            var payload = GetLinkUserPayload(thirdPartyUser, settings);
            var stringBody = JsonConvert.SerializeObject(payload);

            _linkClient?.Dispose();
            _linkClient = new HttpClient();
            _linkClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _linkClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _linkClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, LinkUserUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await _linkClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while updating player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var linkResponse = JsonConvert.DeserializeObject<LinkPostResponse>(responseString);
            if (linkResponse == null)
            {
                throw new SoilException("Link response is null", SoilExceptionErrorCode.InvalidResponse);
            }

            switch (linkResponse.detail.code)
            {
                case Constants.LinkStatus.LinkFound:
                    if (linkResponse.alternate_user == null)
                        throw new SoilException("Link found but alternate user is null",
                            SoilExceptionErrorCode.Conflict);
                    var tokens = linkResponse.alternate_user.tokens;
                    UserApiHandler.ReplaceUser(linkResponse.alternate_user, tokens);
                    break;
                case Constants.LinkStatus.AlreadyLinked:
                case Constants.LinkStatus.LinkCreated:
                    break;
                case Constants.LinkStatus.LinkDeleted:
                case Constants.LinkStatus.AnotherLinkExists:
                case Constants.LinkStatus.LinkNotFound:
                case Constants.LinkStatus.PartyNotFound:
                default:
                    throw new SoilException(
                        $"Unaccepted link status: {linkResponse.detail.code} - {linkResponse.detail.message}",
                        SoilExceptionErrorCode.InvalidResponse);
            }

            LinkingPlayerPrefs.AddLink(linkResponse);

            return linkResponse;
        }

        [UsedImplicitly]
        internal static async Task<LinkGetResponse> GetLinks()
        {
            _linkClient?.Dispose();
            _linkClient = new HttpClient();
            _linkClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _linkClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _linkClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, LinkUserUrl);
            var response = await _linkClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LinkingPlayerPrefs.Links = new List<LinkPostResponse>();
                return new LinkGetResponse()
                {
                    detail = new LinkStatusResponse
                    {
                        code = Constants.LinkStatus.LinkNotFound,
                        message = Constants.LinkStatus.LinkNotFound.ToString()
                    },
                    linked_accounts = new List<LinkPostResponse>()
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while fetching player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var getLinksResponse = JsonConvert.DeserializeObject<LinkGetResponse>(responseString);
            LinkingPlayerPrefs.Links = getLinksResponse.linked_accounts;
            return getLinksResponse;
        }

        [UsedImplicitly]
        internal static async Task<UnlinkResponse> Unlink(ThirdPartySettings settings)
        {
            var body = new Dictionary<string, object>
            {
                { "app_party", GetParty(settings).id }
            };

            var stringBody = JsonConvert.SerializeObject(body);
            _linkClient?.Dispose();
            _linkClient = new HttpClient();
            _linkClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            _linkClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _linkClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, UnlinkUserUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await _linkClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UnlinkResponse
                {
                    detail = new LinkStatusResponse
                    {
                        code = Constants.LinkStatus.LinkNotFound,
                        message = Constants.LinkStatus.LinkNotFound.ToString()
                    }
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while updating player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var unlinkResponse = JsonConvert.DeserializeObject<UnlinkResponse>(responseString);
            LinkingPlayerPrefs.RemoveLink(unlinkResponse);
            return unlinkResponse;
        }
    }
}