using Cdm.Authentication.Browser;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;

namespace FlyingAcorn.Soil.Core.User.Demo
{
    public class AuthenticationUI : UIBehaviour
    {
        public TMP_Text statusText;
        public Button authenticateButton;


        private CrossPlatformBrowser _crossPlatformBrowser;

        protected override void Awake()
        {
            base.Awake();

            statusText.text = "";

            authenticateButton.onClick.AddListener(AuthenticateAsync);
        }

        private void AuthenticateAsync()
        {
            ThirdPartyHandler.OnLinkSuccessCallback += OnLinkSuccess;
            ThirdPartyHandler.OnLinkFailureCallback += OnLinkFailure;
            ThirdPartyHandler.Link(Constants.ThirdParty.Google);
        }

        private void OnLinkFailure(string obj)
        {
            statusText.text = $"Link failure: {obj}";
        }

        private void OnLinkSuccess(ThirdPartyHandler.LinkModel obj)
        {
            statusText.text = $"Link success: {JsonConvert.SerializeObject(obj)}";
        }
    }
}