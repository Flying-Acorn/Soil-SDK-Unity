using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Push.Data;
using FlyingAcorn.Soil.Push.Logic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Push
{
    /// <summary>
    /// Push notifications from Soil: friend requests, accepted requests, leaderboard prizes and invite rewards.
    /// Soil sends every push; the game only hands over its device token. Needs the app's Push notifications feature.
    /// <para>
    /// With the Firebase Messaging package in the project, the SDK's Firebase bridge does that on its own: no game
    /// code is needed. Without it, call <see cref="SetToken"/> with a token from any FCM client.
    /// </para>
    /// <para>
    /// Nothing here ever blocks or throws to the game: registering waits for Soil to be ready, runs once, only
    /// when the token, the language or the player changed (or once a week), and a failure is tried again on the
    /// next launch, never in a loop.
    /// </para>
    /// </summary>
    public static class Push
    {
        private static string ApiBaseUrl => $"{Core.Data.Constants.ApiUrl}/";
        private static string RecordKey => $"{UserPlayerPrefs.GetKeysPrefix()}push_registration";
        private static string LanguageKey => $"{UserPlayerPrefs.GetKeysPrefix()}push_language";
        private static string EnabledKey => $"{UserPlayerPrefs.GetKeysPrefix()}push_enabled";

        private static bool _started;
        private static bool _inFlight;
        private static bool _again;
        private static string _token;
        private static bool _featureOff;
        private static Action<PushMessage> _opened;
        private static PushMessage _pendingOpened;

        /// <summary>Whether Soil was sending pushes when this device last registered. Informational only.</summary>
        [UsedImplicitly]
        public static bool Enabled => PlayerPrefs.GetInt(EnabledKey, 0) == 1;

        /// <summary>The device token handed over, or null. Kept by the provider; registering uses it.</summary>
        [UsedImplicitly] public static string Token => _token;

        /// <summary>
        /// A push arrived while the game was in the foreground. The phone shows nothing for these; show an in-game
        /// note if you like. Not raised for a push the player tapped (see <see cref="OnOpenedFromNotification"/>).
        /// </summary>
        [UsedImplicitly] public static event Action<PushMessage> OnMessageReceived;

        /// <summary>
        /// The player tapped a push to open the game. Route by <see cref="PushMessage.Kind"/>: friend screens for
        /// the friend kinds, the leaderboard <see cref="PushMessage.Ref"/> for a prize. A tap that launched the game
        /// before anything subscribed is delivered to the first subscriber.
        /// </summary>
        [UsedImplicitly]
        public static event Action<PushMessage> OnOpenedFromNotification
        {
            add
            {
                _opened += value;
                if (_pendingOpened == null) return;
                var pending = _pendingOpened;
                _pendingOpened = null;
                Invoke(value, pending);
            }
            remove => _opened -= value;
        }

        /// <summary>
        /// Whether the SDK's Firebase bridge starts on its own (when Firebase Messaging is in the project). Set it to
        /// false in an Awake of the first scene if the game hands tokens over itself with <see cref="SetToken"/>.
        /// </summary>
        [UsedImplicitly]
        public static bool UseFirebaseBridge
        {
            get => PushHub.AutomaticBridge;
            set => PushHub.AutomaticBridge = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Boot()
        {
            if (_started) return;
            _started = true;
            PushHub.TokenReceived += token => SetToken(token);
            PushHub.MessageReceived += message => Dispatch(message).Forget();
            UserApiHandler.OnUserFilled += changed =>
            {
                // A sign-in that lands on another account: the same device now belongs to that player.
                _featureOff = false;
                if (changed) Schedule();
            };
        }

        /// <summary>
        /// Hands over this device's token. Returns at once; registering happens in the background when Soil is ready.
        /// </summary>
        public static void SetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            _token = token.Trim();
            Schedule();
        }

        /// <summary>
        /// The game's language (like "fa", "en" or "Persian"), which picks the language of each push. Optional: without
        /// it Soil uses the "language" player property the game sends, then the game's default language.
        /// </summary>
        public static void SetLanguage(string language)
        {
            var value = string.IsNullOrWhiteSpace(language) ? "" : language.Trim();
            if (PlayerPrefs.GetString(LanguageKey, "") == value) return;
            PlayerPrefs.SetString(LanguageKey, value);
            Schedule();
        }

        /// <summary>
        /// Stops pushes to this device, say when the player turns notifications off in the game's settings. Hand a
        /// token over again to start them.
        /// </summary>
        public static void ClearToken() => Unregister().Forget();

        /// <summary>
        /// Removes this game's notifications from the notification tray, for example when the friends screen opens.
        /// On Android this removes the game's own local notifications that are showing too.
        /// </summary>
        public static void ClearDelivered()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var manager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
                manager.Call("cancelAll");
#elif UNITY_IOS && !UNITY_EDITOR
                _SoilPushClearDelivered();
#endif
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"Soil-Push: could not clear notifications: {e.Message}");
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _SoilPushClearDelivered();
#endif

        private static void Schedule()
        {
            if (_inFlight)
            {
                _again = true;
                return;
            }
            Register().Forget();
        }

        private static async UniTaskVoid Register()
        {
            _inFlight = true;
            try
            {
                do
                {
                    _again = false;
                    await UniTask.SwitchToMainThread();
                    // Waits as long as it takes; a game that never initializes Soil simply never registers.
                    await UniTask.WaitUntil(() => SoilServices.Ready);
                    await RegisterOnce();
                } while (_again);
            }
            catch (Exception e)
            {
                // Never surfaces to the game: the next launch tries again.
                MyDebug.LogWarning($"Soil-Push: registering the device failed: {e.Message}");
            }
            finally
            {
                _inFlight = false;
            }
        }

        private static async UniTask RegisterOnce()
        {
            var token = _token;
            var user = SoilServices.UserInfo?.uuid;
            var language = PlayerPrefs.GetString(LanguageKey, "");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_featureOff || !PushProtocol.ShouldRegister(LoadRecord(), token, language, user, now)) return;

            using var request = new UnityWebRequest(ApiBaseUrl + PushProtocol.DevicesPath, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    PushProtocol.RegisterJson(token, Application.platform.ToString(), language))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (status, body) = await Send(request, PushOperation.Register);
            var result = PushProtocol.ParseRegister(status, body);
            if (result != null && result.Succeeded)
            {
                SaveRecord(new PushRegistrationRecord { token = token, language = language, user = user, at = now });
                PlayerPrefs.SetInt(EnabledKey, result.push_enabled ? 1 : 0);
                MyDebug.Info($"Soil-Push: device registered (push {(result.push_enabled ? "on" : "off")}).");
                return;
            }
            if (status == 403)
            {
                // The app does not have the Push notifications feature: do not ask again this session.
                _featureOff = true;
                MyDebug.Info("Soil-Push: this game does not have the Push notifications feature.");
                return;
            }
            MyDebug.LogWarning($"Soil-Push: registering answered {status} {result?.detail?.message ?? body}");
        }

        private static async UniTaskVoid Unregister()
        {
            try
            {
                var token = _token ?? LoadRecord()?.token;
                SaveRecord(null);
                if (string.IsNullOrEmpty(token) || !SoilServices.Ready) return;
                using var request = new UnityWebRequest(ApiBaseUrl + PushProtocol.DevicesPath,
                    UnityWebRequest.kHttpVerbDELETE)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(PushProtocol.UnregisterJson(token))),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Content-Type", "application/json");
                await Send(request, PushOperation.Unregister);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"Soil-Push: unregistering the device failed: {e.Message}");
            }
        }

        private static async UniTask<(long status, string body)> Send(UnityWebRequest request, PushOperation operation)
        {
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException e)
            {
                throw new PushException(e.Message, operation, e.ErrorCode);
            }
            return (request.responseCode, request.downloadHandler?.text);
        }

        private static async UniTaskVoid Dispatch(PushMessage message)
        {
            await UniTask.SwitchToMainThread();
            if (message.Opened)
            {
                if (_opened == null) _pendingOpened = message;
                else Invoke(_opened, message);
                return;
            }
            Invoke(OnMessageReceived, message);
        }

        private static void Invoke(Action<PushMessage> handlers, PushMessage message)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<PushMessage>)handler)(message);
                }
                catch (Exception e)
                {
                    MyDebug.LogError($"Soil-Push: a push handler threw: {e}");
                }
            }
        }

        private static PushRegistrationRecord LoadRecord()
        {
            var json = PlayerPrefs.GetString(RecordKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<PushRegistrationRecord>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void SaveRecord(PushRegistrationRecord record)
        {
            if (record == null) PlayerPrefs.DeleteKey(RecordKey);
            else PlayerPrefs.SetString(RecordKey, JsonConvert.SerializeObject(record));
        }
    }
}
