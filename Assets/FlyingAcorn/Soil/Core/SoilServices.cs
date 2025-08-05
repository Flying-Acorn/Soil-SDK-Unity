using System;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public class SoilServices : MonoBehaviour
    {
        private static SoilServices _instance;
        private static bool _readyBroadcasted;
        private static Task _initTask;
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;

        [UsedImplicitly]
        public static bool Ready => _instance && _instance._instanceReady && 
                                    (_initTask?.IsCompletedSuccessfully ?? false);

        [UsedImplicitly]
        public static bool IsNetworkAvailable => Application.internetReachability != NetworkReachability.NotReachable;


        private bool _instanceReady;

        private void StartInstance()
        {
            UserPlayerPrefs.ResetSetInMemoryCache();
            _instance.transform.SetParent(null);
            _initTask = null;
            _readyBroadcasted = false;
            DontDestroyOnLoad(gameObject);
            SetupDeeplink();
            UserApiHandler.OnUserFilled += OnUserChanged;
            _instanceReady = true;
        }

        private static void OnUserChanged(bool userChanged)
        {
            if (!userChanged) return;
            _readyBroadcasted = false;
            _ = Initialize();
        }

        private void OnDestroy()
        {
            _instance = null;
            OnServicesReady = null;
            UserApiHandler.OnUserFilled -= OnUserChanged;
        }

        internal static async Task Initialize()
        {
            if (!_instance)
            {
                _instance = FindObjectOfType<SoilServices>();
                if (!_instance)
                    _instance = new GameObject(nameof(SoilServices)).AddComponent<SoilServices>();
                _instance.StartInstance();
            }

            if (_initTask is { IsCompletedSuccessfully: false, IsCompleted: true })
                _initTask = null;

            switch (Ready)
            {
                case true when !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access):
                    try
                    {
                        _initTask = null;
                        _initTask = Authenticate.RefreshTokenIfNeeded();
                    }
                    catch (Exception e)
                    {
                        MyDebug.LogWarning("Soil-Core: " + $"Failed to refresh token {e.Message} {e.StackTrace}");
                    }

                    break;
                case true:
                    BroadcastReady();
                    return;
            }

            if (UserPlayerPrefs.AppID == Data.Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Data.Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"Soil-Core: AppID or SDKToken are not set. You must create and fill {nameof(SDKSettings)}. Using demo values.");

            // Fast-fail for offline scenarios during authentication
            if (!IsNetworkAvailable)
            {
                throw new SoilException("No network connectivity and no cached authentication data",
                    SoilExceptionErrorCode.Timeout);
            }

            try
            {
                _initTask ??= Authenticate.AuthenticateUser();
                await _initTask;
            }
            catch (Exception e)
            {
                MyDebug.Info("Soil: " + $"Failed to authenticate user " + e.Message + " " +
                                   e.StackTrace);
                throw;
            }

            BroadcastReady();
        }


        private static void BroadcastReady()
        {
            if (_readyBroadcasted) return;
            MyDebug.Info($"Soil-Core: Services are ready - {UserInfo?.uuid}");
            _readyBroadcasted = true;
            OnServicesReady?.Invoke();
        }

        internal void SetupDeeplink()
        {
            if (!UserPlayerPrefs.DeepLinkActivated) return;
            if (GetComponent<DeepLinkHandler>())
                return;
            gameObject.AddComponent<DeepLinkHandler>();
        }
    }
}