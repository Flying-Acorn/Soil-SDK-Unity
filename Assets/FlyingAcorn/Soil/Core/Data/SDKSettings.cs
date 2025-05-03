using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    [CreateAssetMenu(fileName = "SDKSettings", menuName = "FlyingAcorn/Soil/Core/SDKSettings")]
    public class SDKSettings : ScriptableObject
    {
        [SerializeField] private string appID;
        [SerializeField] private string sdkToken;
        [SerializeField] private bool deepLinkEnabled;
        [SerializeField] private string paymentDeeplinkRoot;
        [SerializeField] private int requestTimeout = 6;

        public string AppID => appID;
        public string SdkToken => sdkToken;
        public bool DeepLinkEnabled => deepLinkEnabled;
        public string PaymentDeeplinkRoot => paymentDeeplinkRoot;
        public int RequestTimeout => requestTimeout;
    }
}