using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine.Networking;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        private static string SocialBaseUrl => $"{Authenticate.UserBaseUrl}/social";
        private static string LinkUserUrl => $"{SocialBaseUrl}/link/";
        private static string UnlinkUserUrl => $"{SocialBaseUrl}/unlink/";

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

        private static FlyingAcorn.Soil.Core.Data.AppParty GetParty(ThirdPartySettings settings)
        {
            if (SoilServices.UserInfo.linkable_parties == null)
                throw new SoilException("No parties to link to", SoilExceptionErrorCode.InvalidRequest);
            var party = SoilServices.UserInfo.linkable_parties.Find(p => p.party.ToThirdParty() == settings.ThirdParty);
            if (party == null)
                throw new SoilException("Third party not found in linkable parties",
                    SoilExceptionErrorCode.InvalidRequest);
            return party;
        }

        [UsedImplicitly]
        internal static async UniTask<LinkPostResponse> Link(LinkAccountInfo thirdPartyUser,
            ThirdPartySettings settings, CancellationToken cancellationToken = default)
        {
            var payload = GetLinkUserPayload(thirdPartyUser, settings);
            var stringBody = JsonConvert.SerializeObject(payload);

            using var request = new UnityWebRequest(LinkUserUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            using var reg = cancellationToken.CanBeCanceled ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); }) : default;
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException sx)
            {
                throw new SoilException($"Request failed while linking account: {sx.Message}", sx.ErrorCode);
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new SoilException("Linking canceled", SoilExceptionErrorCode.Canceled);
                throw new SoilException($"Unexpected error while linking account: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            var linkResponse = JsonConvert.DeserializeObject<LinkPostResponse>(request.downloadHandler.text);
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
        internal static async UniTask<LinkGetResponse> GetLinks(CancellationToken cancellationToken = default)
        {
            using var request = UnityWebRequest.Get(LinkUserUrl);
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");
            using var reg = cancellationToken.CanBeCanceled ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); }) : default;
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException sx)
            {
                throw new SoilException($"Request failed while getting links: {sx.Message}", sx.ErrorCode);
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new SoilException("Get links canceled", SoilExceptionErrorCode.Canceled);
                throw new SoilException($"Unexpected error while getting links: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode == (long)HttpStatusCode.NotFound)
            {
                LinkingPlayerPrefs.Links = new List<LinkPostResponse>();
                return new LinkGetResponse
                {
                    detail = new LinkStatusResponse
                    {
                        code = Constants.LinkStatus.LinkNotFound,
                        message = nameof(Constants.LinkStatus.LinkNotFound)
                    },
                    linked_accounts = new List<LinkPostResponse>()
                };
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            var getLinksResponse = JsonConvert.DeserializeObject<LinkGetResponse>(request.downloadHandler.text);
            LinkingPlayerPrefs.Links = getLinksResponse.linked_accounts;
            return getLinksResponse;
        }

        [UsedImplicitly]
        internal static async UniTask<UnlinkResponse> Unlink(ThirdPartySettings settings, CancellationToken cancellationToken = default)
        {
            var body = new Dictionary<string, object>
            {
                { "app_party", GetParty(settings).id }
            };
            var stringBody = JsonConvert.SerializeObject(body);

            using var request = new UnityWebRequest(UnlinkUserUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            using var reg = cancellationToken.CanBeCanceled ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); }) : default;
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException sx)
            {
                throw new SoilException($"Request failed while unlinking account: {sx.Message}", sx.ErrorCode);
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new SoilException("Unlinking canceled", SoilExceptionErrorCode.Canceled);
                throw new SoilException($"Unexpected error while unlinking account: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode != (long)HttpStatusCode.NotFound && (request.responseCode < 200 || request.responseCode >= 300))
            {
                throw new SoilException($"Server returned error {(HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }
            var unlinkResponse = JsonConvert.DeserializeObject<UnlinkResponse>(request.downloadHandler.text);
            LinkingPlayerPrefs.RemoveLink(unlinkResponse);
            return unlinkResponse;
        }
    }
}