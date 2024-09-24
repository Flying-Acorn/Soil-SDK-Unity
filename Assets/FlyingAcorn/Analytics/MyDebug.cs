using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using FlyingAcornErrorSeverity = FlyingAcorn.Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity;

namespace FlyingAcorn.Analytics
{
    public static class MyDebug
    {
        public static readonly bool GeneralDebugMode = false;

        private static readonly string LogTag = $"[{Constants.FlyingAcorn}]:";
        private const int DebugDepth = 2;

        public static void SetLogLevel(FlyingAcornErrorSeverity logLevel)
        {
            logLevel = GeneralDebugMode ? FlyingAcornErrorSeverity.DebugSeverity : logLevel;
            AnalyticsPlayerPrefs.SavedLogLevel = logLevel;
        }
        
        public static FlyingAcornErrorSeverity GetLogLevel()
        {
            return GeneralDebugMode ? FlyingAcornErrorSeverity.DebugSeverity : AnalyticsPlayerPrefs.SavedLogLevel;
        }

        private static string GetPrefix()
        {
            string callerClassName;
            try
            {
                callerClassName = new StackTrace().GetFrame(DebugDepth).GetMethod().ReflectedType?.Name;
            }
            catch (Exception)
            {
                return "";
            }

            return $"{LogTag}-[{callerClassName}] ";
        }
        
        public static void Log(object message, FlyingAcornErrorSeverity severity)
        {
            switch (severity)
            {
                case FlyingAcornErrorSeverity.DebugSeverity:
                    Verbose(message);
                    break;
                case FlyingAcornErrorSeverity.InfoSeverity:
                    Info(message);
                    break;
                case FlyingAcornErrorSeverity.WarningSeverity:
                    LogWarning(message);
                    break;
                case FlyingAcornErrorSeverity.ErrorSeverity:
                    LogError(message);
                    break;
                case FlyingAcornErrorSeverity.CriticalSeverity:
                    LogException((Exception)message);
                    break;
                case FlyingAcornErrorSeverity.UndefinedSeverity:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        public static void Verbose(object message)
        {
            if (GetLogLevel() > FlyingAcornErrorSeverity.DebugSeverity) return;
            AnalyticsManager.ErrorEvent(FlyingAcornErrorSeverity.DebugSeverity, message.ToString());
            Debug.Log($"{GetPrefix()}{message}");
        }

        public static void Info(object message)
        {
            if (GetLogLevel() > FlyingAcornErrorSeverity.InfoSeverity) return;
            Debug.Log($"{GetPrefix()}{message}");
            AnalyticsManager.ErrorEvent(FlyingAcornErrorSeverity.InfoSeverity, message.ToString());
        }

        public static void LogWarning(object message)
        {
            if (GetLogLevel() > FlyingAcornErrorSeverity.WarningSeverity) return;
            Debug.LogWarning($"{GetPrefix()}{message}");
            AnalyticsManager.ErrorEvent(FlyingAcornErrorSeverity.WarningSeverity, message.ToString());
        }

        public static void LogError(object message)
        {
            if (GetLogLevel() > FlyingAcornErrorSeverity.ErrorSeverity) return;
            Debug.LogError($"{GetPrefix()}{message}");
            AnalyticsManager.ErrorEvent(FlyingAcornErrorSeverity.ErrorSeverity, message.ToString());
        }

        public static void LogException(Exception exception, string extraMessage = "")
        {
            if (GetLogLevel() > FlyingAcornErrorSeverity.CriticalSeverity) return;
            var message = $"{extraMessage} {exception.Message} {exception.StackTrace}";
            AnalyticsManager.ErrorEvent(FlyingAcornErrorSeverity.CriticalSeverity, message);
        }
    }
}