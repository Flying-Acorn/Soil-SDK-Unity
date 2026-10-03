using System.Collections.Generic;

namespace FlyingAcorn.Analytics
{
    public abstract class Constants
    {
        public abstract class ErrorSeverity
        {
            public enum FlyingAcornErrorSeverity
            {
                UndefinedSeverity = 0,
                DebugSeverity = 1,
                InfoSeverity = 2,
                WarningSeverity = 3,
                ErrorSeverity = 4,
                CriticalSeverity = 5
            }
        }

        public abstract class ProgressionStatus
        {
            public enum FlyingAcornProgressionStatus
            {
                //Undefined progression
                UndefinedLevel = 0,

                // User started progression
                StartLevel = 1,

                // User succesfully ended a progression
                CompleteLevel = 2,

                // User failed a progression
                FailLevel = 3
            }

            public enum FlyingAcornNonLevelStatus
            {
                Undefined = 0,
                Start = 1,
                Complete = 2,
                Fail = 3,
            }
        }

        public abstract class ResourceFlowType
        {
            public enum FlyingAcornResourceFlowType
            {
                //Undefined progression
                UndefinedFlow = 0,

                // Source: Used when adding resource to a user
                SourceFlow = 1,

                // Sink: Used when removing a resource from a user
                SinkFlow = 2
            }
        }
        public static readonly Dictionary<Intent, string> IntentActions = new()
        {
            { Intent.ActionView, "ACTION_VIEW" },
            { Intent.ActionEdit, "ACTION_EDIT" }
        };


        public enum Intent
        {
            ActionView,
            ActionEdit,
        }


        public enum PaymentSDK
        {
            Other = 0,
            AppStore = 1,
            GooglePlay = 2,
            Soil = 3,
            // Regional Android billing. Compiled out of iOS players, kept in the editor; see
            // BuildData.Constants.Store for the same pattern. Values are explicit so omitting
            // these never renumbers anything.
#if !UNITY_IOS || UNITY_EDITOR
            Myket = 4,
            CafeBazaar = 5
#endif
        }

        public const string FlyingAcorn = "FlyingAcorn";
    }
}