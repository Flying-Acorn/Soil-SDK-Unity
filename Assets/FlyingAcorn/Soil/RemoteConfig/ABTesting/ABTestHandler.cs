using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.RemoteConfig.ABTesting.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Random = UnityEngine.Random;

namespace FlyingAcorn.Soil.RemoteConfig.ABTesting
{
    public static class ABTestHandler
    {
        private static ABTestingConfig _aBTestingConfig;
        private static List<string> _seenChallengers = new();
        private static bool _canAcceptChallengers;
        private static bool _abTestingInitialized;
        private static string _pendingCohortIdForAnalytics;

        internal static void InitializeAbTesting(JObject remoteConfigData)
        {
            if (!AnalyticsManager.InitCalled)
            {
                // Proceed with A/B logic regardless; defer analytics segmentation until analytics is ready
                MyDebug.LogWarning("AnalyticsManager not initialized yet. Proceeding with A/B testing; will defer cohort segmentation to analytics until ready.");
            }

            // Parse AB testing config; if missing or invalid, fall back to empty config so we can cleanly set NoCohort
            if (remoteConfigData != null && remoteConfigData.TryGetValue(Constants.AbTestingExperimentsParentKey, out var experimentsToken) && experimentsToken != null)
            {
                try
                {
                    _aBTestingConfig = JsonConvert.DeserializeObject<ABTestingConfig>(experimentsToken.ToString());
                }
                catch (Exception e)
                {
                    MyDebug.LogException(new Exception($"AbTestingConfig is not valid - {experimentsToken}", e));
                    _aBTestingConfig = new ABTestingConfig { Challengers = new List<Challenger>() };
                }
            }
            else
            {
                MyDebug.LogError("ABTesting experiments key not found in remote config; proceeding with empty A/B config.");
                _aBTestingConfig = new ABTestingConfig { Challengers = new List<Challenger>() };
            }

            _aBTestingConfig.Challengers ??= new List<Challenger>();

            HandleBasicAbTestingTasks();
            if (_aBTestingConfig.Challengers.Any(challenger =>
                    challenger.ActivationEvent == Constants.SessionStartEventName))
                ActivateAbTestingExperiment(Constants.SessionStartEventName);
            AnalyticServiceProvider.OnEventSent -= ActivateAbTestingExperiment;
            AnalyticServiceProvider.OnEventSent += ActivateAbTestingExperiment;
            // Subscribe to analytics readiness to flush any pending cohort immediately when analytics becomes ready
            AnalyticsManager.OnInitCalled -= TryFlushPendingCohortSegmentation;
            AnalyticsManager.OnInitCalled += TryFlushPendingCohortSegmentation;
            // If analytics is already ready, try flushing now as well
            TryFlushPendingCohortSegmentation();
        }

        private static void HandleBasicAbTestingTasks()
        {
            _seenChallengers = ABTestingPlayerPrefs.GetSeenChallengers();
            _canAcceptChallengers = false;

            if (_aBTestingConfig.Challengers.Count <= 0)
                HandleCohortChange(Constants.NoCohortName);
            else
            {
                var index = _aBTestingConfig.Challengers.FindIndex(challenger =>
                    challenger.ChallengerId ==
                    ABTestingPlayerPrefs
                        .GetLastExperimentId()); // If you are in an experiment and it is still running, choose it
                if (index >= 0)
                {
                    var currentData = RemoteConfig.UserDefinedConfigs;
                    currentData[_aBTestingConfig.Challengers[index].KeyToChallenge] =
                        _aBTestingConfig.Challengers[index].StringConfig;
                    ManipulateRemoteConfig(currentData);
                    HandleCohortChange(_aBTestingConfig.Challengers[index].ChallengerId);
                }
                else
                {
                    _aBTestingConfig.Challengers =
                        _aBTestingConfig.Challengers.FindAll(challenger =>
                            !_seenChallengers.Contains(challenger.ChallengerId)); // Exclude already seen challengers
                    if (_aBTestingConfig.Challengers.Count <= 0)
                        HandleCohortChange(Constants.NoCohortName);
                    else
                        _canAcceptChallengers = true;
                }
            }

            _abTestingInitialized = true;
        }

