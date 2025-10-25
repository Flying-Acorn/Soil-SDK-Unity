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
        protected static AnalyticsManager Instance;

        protected AnalyticServiceProvider AnalyticServiceProvider;
        protected internal static bool InitCalled;
        private static bool _started;
        private static bool IsReady => Instance != null && Instance.AnalyticServiceProvider != null;
        public static event Action OnInitCalled;


        protected virtual void Awake()
        {
            if (!Instance) return;
            Destroy(this);
        }

        protected virtual void Start()
        {
            _started = true;
        }

        internal void OnApplicationPause(bool pauseStatus)
        {
            if (!_started)
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
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            AnalyticServiceProvider.SetConsents();
        }

        public static void SaveUserIdentifier(string playerId)
        {

            AnalyticsPlayerPrefs.CustomUserId = playerId;
        }

        protected static void SetAnalyticsConsents()
        {
            if (Instance?.AnalyticServiceProvider == null)
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
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.BusinessEvent(currency, amount, itemType, itemId, cartType, storeType,
                receipt, customData);
        }

        public static void DesignEvent(Dictionary<string, object> customFields, params string[] eventSteps)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, eventSteps);
        }

        // ATTENTION: DO NOT USE MYDEBUG HERE
        public static void ErrorEvent(Constants.ErrorSeverity.FlyingAcornErrorSeverity severity, string message)
        {
            if (!IsReady)
                return; // silent return to avoid recursion/log spam during early boot
            Instance.AnalyticServiceProvider.ErrorEvent(severity, message);
        }

        public static void UserSegmentation(string name, string value, int dimension = -1)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.UserSegmentation(name, value, dimension);
        }

        public static void Initialize(List<IAnalytics> services)
        {
            if (InitCalled)
            {
                MyDebug.LogWarning("Initialize already called");
                return;
            }

            if (!Instance)
            {
                Instance = FindObjectOfType<AnalyticsManager>();
                if (!Instance)
                {
                    var go = new GameObject("AnalyticsManager");
                    Instance = go.AddComponent<AnalyticsManager>();
                }

                DontDestroyOnLoad(Instance);
            }

            Instance.AnalyticServiceProvider = new AnalyticServiceProvider(services);
            if (AnalyticsPlayerPrefs.SessionCount <= 0)
            {
                AnalyticsPlayerPrefs.InstallationVersion = Application.version;
                AnalyticsPlayerPrefs.InstallationBuild = DataUtils.GetUserBuildNumber();
                Instance.AnalyticServiceProvider?.DesignEvent("FA_session", "first");
            }

            AnalyticsPlayerPrefs.SessionCount++;
            Instance.AnalyticServiceProvider?.DesignEvent(AnalyticsPlayerPrefs.SessionCount, "FA_session", "start");

            Instance.Init();
        }

        // ReSharper disable once MemberCanBePrivate.Global
        protected virtual void Init()
        {
            if (InitCalled)
            {
                MyDebug.LogWarning("Init already called");
                return;
            }

            InitCalled = true;
            AnalyticServiceProvider.Initialize();
            try
            {
                OnInitCalled?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] OnInitCalled listeners threw: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static IAnalytics GetRunningService([NotNull] Type type)
        {
            return Instance?.AnalyticServiceProvider?.GetServices().Find(s => s.GetType() == type);
        }

        public IAnalytics GetService([NotNull] Type type)
        {
            return Instance.AnalyticServiceProvider.GetServices().Find(s => s.GetType() == type);
        }

        public static void SetDebugMode(bool debugMode)
        {
            MyDebug.Info($"Debug mode set to {debugMode}");
            AnalyticsPlayerPrefs.UserDebugMode = debugMode;
        }

        public static void ProgressionEvent(Constants.ProgressionStatus.FlyingAcornProgressionStatus progressionStatus,
            string levelType, string levelNumber)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.ProgressionEvent(progressionStatus, levelType, levelNumber);
        }

        public static void DesignEvent(string customFields, string levelType, string eventStep, string levelNumber)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, levelType, eventStep, levelNumber);
        }

        public static void ProgressionEvent(Constants.ProgressionStatus.FlyingAcornProgressionStatus progressionStatus,
            string levelType, string levelNumber, int score)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.ProgressionEvent(progressionStatus, levelType, levelNumber, score);
        }


        public static void DesignEvent(string customFields, string interactionName)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, interactionName);
        }


        public static void DesignEvent(string customFields, string levelType, string dialogName)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, levelType, dialogName);
        }

        public static void DesignEvent(string[] event_steps)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(event_steps);
        }

        public static void DesignEvent(float customFields, string interactionName, string dialogName,
            string levelNumber, string eventStep)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, interactionName, dialogName, levelNumber,
                eventStep);
        }

        public static void ResourceEvent(Constants.ResourceFlowType.FlyingAcornResourceFlowType sourceFlow,
            string itemType, float amount, string reason, string source)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.ResourceEvent(sourceFlow, itemType, amount, reason, source);
        }

        internal static void SignUpEvent(string method, Dictionary<string, object> extraFields = null)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.SignUpEvent(method, extraFields);
        }
    }
}