using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.CloudSave.Data;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using Newtonsoft.Json;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.CloudSave
{
    public static class CloudSave
    {
        private static string CloudSaveUrl => $"{Constants.ApiUrl}/cloudsave/";
        public static bool Ready => SoilServices.Ready;

        public static async UniTask SaveAsync(string key, object value, bool isPublic = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new SoilException("Key cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }

            if (string.IsNullOrEmpty(value?.ToString()))
            {
                throw new SoilException("Value cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }

            if (!Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot save data.",
                    SoilExceptionErrorCode.NotReady);
            }

            var payload = new Dictionary<string, object>()
            {
                { "key", key },
                { "value", value },
                { "is_public", isPublic }
            };

            var stringBody = JsonConvert.SerializeObject(payload, Formatting.None);

            using var request = new UnityWebRequest(CloudSaveUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException sx)
            {
                throw new SoilException($"Request failed while saving data: {sx.Message}", sx.ErrorCode);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while saving data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var body = request.downloadHandler?.text ?? string.Empty;
                throw new SoilException($"Server returned error {(HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(request.downloadHandler.text);
            if (saveResponse == null)
            {
                throw new SoilException("Failed to save data", SoilExceptionErrorCode.InvalidResponse);
            }

            CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Info($"{key} saved in cloud");
        }

        public static async UniTask<SaveModel> LoadAsync(string key, string otherUserID = null,
            List<Constants.DataScopes> extraScopes = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new SoilException("Key cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            }
            if (!Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot load data.",
                    SoilExceptionErrorCode.NotReady);
            }

            var query = $"?key={key}";
            if (!string.IsNullOrEmpty(otherUserID))
                query += $"&user={otherUserID}";
            if (extraScopes is { Count: > 0 })
                query += $"&extra_scopes={string.Join(",", extraScopes.Distinct())}";
            using var request = new UnityWebRequest($"{CloudSaveUrl}{query}", UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException sx)
            {
                throw new SoilException($"Request failed while loading data: {sx.Message}", sx.ErrorCode);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while loading data: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode == (long)HttpStatusCode.NotFound)
            {
                throw new SoilNotFoundException($"Key {key} not found");
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                var body = request.downloadHandler?.text ?? string.Empty;
                throw new SoilException($"Server returned error {(HttpStatusCode)request.responseCode}: {body}", SoilExceptionErrorCode.TransportError);
            }

            var saveResponse = JsonConvert.DeserializeObject<SaveModel>(request.downloadHandler.text);
            if (saveResponse == null)
            {
                throw new SoilException("Failed to load data", SoilExceptionErrorCode.InvalidResponse);
            }

            if (string.IsNullOrEmpty(otherUserID) || otherUserID == SoilServices.UserInfo.uuid)
                CloudSavePlayerPrefs.Save(saveResponse);
            MyDebug.Verbose($"{key} loaded from cloud");
            return saveResponse;
        }
    }
}