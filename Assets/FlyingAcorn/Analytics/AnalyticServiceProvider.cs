using System;
using System.Collections.Generic;
using System.Linq;
using static FlyingAcorn.Analytics.Constants;
using static FlyingAcorn.Analytics.Constants.ErrorSeverity;
using static FlyingAcorn.Analytics.Constants.ProgressionStatus;
using static FlyingAcorn.Analytics.Constants.ResourceFlowType;

namespace FlyingAcorn.Analytics
{
    /// <summary>
    /// Provides analytics services by delegating to multiple IAnalytics implementations.
    /// </summary>
    public class AnalyticServiceProvider : IAnalytics
    {
        #region public fields

        private readonly List<IAnalytics> _services;

        #endregion

        #region methods

        /// <summary>
        /// Initializes a new instance of the AnalyticServiceProvider class with the provided services.
        /// </summary>
        /// <param name="services">The list of analytics services to use.</param>
        public AnalyticServiceProvider(List<IAnalytics> services)
        {
            if (services is null || services.Count <= 0)
            {
                MyDebug.LogError("Not enough analytics implemented. Please inherit IAnalytics and implement your own");
                services = new List<IAnalytics>();
            }

            _services = services;
        }

        public int EventLengthLimit => -1;
        public int EventStepLengthLimit => -1;
        public bool IsInitialized { get; set; }
        public string EventSeparator => "_";
        internal static Action<string> OnEventSent { get; set; }

        /// <summary>
        /// Initializes all analytics services.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;
            foreach (var service in _services)
            {
                try
                {
                    service.Initialize();
                }
                catch (Exception ex)
                {
                    // Use direct Unity logging to avoid recursive analytics/error loops
                    UnityEngine.Debug.LogWarning($"[Analytics] Service {service.GetType().Name} failed to Initialize. Continuing. Exception: {ex.Message}\n{ex.StackTrace}");
                }
            }

            IsInitialized = true;
        }

        /// <summary>
        /// Gets the list of analytics services.
        /// </summary>
        /// <returns>The list of IAnalytics services.</returns>
        public List<IAnalytics> GetServices()
        {
            return _services;
        }

        /// <summary>
        /// Sends an error event to all analytics services.
        /// </summary>
        /// <param name="severity">The severity level of the error.</param>
        /// <param name="message">The error message.</param>
        public void ErrorEvent(FlyingAcornErrorSeverity severity, string message)
        {
            if (MyDebug.GetLogLevel() <= FlyingAcornErrorSeverity.DebugSeverity)
                UnityEngine.Debug.Log(
                    $"Sending message to analytics: {message} with severity: {severity} for services");

            ForEachServiceSafely("ErrorEvent", s => s.ErrorEvent(severity, message));
        }

        /// <summary>
        /// Sends user segmentation data to analytics services.
        /// </summary>
        /// <param name="name">The segmentation name.</param>
        /// <param name="property">The segmentation property.</param>
        /// <param name="dimension">The dimension (used by GameAnalytics).</param>
        public void UserSegmentation(string name, string property, int dimension)
        {
            MyDebug.Verbose(
                $"Sending user segmentation to analytics: {name} with property: {property} for these services: {GetServiceNames()}");
            ForEachServiceSafely("UserSegmentation", s => s.UserSegmentation(name, property));
        }

        /// <summary>
        /// Sends a resource event to analytics services.
        /// </summary>
        /// <param name="flowType">The type of resource flow.</param>
        /// <param name="currency">The currency involved.</param>
        /// <param name="amount">The amount of the resource.</param>
        /// <param name="itemType">The type of the item.</param>
        /// <param name="itemID">The ID of the item.</param>
        public void ResourceEvent(FlyingAcornResourceFlowType flowType, string currency, float amount, string itemType,
            string itemID)
        {
            MyDebug.Verbose(
                $"Sending resource event to analytics: {flowType} with currency: {currency} with amount: " +
                $"{amount} with itemType: {itemType} with itemID: {itemID} for these services: {GetServiceNames()}");
            ForEachServiceSafely("ResourceEvent", s => s.ResourceEvent(flowType, currency, amount, itemType, itemID));
        }

        /// <summary>
        /// Sets the user identifier for analytics services.
        /// </summary>
        public void SetUserIdentifier()
        {
            // Call on each adapter manually
        }

