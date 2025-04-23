using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FlyingAcorn.Analytics.Demo
{
    public class DemoInitCall : MonoBehaviour
    {
        public string customUserId = "custom_user_id";
        public string appMetricaKey = "APP_KEY";
        public bool debugMode = true;
        public TextMeshProUGUI log;
        
        private void Awake()
        {
            Application.logMessageReceived += LogCallback;
            AnalyticsManager.SetDebugMode(debugMode);
            AnalyticsManager.SaveUserIdentifier(customUserId);
            AnalyticsManager.Initialize(new List<IAnalytics>
            {
                // new Services.GameAnalyticsEvents(), // Uncomment this line if you want to use GameAnalytics
                // new Services.FirebaseEvents(), // Uncomment this line if you want to use Firebase
                // new Services.AppMetricaEvents(appMetricaKey) // Uncomment this line if you want to use AppMetrica
            });
            AnalyticsManager.ErrorEvent(Constants.ErrorSeverity.FlyingAcornErrorSeverity.InfoSeverity, "This is a test error message");
        }
        
        private void LogCallback(string condition, string stackTrace, LogType type)
        {
            log.text += $"{condition}\n";
        }
    }
}