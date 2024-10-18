using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    [CreateAssetMenu(fileName = "SDKSettings", menuName = "FlyingAcorn/Soil/Core/SDKSettings")]
    public class SDKSettings : ScriptableObject
    {
        [SerializeField] private string appID;
        [SerializeField] private string sdkToken;

        public string AppID => appID;
        public string SdkToken => sdkToken;
    }
}