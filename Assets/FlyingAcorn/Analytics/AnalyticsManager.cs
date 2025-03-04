using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core.Data;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Analytics
{
    [Serializable]
    public class AnalyticsManager : MonoBehaviour
    {
        [SerializeField] private bool initOnAwake;
        protected static AnalyticsManager Instance;

        [SerializeReference] private List<IAnalytics> services = new();
        protected AnalyticServiceProvider AnalyticServiceProvider;
        protected internal static bool InitCalled;
        private bool _started;

        protected void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            AnalyticServiceProvider = new AnalyticServiceProvider(services);

            if (initOnAwake)
                Init();
        }

        private void Start()
        {
            if (Instance._started) return;
            Instance._started = true;
            MyDebug.Verbose("AnalyticsManager.Start");
            if (AnalyticsPlayerPrefs.SessionCount <= 0)
            {
                AnalyticsPlayerPrefs.InstallationVersion = Application.version;
                AnalyticsPlayerPrefs.InstallationBuild = DataUtils.GetUserBuildNumber();
                AnalyticServiceProvider?.DesignEvent("FA_session", "first");
            }

            AnalyticsPlayerPrefs.SessionCount++;
            AnalyticServiceProvider?.DesignEvent(AnalyticsPlayerPrefs.SessionCount, "FA_session", "start");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!Instance._started)
                return;
            
            var eventName = pauseStatus ? "pause" : "unpause";
            AnalyticServiceProvider?.DesignEvent("FA_session", eventName);
        }

        private void OnDestroy()
        {
            if (!_started)
                return;
            AnalyticServiceProvider?.DesignEvent("FA_session", "end");
        }

        public virtual void SetConsents()
        {
            Debug.Log("SetConsents not implemented");
        }

        public virtual void SetUserIdentifier(string playerId)
        {
            Debug.Log("SetUserIdentifier not implemented");
        }

        public static void SetAnalyticsConsents()
        {
            if (!Instance || Instance.AnalyticServiceProvider == null)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }

            Instance.SetConsents();
        }

        public static void BusinessEvent(string currency, decimal amount, string itemType, string itemId,
            string cartType,
            StoreType storeType, string receipt, Dictionary<string, object> customData)
        {
            if (!Instance || Instance.AnalyticServiceProvider == null)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }

            Instance.AnalyticServiceProvider.BusinessEvent(currency, amount, itemType, itemId, cartType, storeType,
                receipt, customData);
        }

        // ATTENTION: DO NOT USE MYDEBUG HERE
        public static void ErrorEvent(Constants.ErrorSeverity.FlyingAcornErrorSeverity severity, string message)
        {
            if (!Instance || Instance.AnalyticServiceProvider == null)
            {
                Debug.Log("Analytics not initialized");
                return;
            }

            Instance.AnalyticServiceProvider.ErrorEvent(severity, message);
        }

        public static void UserSegmentation(string name, string value, int dimension=-1)
        {
            if (!Instance || Instance.AnalyticServiceProvider == null)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }

            Instance.AnalyticServiceProvider.UserSegmentation(name, value, dimension);
        }

        public static void Initialize()
        {
            if (!Instance)
            {
                MyDebug.LogWarning("Instance is null");
                return;
            }

            Instance.Init();
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public virtual void Init()
        {
            MyDebug.Verbose("Initializing Analytics");
            if (InitCalled)
            {
                MyDebug.LogWarning("Init already called");
                return;
            }

            InitCalled = true;
            AnalyticServiceProvider.Initialize();
        }

        public static IAnalytics GetRunningService([NotNull] Type type)
        {
            if (!Instance || Instance.AnalyticServiceProvider == null)
            {
                return null;
            }

            return Instance.services.Find(s => s.GetType() == type);
        }

        public IAnalytics GetService([NotNull] Type type)
        {
            return services.Find(s => s.GetType() == type);
        }
    }
}