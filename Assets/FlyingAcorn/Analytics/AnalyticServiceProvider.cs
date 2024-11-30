using System;
using System.Collections.Generic;
using System.Linq;
using static FlyingAcorn.Analytics.Constants.ErrorSeverity;
using static FlyingAcorn.Analytics.Constants.ProgressionStatus;
using static FlyingAcorn.Analytics.Constants.ResourceFlowType;

namespace FlyingAcorn.Analytics
{
    public class AnalyticServiceProvider : IAnalytics
    {
        #region public fields

        private readonly List<IAnalytics> _services;

        #endregion

        #region methods

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

        public void Initialize()
        {
            if (IsInitialized)
                return;
            foreach (var service in _services)
            {
                service.Initialize();
            }

            IsInitialized = true;
        }

        // ATTENTION: DO NOT USE MYDEBUG HERE
        public void ErrorEvent(FlyingAcornErrorSeverity severity, string message)
        {
            if (MyDebug.GetLogLevel() <= FlyingAcornErrorSeverity.DebugSeverity)
                UnityEngine.Debug.Log(
                    $"Sending message to analytics: {message} with severity: {severity} for services");

            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.ErrorEvent(severity, message);
            }
        }

        public void UserSegmentation(string name, string property)
        {
            MyDebug.Verbose(
                $"Sending user segmentation to analytics: {name} with property: {property} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.UserSegmentation(name, property);
            }
        }

        public void ResourceEvent(FlyingAcornResourceFlowType flowType, string currency, float amount, string itemType,
            string itemID)
        {
            MyDebug.Verbose(
                $"Sending resource event to analytics: {flowType} with currency: {currency} with amount: " +
                $"{amount} with itemType: {itemType} with itemID: {itemID} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.ResourceEvent(flowType, currency, amount, itemType, itemID);
            }
        }

        public void SetUserIdentifier(string userId)
        {
            MyDebug.Verbose($"Sending user to analytics: {userId} for services");
            if (string.IsNullOrEmpty(userId))
                return;
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.SetUserIdentifier(userId);
            }
        }

        public void SetConsents()
        {
            MyDebug.Verbose("Sending consents to analytics for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.SetConsents();
            }
        }

        public void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            StoreType storeType, string receipt = null)
        {
            MyDebug.Verbose($" Sending business event to analytics: {currency} with amount: " +
                            $"{amount} with itemType: {itemType} with itemID: {itemId} with cartType: " +
                            $"{cartType} with receipt: {receipt} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.BusinessEvent(currency, amount, itemType, itemId, cartType, storeType, receipt);
            }
        }

        public void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            StoreType storeType, string receipt, Dictionary<string, object> customData)
        {
            MyDebug.Info($"Tracking business event to analytics: {currency} with amount: " +
                         $"{amount} with itemType: {itemType} with itemID: {itemId} with cartType: " +
                         $"{cartType} with receipt: {receipt} with customData: {GetNames(customData)}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.BusinessEvent(currency, amount, itemType, itemId, cartType, storeType, receipt, customData);
            }
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
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.DesignEvent(eventSteps);
            }

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
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.DesignEvent(customFields, eventSteps);
            }
        }

        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
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
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.DesignEvent(value, eventSteps);
            }
        }

        /// <summary>
        /// Track any type of design event that you want to measure i.e. GUI elements or tutorial steps.
        /// </summary>
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
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.DesignEvent(value, customFields, eventSteps);
            }
        }

        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber)
        {
            MyDebug.Verbose(" Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.ProgressionEvent(progressionStatus, levelType, levelNumber);
            }
        }

        public void NonLevelProgressionEvent(FlyingAcornNonLevelStatus progressionStatus, string progressionType)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {progressionType} with {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.NonLevelProgressionEvent(progressionStatus, progressionType);
            }
        }

        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber, int score)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} with score: {score} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.ProgressionEvent(progressionStatus, levelType, levelNumber, score);
            }
        }

        public void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType,
            string levelNumber, int score, Dictionary<string, object> customFields)
        {
            MyDebug.Verbose("Sending progression event to analytics:" +
                            $" {progressionStatus} with levelType: {levelType} with " +
                            $"levelNumber: {levelNumber} with score: {score} with customFields: {GetNames(customFields)} for these services: {GetServiceNames()}");
            foreach (var service in _services.Where(service => service.IsInitialized))
            {
                service.ProgressionEvent(progressionStatus, levelType, levelNumber, score, customFields);
            }
        }

        private static string GetNames(Dictionary<string, object> customFields)
        {
            return string.Join(",", customFields.Select(x => x.Key + ":" + x.Value));
        }


        private object GetServiceNames()
        {
            return string.Join(", ", _services.Select(x => x.GetType().Name));
        }

        #endregion
    }

    public enum StoreType
    {
        GooglePlay,
        AppStore,
        Other
    }
}