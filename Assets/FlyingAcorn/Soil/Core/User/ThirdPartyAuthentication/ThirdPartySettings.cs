using UnityEngine;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    [CreateAssetMenu(fileName = "ThirdPartySetting", menuName = "FlyingAcorn/Soil/Core/Auth/ThirdPartySetting")]
    public class ThirdPartySettings : ScriptableObject
    {
        [SerializeField] private RuntimePlatform platform;
        [SerializeField] private ThirdParty thirdParty;
        [SerializeField] private string clientId;
        [SerializeField] private string clientSecret;
        [SerializeField] private string scope = "email profile";
        [SerializeField] private string redirectUri;

        public RuntimePlatform Platform => platform;
        public string ClientId => clientId;
        public string Scope => scope;
        public string RedirectUri => redirectUri;
        public string ClientSecret => clientSecret;
        public ThirdParty ThirdParty => thirdParty;
    }
}