        private static void ManipulateRemoteConfig(JObject currentData)
        {
            // Get the full cached data
            var fullCache = RemoteConfigPlayerPrefs.CachedRemoteConfigData;
            // Update the user defined configs
            fullCache[Soil.RemoteConfig.Constants.UserDefinedParentKey] = currentData;
            // Trigger the setter to persist to disk by reassigning the whole object
            RemoteConfigPlayerPrefs.CachedRemoteConfigData = fullCache;
        }

        private static void ActivateAbTestingExperiment(string activationEvent)
        {
            if (!_abTestingInitialized || !_canAcceptChallengers)
            {
                if (!_abTestingInitialized)
                    MyDebug.Info("a/b testing is not yet initialized");
                return;
            }

            var suitableChallengers =
                _aBTestingConfig.Challengers.FindAll(challenger => activationEvent == challenger.ActivationEvent);
            var pickedChallenger = PickAChallenger(suitableChallengers);
            if (pickedChallenger != null)
            {
                var currentData = RemoteConfig.UserDefinedConfigs;
                currentData[pickedChallenger.KeyToChallenge] = pickedChallenger.StringConfig;
                ManipulateRemoteConfig(currentData);
                HandleCohortChange(pickedChallenger.ChallengerId);
                _canAcceptChallengers = false;
            }
            else
            {
                HandleCohortChange(Constants.NoCohortName);
            }

            _seenChallengers.AddRange(suitableChallengers.Select(challenger => challenger.ChallengerId));
            ABTestingPlayerPrefs.SetSeenChallengers(_seenChallengers.Distinct());
        }

        private static void HandleCohortChange(string cohortName)
        {
            ABTestingPlayerPrefs.SetLastExperimentId(cohortName);
            SendExperimentIdToAnalytics(cohortName);
            RecordCohortInSoil(cohortName);

            // Attempt immediate send to analytics if possible; otherwise will be deferred
            TryFlushPendingCohortSegmentation();
        }

        private static void RecordCohortInSoil(string cohortName)
        {
            FlyingAcorn.Soil.Core.User.UserApiHandler.UpdatePlayerInfo()
                .WithInternalProperty(Constants.CohortIdPropertyKey, cohortName)
                .Forget();
        }

        private static Challenger PickAChallenger(List<Challenger> challengers)
        {
            var sumOfChallengersPercent = GetSumOfChallengersPercent(challengers);
            var randomNumber = Random.value;
            if (randomNumber * 100 > sumOfChallengersPercent) // The player remains non-tester
                return null;
            challengers.Shuffle();
            var pickedPercent = randomNumber * sumOfChallengersPercent;
            var percent = 0f;

            foreach (var challenger in challengers)
            {
                percent += (float)challenger.PercentInWholeUsers;
                if (pickedPercent > percent) continue;
                return challenger;
            }

            return null;
        }

        private static float GetSumOfChallengersPercent(List<Challenger> challengers)
        {
            return challengers.Sum(challenger => (float)challenger.PercentInWholeUsers);
        }

        private static void SendExperimentIdToAnalytics(string keyToSend)
        {
            MyDebug.Info("Current experiment changed to " + keyToSend);

            // If analytics isn't initialized yet, defer segmentation and flush when ready
            if (AnalyticsManager.InitCalled)
            {
                AnalyticsManager.UserSegmentation("ABTestingCohortID", keyToSend, 3);
            }
            else
            {
                _pendingCohortIdForAnalytics = keyToSend;
                MyDebug.Info($"Deferring analytics cohort segmentation: {_pendingCohortIdForAnalytics}");
            }
        }

        private static void TryFlushPendingCohortSegmentation()
        {
            if (!string.IsNullOrEmpty(_pendingCohortIdForAnalytics) && AnalyticsManager.InitCalled)
            {
                var cohort = _pendingCohortIdForAnalytics;
                _pendingCohortIdForAnalytics = null; // clear before sending to avoid reentrancy issues
                MyDebug.Info($"Flushing deferred analytics cohort segmentation: {cohort}");
                try
                {
                    AnalyticsManager.UserSegmentation("ABTestingCohortID", cohort, 3);
                }
                catch (Exception ex)
                {
                    // Restore pending value so we can retry later if sending fails unexpectedly
                    _pendingCohortIdForAnalytics = cohort;
                    MyDebug.LogWarning($"Failed to flush deferred analytics cohort segmentation: {ex.Message}");
                }
            }
        }
    }
}