using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlyingAcorn.Analytics
{
    [Serializable]
    public abstract class AnalyticsManager : MonoBehaviour
    {
        [SerializeField] private bool initOnAwake;
        protected static AnalyticsManager Instance;

        [SerializeReference] private List<IAnalytics> services = new();
        protected AnalyticServiceProvider AnalyticServiceProvider;
        protected bool InitCalled;
        private bool _started;

        protected void Awake()
        {
            if (Instance != null)
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
                AnalyticServiceProvider?.DesignEvent("FA_session", "first");
            
            AnalyticsPlayerPrefs.SessionCount++;
            AnalyticServiceProvider?.DesignEvent(AnalyticsPlayerPrefs.SessionCount, "FA_session", "start");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!Instance._started)
                return;
            
            if (pauseStatus)
            {
                AnalyticServiceProvider?.DesignEvent("FA_session", "pause");
            }
            else
            {
                AnalyticServiceProvider?.DesignEvent("FA_session", "unpause");
            }
        }

        private void OnDestroy()
        {
            if (!_started)
                return;
            AnalyticServiceProvider?.DesignEvent("FA_session", "end");
        }

        public abstract void SetConsents();
        public abstract void SetUserIdentifier(string playerId);
        
        public static void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            StoreType storeType, string receipt, Dictionary<string, object> customData)
        {
            if (Instance == null || Instance.AnalyticServiceProvider == null)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.BusinessEvent(currency, amount, itemType, itemId, cartType, storeType, receipt, customData);
        }
        
        // ATTENTION: DO NOT USE MYDEBUG HERE
        public static void ErrorEvent(Constants.ErrorSeverity.FlyingAcornErrorSeverity severity, string message)
        {
            if (Instance == null || Instance.AnalyticServiceProvider == null)
            {
                Debug.Log("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.ErrorEvent(severity, message);
        }
        
        public static void UserSegmentation(string name, string value)
        {
            if (Instance == null || Instance.AnalyticServiceProvider == null)
            {
                MyDebug.LogWarning("Analytics not initialized");
                return;
            }
            Instance.AnalyticServiceProvider.UserSegmentation(name, value);
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
    }
}