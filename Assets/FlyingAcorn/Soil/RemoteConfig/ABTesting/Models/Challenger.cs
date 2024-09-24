using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

// ReSharper disable UnassignedField.Global

// ReSharper disable UnusedMember.Global

namespace FlyingAcorn.Soil.RemoteConfig.ABTesting.Models
{
    [UsedImplicitly]
    public class Challenger
    {
        public string ChallengerId;
        public double WeightInExperiment;
        public JToken StringConfig;
        public string KeyToChallenge;
        public string ActivationEvent;
        public string ExperimentId;
        public double ExperimentTargetPercent;
        public double PercentInExperiment;
        public double PercentInWholeUsers;
    }
}