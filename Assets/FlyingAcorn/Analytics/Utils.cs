using System.Collections.Generic;

namespace FlyingAcorn.Analytics
{
    public static class Utils
    {
        public static string GetEventName(this IAnalytics service, params string[] eventSteps)
        {
            var validatedEventSteps = eventSteps.Clone() as string[];
            if (service.EventStepLengthLimit >= 0)
            {
                for (var index = 0; index < eventSteps.Length; index++)
                {
                    var eventStep = eventSteps[index];
                    if (eventStep.Length > service.EventStepLengthLimit)
                    {
                        MyDebug.Info($"Event step is too long for this provider: {eventStep}, slicing it");
                        eventStep = eventStep[..(service.EventStepLengthLimit - 1)];
                    }

                    if (validatedEventSteps != null) validatedEventSteps[index] = eventStep;
                }
            }

            if (validatedEventSteps == null) return null;
            var eventName = string.Join(service.EventSeparator, validatedEventSteps);
            if (service.EventLengthLimit >= 0)
            {
                if (eventName.Length <= service.EventLengthLimit) return eventName;
                MyDebug.Info(
                    $"Event is too long for this provider: {eventName}, slicing it to {eventName = eventName[..(service.EventLengthLimit - 1)]}");
            }

            MyDebug.Verbose($"Sending event: {eventName} to {service.GetType().Name}");
            return eventName;
        }

        public static void Shuffle<T>(this IList<T> list, int? seed = null)
        {
            var rng = seed != null ? new System.Random((int)seed) : new System.Random();
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}