        /// <summary>
        /// Sets consents for analytics services.
        /// </summary>
        public void SetConsents()
        {
            MyDebug.Verbose("Sending consents to analytics for these services: {GetServiceNames()}");
            ForEachServiceSafely("SetConsents", s => s.SetConsents());
        }

        /// <summary>
        /// Sends a business event to analytics services.
        /// </summary>
        /// <param name="currency">The currency of the transaction.</param>
        /// <param name="amount">The amount of the transaction.</param>
        /// <param name="itemType">The type of the item purchased.</param>
        /// <param name="itemId">The ID of the item.</param>
        /// <param name="cartType">The type of cart.</param>
        /// <param name="paymentSDK">The payment sdk handling the transaction (e.g., Bazaar, Myket, GooglePlay, AppStore).</param>
        /// <param name="receipt">The receipt of the transaction.</param>
        public void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            PaymentSDK paymentSDK, string receipt = null)
        {
            MyDebug.Verbose($" Sending business event to analytics: {currency} with amount: " +
                            $"{amount} with itemType: {itemType} with itemID: {itemId} with cartType: " +
                            $"{cartType} with receipt: {receipt} for these services: {GetServiceNames()}. PaymentSDK: {paymentSDK}");
            ForEachServiceSafely("BusinessEvent", s => s.BusinessEvent(currency, amount, itemType, itemId, cartType, paymentSDK, receipt));
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
        public void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            PaymentSDK paymentSDK, string receipt, Dictionary<string, object> customData)
        {
            MyDebug.Info($"Tracking business event to analytics: {currency} with amount: " +
                         $"{amount} with itemType: {itemType} with itemID: {itemId} with cartType: " +
                         $"{cartType} with receipt: {receipt} with customData: {GetNames(customData)}. PaymentSDK: {paymentSDK}");
            ForEachServiceSafely("BusinessEvent(customData)", s => s.BusinessEvent(currency, amount, itemType, itemId, cartType, paymentSDK, receipt, customData));
        }

        /// <summary>
        /// Sends a sign up event to analytics services.
        /// </summary>
        /// <param name="method">The sign up method.</param>
        /// <param name="extraFields">Additional fields for the event.</param>
        public void SignUpEvent(string method, Dictionary<string, object> extraFields = null)
        {
            MyDebug.Verbose($" Sending sign up event to analytics with extraFields: {GetNames(extraFields)} for these services: {GetServiceNames()}");
            ForEachServiceSafely("SignUpEvent", s => s.SignUpEvent(method, extraFields));
        }


        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
        /// <param name="eventSteps">The array can consist of 1 to 5 steps.
        /// For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param> 
        public void DesignEvent(params string[] eventSteps)
        {
            if (eventSteps.Length == 0)
            {
                MyDebug.LogWarning("Design event called with no steps");
                return;
            }

            MyDebug.Verbose(" Sending design event to analytics:" +
                            $" {eventSteps[0]} for these services: {GetServiceNames()}");
            ForEachServiceSafely("DesignEvent", s => s.DesignEvent(eventSteps));
            OnEventSent?.Invoke(string.Join(EventSeparator, eventSteps));
        }

        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
        /// <param name="customFields"> Extra data to be sent with the event.
        /// </param>
        /// <param name="eventSteps">The array can consist of 1 to 5 steps.
        /// For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param> 
        public void DesignEvent(Dictionary<string, object> customFields, params string[] eventSteps)
        {
            if (eventSteps.Length == 0)
            {
                MyDebug.LogWarning("Design event called with no steps");
                return;
            }

            MyDebug.Verbose(" Sending design event to analytics:" +
                            $" {eventSteps[0]} with customFields: {GetNames(customFields)} for these services: {GetServiceNames()}");
            ForEachServiceSafely("DesignEvent(customFields)", s => s.DesignEvent(customFields, eventSteps));
        }

        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
        /// <param name="value">The value to be tracked with the event.</param>
        /// <param name="eventSteps">The array can consist of 1 to 5 steps.
        /// For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param>
        public void DesignEvent(float value, params string[] eventSteps)
        {
            if (eventSteps.Length == 0)
            {
                MyDebug.LogWarning("Design event called with no steps");
                return;
            }

            MyDebug.Verbose(" Sending design event to analytics:" +
                            $" {eventSteps[0]} with value: {value} for these services: {GetServiceNames()}");
            ForEachServiceSafely("DesignEvent(value)", s => s.DesignEvent(value, eventSteps));
        }

        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
        /// <param name="value">The value to be tracked with the event.</param>
        /// <param name="customFields">Extra data to be sent with the event.</param>
        /// <param name="eventSteps">The array can consist of 1 to 5 steps.
        /// For example, "level", "start", "1" will be translated to "level:start:1" or "level_start_1" for some services.</param> 
        public void DesignEvent(float value, Dictionary<string, object> customFields, params string[] eventSteps)
        {
            if (eventSteps.Length == 0)
            {
                MyDebug.LogWarning("Design event called with no steps");
                return;
            }

            MyDebug.Verbose(" Sending design event to analytics:" +
                            $" {eventSteps[0]} with value: {value} with customFields: {GetNames(customFields)} for these services: {GetServiceNames()}");
            ForEachServiceSafely("DesignEvent(value,customFields)", s => s.DesignEvent(value, customFields, eventSteps));
        }

        /// <summary>
        /// Sends a progression event to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="levelNumber">The number of the level.</param>
        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber)
        {
            MyDebug.Verbose(" Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} for these services: {GetServiceNames()}");
            ForEachServiceSafely("ProgressionEvent", s => s.ProgressionEvent(progressionStatus, levelType, levelNumber));
        }

        /// <summary>
        /// Sends a non-level progression event to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="progressionType">The type of progression.</param>
        public void NonLevelProgressionEvent(FlyingAcornNonLevelStatus progressionStatus, string progressionType)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {progressionType} with {GetServiceNames()}");
            ForEachServiceSafely("NonLevelProgressionEvent", s => s.NonLevelProgressionEvent(progressionStatus, progressionType));
        }

        /// <summary>
        /// Sends a progression event with score to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="levelNumber">The number of the level.</param>
        /// <param name="score">The score achieved.</param>
        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber, int score)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} with score: {score} for these services: {GetServiceNames()}");
            ForEachServiceSafely("ProgressionEvent(score)", s => s.ProgressionEvent(progressionStatus, levelType, levelNumber, score));
        }

        /// <summary>
        /// Sends a progression event with score and custom fields to analytics services.
        /// </summary>
        /// <param name="progressionStatus">The status of the progression.</param>
        /// <param name="levelType">The type of the level.</param>
        /// <param name="levelNumber">The number of the level.</param>
        /// <param name="score">The score achieved.</param>
        /// <param name="customFields">Additional custom fields.</param>
        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber, int score, Dictionary<string, object> customFields)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} with score: {score} with customFields: {GetNames(customFields)} for these services: {GetServiceNames()}");
            ForEachServiceSafely("ProgressionEvent(score,customFields)", s => s.ProgressionEvent(progressionStatus, levelType, levelNumber, score, customFields));
        }

        /// <summary>
        /// Gets a string representation of the custom fields dictionary.
        /// </summary>
        /// <param name="customFields">The dictionary of custom fields.</param>
        /// <returns>A string representation of the custom fields.</returns>
        private static string GetNames(Dictionary<string, object> customFields)
        {
            if (customFields == null || customFields.Count == 0)
                return "none";
            return string.Join(",", customFields.Select(x => x.Key + ":" + x.Value));
        }


        /// <summary>
        /// Gets a string representation of the service names.
        /// </summary>
        /// <returns>A comma-separated string of service names.</returns>
        private object GetServiceNames()
        {
            return string.Join(", ", _services.Select(x => x.GetType().Name));
        }

        /// <summary>
        /// Executes an action for each initialized service in a safe manner so that one failing service
        /// (e.g. due to missing Google Play Services on the device) does not prevent other analytics
        /// providers from receiving the event.
        /// </summary>
        /// <param name="operation">Name of the analytic operation for logging.</param>
        /// <param name="action">Action to perform per service.</param>
        private void ForEachServiceSafely(string operation, Action<IAnalytics> action)
        {
            foreach (var service in _services.Where(s => s.IsInitialized))
            {
                try
                {
                    action(service);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[Analytics] Service {service.GetType().Name} threw during {operation}. Continuing. Exception: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        #endregion
    }
}