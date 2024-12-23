using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    [CreateAssetMenu(fileName = "ThirdPartySetting", menuName = "FlyingAcorn/Soil/Core/Auth/ThirdPartySetting")]
    public class ThirdPartySettings : ScriptableObject
    {
        [SerializeField] private RuntimePlatform platform;
        [SerializeField] private Constants.ThirdParty thirdParty;
        [SerializeField] private string clientId;
        [SerializeField] private string clientSecret;
        [SerializeField] private string scope = "email profile";
        [SerializeField] private string redirectUri;

        public string ClientId => clientId;
        public string Scope => scope;
        public string RedirectUri => redirectUri;
        public string ClientSecret => clientSecret;
        public Constants.ThirdParty ThirdParty => thirdParty;
    }
}