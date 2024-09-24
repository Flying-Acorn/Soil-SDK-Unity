using System.Collections.Generic;
using static FlyingAcorn.Analytics.Constants.ErrorSeverity;
using static FlyingAcorn.Analytics.Constants.ProgressionStatus;
using static FlyingAcorn.Analytics.Constants.ResourceFlowType;

namespace FlyingAcorn.Analytics
{
    public interface IAnalytics
    {
        int EventLengthLimit { get; }
        int EventStepLengthLimit { get; }
        string EventSeparator { get; }
        void Initialize();
        void ErrorEvent(FlyingAcornErrorSeverity severity, string message);
        void UserSegmentation(string name, string property);
        void ResourceEvent(FlyingAcornResourceFlowType flowType, string currency, float amount, string itemType, string itemId);
        
        void SetUserIdentifier(string userId);
        void SetConsents();

        void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            StoreType storeType, string receipt = null);
        void BusinessEvent(string currency, decimal amount, string itemType, string itemId, string cartType,
            StoreType storeType, string receipt, Dictionary<string, object> customData);

        void DesignEvent(params string[] eventSteps);
        void DesignEvent(Dictionary<string, object> customFields, params string[] eventSteps);
        void DesignEvent(float value, params string[] eventSteps);
        void DesignEvent(float value, Dictionary<string, object> customFields, params string[] eventSteps);

        void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType, string levelNumber);
        void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType, string levelNumber, int score);
        void ProgressionEvent(FlyingAcornProgressionStatus progressionStatus, string levelType, string levelNumber, int score, Dictionary<string, object> customFields);
        void NonLevelProgressionEvent(FlyingAcornNonLevelStatus progressionStatus, string progressionType);
    }
}
