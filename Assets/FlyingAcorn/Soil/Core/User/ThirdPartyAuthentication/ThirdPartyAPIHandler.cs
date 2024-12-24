using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public static class ThirdPartyAPIHandler
    {
        private static readonly string SocialBaseUrl = $"{Authenticate.UserBaseUrl}/social";

        private static readonly string LinkUserUrl = $"{SocialBaseUrl}/link/";
        private static readonly string UnlinkUserUrl = $"{SocialBaseUrl}/unlink/";

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
                throw new Exception("No parties to link to");
            var party = SoilServices.UserInfo.linkable_parties.Find(p => p.party == settings.ThirdParty.ToString());
            if (party == null)
                throw new Exception("Third party not found in linkable parties");
            return party;
        }

        [UsedImplicitly]
        internal static async Task<LinkPostResponse> Link(LinkAccountInfo thirdPartyUser,
            ThirdPartySettings settings)
        {
            var payload = GetLinkUserPayload(thirdPartyUser, settings);
            var stringBody = JsonConvert.SerializeObject(payload);
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, LinkUserUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while updating player info. Response: {responseString}");
            }

            var linkResponse = JsonConvert.DeserializeObject<LinkPostResponse>(responseString);
            if (linkResponse == null)
            {
                throw new Exception("Link response is null");
            }

            switch (linkResponse.detail.code)
            {
                case (int)Constants.LinkStatus.LinkFound:
                    if (linkResponse.alternate_user == null)
                        throw new Exception("Link found but alternate user is null");
                    var tokens = linkResponse.alternate_user.tokens;
                    UserApiHandler.ReplaceUser(linkResponse.alternate_user, tokens);
                    break;
                case (int)Constants.LinkStatus.AlreadyLinked:
                case (int)Constants.LinkStatus.LinkCreated:
                    break;
                default:
                    throw new Exception(
                        $"Unaccepted link status: {linkResponse.detail.code} - {linkResponse.detail.message}");
            }

            LinkingPlayerPrefs.AddLink(linkResponse);

            return linkResponse;
        }

        [UsedImplicitly]
        internal static async Task<LinkGetResponse> GetLinks()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, LinkUserUrl);
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                LinkingPlayerPrefs.Links = new List<LinkPostResponse>();
                return new LinkGetResponse()
                {
                    detail = new LinkStatusResponse
                    {
                        code = (int)Constants.LinkStatus.LinkNotFound,
                        message = Constants.LinkStatus.LinkNotFound.ToString()
                    },
                    linked_accounts = new List<LinkPostResponse>()
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while fetching player info. Response: {responseString}");
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
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, UnlinkUserUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = client.SendAsync(request).Result;
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UnlinkResponse
                {
                    detail = new LinkStatusResponse
                    {
                        code = (int)Constants.LinkStatus.LinkNotFound,
                        message = Constants.LinkStatus.LinkNotFound.ToString()
                    }
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while updating player info. Response: {responseString}");
            }

            var unlinkResponse = JsonConvert.DeserializeObject<UnlinkResponse>(responseString);
            LinkingPlayerPrefs.RemoveLink(unlinkResponse);
            return unlinkResponse;
        }
    }
}