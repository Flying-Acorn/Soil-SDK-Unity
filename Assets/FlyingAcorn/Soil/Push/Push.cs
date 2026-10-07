using System;
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
    /// With the Firebase Messaging package in the project, call <see cref="StartFirebaseBridge"/> once the game's
    /// Firebase setup reports its dependencies Available, and the SDK's Firebase bridge hands tokens over from then
    /// on. Without it, call <see cref="SetToken"/> with a token from any FCM client.
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
        private static string OptedOutKey => $"{UserPlayerPrefs.GetKeysPrefix()}push_opted_out";

        // Tokens arrive on Firebase's thread: everything below that both threads touch is read and written under Lock.
        private static readonly object Lock = new object();
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

        /// <summary>Whether the player turned pushes off on this device (<see cref="ClearToken"/>).</summary>
        [UsedImplicitly]
        public static bool OptedOut => PlayerPrefs.GetInt(OptedOutKey, 0) == 1;

        /// <summary>The device token handed over, or null.</summary>
        [UsedImplicitly]
        public static string Token
        {
            get
            {
                lock (Lock) return _token;
            }
        }

        /// <summary>
        /// A push arrived while the game was in the foreground. The phone shows nothing for these; show an in-game
        /// note if you like. Not raised for a push the player tapped (see <see cref="OnOpenedFromNotification"/>).
        /// </summary>
        [UsedImplicitly] public static event Action<PushMessage> OnMessageReceived;

        /// <summary>
        /// The player tapped a push to open the game. Route by <see cref="PushMessage.Kind"/>: friend screens for
        /// the friend kinds, the leaderboard <see cref="PushMessage.Ref"/> for a prize. A tap that launched the game
        /// before anything subscribed is delivered to the first subscriber. On Android this needs Firebase's
        /// MessagingUnityPlayerActivity as the main activity (see docs/push/Integration.md).
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            // Play mode without domain reload keeps statics between sessions.
            lock (Lock)
            {
                _started = _inFlight = _again = _featureOff = false;
                _token = null;
            }
            _opened = null;
            _pendingOpened = null;
            OnMessageReceived = null;
            PushHub.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Boot()
        {
            lock (Lock)
            {
                if (_started) return;
                _started = true;
            }
            PushHub.TokenReceived += SetToken;
            PushHub.MessageReceived += message => Dispatch(message).Forget();
            UserApiHandler.OnUserFilled += changed =>
            {
                // A sign-in that lands on another account: the same device now belongs to that player.
                lock (Lock) _featureOff = false;
                if (changed) Schedule();
            };
        }

        /// <summary>
        /// Starts the SDK's Firebase bridge (needs the Firebase Messaging package). Call it once your Firebase setup
        /// reported DependencyStatus.Available; the bridge never checks Firebase's dependencies itself, because a second
        /// check running beside yours would break your Firebase setup. On iOS this is when the system asks the
        /// player for notification permission. Safe to call more than once.
        /// </summary>
        public static void StartFirebaseBridge() => PushHub.RequestFirebaseStart();

        /// <summary>
        /// Hands over this device's token. Returns at once, from any thread; registering happens on the main thread
        /// when Soil is ready.
        /// </summary>
        public static void SetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            lock (Lock) _token = token.Trim();
            Schedule();
        }

        /// <summary>
        /// The game's language (like "fa", "en" or "Persian"), which picks the language of each push. Optional: without
        /// it Soil uses the "language" player property the game sends, then the game's default language. Main thread.
        /// </summary>
        public static void SetLanguage(string language)
        {
            var value = string.IsNullOrWhiteSpace(language) ? "" : language.Trim();
            if (PlayerPrefs.GetString(LanguageKey, "") == value) return;
            PlayerPrefs.SetString(LanguageKey, value);
            Schedule();
        }

        /// <summary>
        /// Stops pushes to this device, say when the player turns notifications off in the game's settings. Kept
        /// across launches: the device does not register again, whatever token Firebase reports, until
        /// <see cref="Resume"/>. Main thread.
        /// </summary>
        public static void ClearToken()
        {
            PlayerPrefs.SetInt(OptedOutKey, 1);
            Schedule();
        }

        /// <summary>Starts pushes to this device again after <see cref="ClearToken"/>. Main thread.</summary>
        public static void Resume()
        {
            if (!OptedOut) return;
            PlayerPrefs.DeleteKey(OptedOutKey);
            Schedule();
        }

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

        /// <summary>One pass at a time; a change during a pass runs another pass after it. Any thread.</summary>
        private static void Schedule()
        {
            lock (Lock)
            {
                if (_inFlight)
                {
                    _again = true;
                    return;
                }
                _inFlight = true;
                _again = false;
            }
            Run().Forget();
        }

        private static async UniTaskVoid Run()
        {
            try
            {
                await UniTask.SwitchToMainThread();
                while (true)
                {
                    // Waits as long as it takes; a game that never initializes Soil simply never registers.
                    await UniTask.WaitUntil(() => SoilServices.Ready);
                    try
                    {
                        await Pass();
                    }
                    catch (Exception e)
                    {
                        // Never surfaces to the game: the next launch tries again.
                        MyDebug.LogWarning($"Soil-Push: updating the device's registration failed: {e.Message}");
                    }
                    lock (Lock)
                    {
                        if (!_again)
                        {
                            _inFlight = false;
                            return;
                        }
                        _again = false;
                    }
                }
            }
            catch (Exception e)
            {
                lock (Lock) _inFlight = false;
                MyDebug.LogWarning($"Soil-Push: {e.Message}");
            }
        }

        private static async UniTask Pass()
        {
            string token;
            bool featureOff;
            lock (Lock)
            {
                token = _token;
                featureOff = _featureOff;
            }
            var user = SoilServices.UserInfo?.uuid;
            var language = PlayerPrefs.GetString(LanguageKey, "");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var record = LoadRecord();
            switch (PushProtocol.Decide(record, token, language, user, now, OptedOut, featureOff))
            {
                case PushAction.Register:
                    await RegisterOnce(token, language, user, now);
                    break;
                case PushAction.Unregister:
                    await UnregisterOnce(record.token);
                    break;
            }
        }

        private static async UniTask RegisterOnce(string token, string language, string user, long now)
        {
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
                // A ClearToken that came while this was on its way wins: the next pass unregisters it again.
                SaveRecord(new PushRegistrationRecord { token = token, language = language, user = user, at = now });
                PlayerPrefs.SetInt(EnabledKey, result.push_enabled ? 1 : 0);
                MyDebug.Info($"Soil-Push: device registered (push {(result.push_enabled ? "on" : "off")}).");
                return;
            }
            if (status == 403)
            {
                // The app does not have the Push notifications feature: do not ask again this session.
                lock (Lock) _featureOff = true;
                MyDebug.Info("Soil-Push: this game does not have the Push notifications feature.");
                return;
            }
            MyDebug.LogWarning($"Soil-Push: registering answered {status} {result?.detail?.message ?? body}");
        }

        private static async UniTask UnregisterOnce(string token)
        {
            using var request = new UnityWebRequest(ApiBaseUrl + PushProtocol.DevicesPath, UnityWebRequest.kHttpVerbDELETE)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(PushProtocol.UnregisterJson(token))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (status, body) = await Send(request, PushOperation.Unregister);
            var answer = PushProtocol.ParseStatus(body);
            if (answer != null && answer.Status == PushStatus.Unregistered || status == 403)
            {
                // Forgotten by Soil (or the game has no push at all): nothing left to undo.
                SaveRecord(null);
                MyDebug.Info("Soil-Push: device unregistered.");
                return;
            }
            MyDebug.LogWarning($"Soil-Push: unregistering answered {status} {answer?.message ?? body}");
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
