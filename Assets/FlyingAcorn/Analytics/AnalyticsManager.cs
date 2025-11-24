using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics.BuildData;
using JetBrains.Annotations;
using UnityEngine;
using static FlyingAcorn.Analytics.Constants;

namespace FlyingAcorn.Analytics
{
    /// <summary>
    /// Manages analytics services and provides a static interface for tracking events.
    /// </summary>
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

        /// <summary>
        /// Sets consents for analytics services.
        /// </summary>
        public virtual void SetConsents()
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            AnalyticServiceProvider.SetConsents();
        }

        // Call this before Initialization
        public static void SaveUserIdentifier(string playerId)
        {
            AnalyticsPlayerPrefs.CustomUserId = playerId;
        }

        // Call this before Initialization
        public static void SetGDPRConsent(bool consent)
        {
            AnalyticsPlayerPrefs.GDPRConsent = consent;
        }

        /// <summary>
        /// Sets the store for analytics tracking.
        /// </summary>
        /// <param name="store">The store enum value (e.g., GooglePlay, AppStore).</param>
        /// <remarks>
        /// Call this method before AnalyticsManager.Initialize() if build enforcement is disabled.
        /// It is recommended to set the store via Build Settings instead for automatic enforcement.
        /// Do not call multiple times; set once at startup.
        /// </remarks>
        public static void SetStore(Analytics.BuildData.Constants.Store store)
        {
            AnalyticsPlayerPrefs.Store = store;
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

        /// <summary>
        /// Sends a business event to analytics services with custom data.
        /// This method tracks revenue from any payment service. For official stores (Google Play, App Store),
        /// automatic tracking is recommended and manual tracking will be skipped if automatic tracking is enabled:
        /// - GameAnalytics: Skips if automatic purchase tracking is expected
        /// - Firebase: Skips if automatic purchase tracking is configured
        /// - AppMetrica: Skips if RevenueAutoTrackingEnabled = true
        /// Custom payment services (Bazaar, Myket, etc.) are always tracked manually.
        /// </summary>
        /// <param name="currency">The currency of the transaction.</param>
        /// <param name="amount">The amount of the transaction.</param>
        /// <param name="itemType">The type of the item purchased.</param>
        /// <param name="itemId">The ID of the item.</param>
        /// <param name="cartType">The type of cart.</param>
        /// <param name="paymentSDK">The payment sdk handling the transaction (e.g., Bazaar, Myket, GooglePlay, AppStore).</param>
        /// <param name="receipt">The receipt of the transaction.</param>
        /// <param name="customData">Additional custom data.</param>
        public static void BusinessEvent(string currency, decimal amount, string itemType, string itemId,
            string cartType,
            PaymentSDK paymentSDK, string receipt, Dictionary<string, object> customData)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.BusinessEvent(currency, amount, itemType, itemId, cartType, paymentSDK,
                receipt, customData);
        }

        /// <summary>
        /// Sends a design event to analytics services with custom fields.
        /// </summary>
        /// <param name="customFields">Extra data to be sent with the event.</param>
        /// <param name="eventSteps">The array can consist of 1 to 5 steps. For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param>
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
        /// <summary>
        /// Sends an error event to all analytics services.
        /// </summary>
        /// <param name="severity">The severity level of the error.</param>
        /// <param name="message">The error message.</param>
        public static void ErrorEvent(Constants.ErrorSeverity.FlyingAcornErrorSeverity severity, string message)
        {
            if (!IsReady)
                return; // silent return to avoid recursion/log spam during early boot
            Instance.AnalyticServiceProvider.ErrorEvent(severity, message);
        }

        /// <summary>
        /// Sends user segmentation data to analytics services.
        /// </summary>
        /// <param name="name">The segmentation name.</param>
        /// <param name="value">The segmentation value.</param>
        /// <param name="dimension">The dimension (used by GameAnalytics).</param>
        public static void UserSegmentation(string name, string value, int dimension = -1)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.UserSegmentation(name, value, dimension);
        }

        /// <summary>
        /// Initializes the analytics manager with the provided services.
        /// </summary>
        /// <param name="services">The list of analytics services to use.</param>
        public static void Initialize(List<IAnalytics> services)
        {
            if (InitCalled)
            {
                MyDebug.LogWarning("Initialize already called");
                return;
            }

            if (!Instance)
            {
                Instance = FindFirstObjectByType(typeof(AnalyticsManager)) as AnalyticsManager;
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
                AnalyticsPlayerPrefs.InstallationBuild = BuildDataUtils.GetUserBuildNumber();
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

        /// <summary>
        /// Gets the running analytics service of the specified type.
        /// </summary>
        /// <param name="type">The type of the analytics service.</param>
        /// <returns>The analytics service instance, or null if not found.</returns>
        public static IAnalytics GetRunningService([NotNull] Type type)
        {
            return Instance?.AnalyticServiceProvider?.GetServices().Find(s => s.GetType() == type);
        }

        /// <summary>
        /// Gets the analytics service of the specified type.
        /// </summary>
        /// <param name="type">The type of the analytics service.</param>
        /// <returns>The analytics service instance, or null if not found.</returns>
        public IAnalytics GetService([NotNull] Type type)
        {
            return Instance.AnalyticServiceProvider.GetServices().Find(s => s.GetType() == type);
        }

        /// <summary>
        /// Sets the debug mode for analytics.
        /// </summary>
        /// <param name="debugMode">Whether to enable debug mode.</param>
        public static void SetDebugMode(bool debugMode)
        {
            MyDebug.Info($"Debug mode set to {debugMode}");
            AnalyticsPlayerPrefs.UserDebugMode = debugMode;
        }

        /// <summary>
        /// Sends a progression event to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="levelNumber">The number of the level.</param>
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

        /// <summary>
        /// Sends a design event to analytics services.
        /// </summary>
        /// <param name="customFields">Custom fields for the event.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="eventStep">The event step.</param>
        /// <param name="levelNumber">The level number.</param>
        public static void DesignEvent(string customFields, string levelType, string eventStep, string levelNumber)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, levelType, eventStep, levelNumber);
        }

        /// <summary>
        /// Sends a progression event with score to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="levelNumber">The number of the level.</param>
        /// <param name="score">The score achieved.</param>
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


        /// <summary>
        /// Sends a design event to analytics services.
        /// </summary>
        /// <param name="customFields">Custom fields for the event.</param>
        /// <param name="interactionName">The name of the interaction.</param>
        public static void DesignEvent(string customFields, string interactionName)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, interactionName);
        }


        /// <summary>
        /// Sends a design event to analytics services.
        /// </summary>
        /// <param name="customFields">Custom fields for the event.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="dialogName">The name of the dialog.</param>
        public static void DesignEvent(string customFields, string levelType, string dialogName)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(customFields, levelType, dialogName);
        }

        /// <summary>
        /// Sends a design event to analytics services.
        /// </summary>
        /// <param name="event_steps">The array can consist of 1 to 5 steps. For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param>
        public static void DesignEvent(string[] event_steps)
        {
            if (!IsReady)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.DesignEvent(event_steps);
        }

        /// <summary>
        /// Sends a design event to analytics services.
        /// </summary>
        /// <param name="customFields">Custom fields for the event.</param>
        /// <param name="interactionName">The name of the interaction.</param>
        /// <param name="dialogName">The name of the dialog.</param>
        /// <param name="levelNumber">The level number.</param>
        /// <param name="eventStep">The event step.</param>
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

        /// <summary>
        /// Sends a resource event to analytics services.
        /// </summary>
        /// <param name="sourceFlow">The type of resource flow.</param>
        /// <param name="itemType">The type of the item.</param>
        /// <param name="amount">The amount of the resource.</param>
        /// <param name="reason">The reason for the resource change.</param>
        /// <param name="source">The source of the resource.</param>
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

        /// <summary>
        /// Sends a sign up event to analytics services.
        /// </summary>
        /// <param name="method">The sign up method.</param>
        /// <param name="extraFields">Additional fields for the event.</param>
        public static void SignUpEvent(string method, Dictionary<string, object> extraFields = null)
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