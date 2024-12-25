using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.RemoteConfig.ABTesting.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FlyingAcorn.Soil.RemoteConfig.ABTesting
{
    public static class ABTestHandler
    {
        private static ABTestingConfig _aBTestingConfig;
        private static List<string> _seenChallengers = new();
        private static bool _canAcceptChallengers;
        private static bool _abTestingInitialized;

        internal static void InitializeAbTesting(JObject remoteConfigData)
        {
            if (!AnalyticsManager.InitCalled)
            {
                Debug.LogWarning("AnalyticsManager.InitCalled is false. Calling Initialize manually.");
                AnalyticsManager.Initialize();
            }

            try
            {
                // ReSharper disable once PossibleNullReferenceException
                _aBTestingConfig = JsonConvert.DeserializeObject<ABTestingConfig>(remoteConfigData
                    .GetValue(Constants.AbTestingExperimentsParentKey).ToString());
            }
            catch (Exception)
            {
                Debug.LogWarning("FA_ABTesting ====> AbTestingConfig is not valid");
                return;
            }

            _aBTestingConfig.Challengers ??= new List<Challenger>();

            HandleBasicAbTestingTasks();
            if (_aBTestingConfig.Challengers.Any(challenger =>
                    challenger.ActivationEvent == Constants.SessionStartEventName))
                ActivateAbTestingExperiment(Constants.SessionStartEventName);
            AnalyticServiceProvider.OnEventSent -= ActivateAbTestingExperiment;
            AnalyticServiceProvider.OnEventSent += ActivateAbTestingExperiment;
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
            RemoteConfigPlayerPrefs.CachedRemoteConfigData[Soil.RemoteConfig.Constants.UserDefinedParentKey] =
                currentData;
        }

        private static void ActivateAbTestingExperiment(string activationEvent)
        {
            if (!_abTestingInitialized || !_canAcceptChallengers)
            {
                if (!_abTestingInitialized)
                    Debug.Log("FA_ABTesting ====> a/b testing is not yet initialized");
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
            SendExperimentIdToAnalyze(cohortName);
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

        private static void SendExperimentIdToAnalyze(string keyToSend)
        {
            Debug.Log("FA_ABTesting ====> Current experiment changed to " + keyToSend);
            AnalyticsManager.UserSegmentation("ABTestingCohortID", keyToSend, 3);
        }
    }
}