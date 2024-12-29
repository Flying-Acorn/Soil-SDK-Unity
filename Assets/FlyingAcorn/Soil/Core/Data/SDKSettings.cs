using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    [CreateAssetMenu(fileName = "SDKSettings", menuName = "FlyingAcorn/Soil/Core/SDKSettings")]
    public class SDKSettings : ScriptableObject
    {
        [SerializeField] private string appID;
        [SerializeField] private string sdkToken;
        [SerializeField] private bool deepLinkEnabled;
        [SerializeField] private string paymentDeeplink;

        public string AppID => appID;
        public string SdkToken => sdkToken;
        public bool DeepLinkEnabled => deepLinkEnabled;
        public string PaymentDeeplink => paymentDeeplink;
    }
}