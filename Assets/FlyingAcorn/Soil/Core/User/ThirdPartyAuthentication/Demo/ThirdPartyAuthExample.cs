using System.Linq;
using Cdm.Authentication.Browser;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Demo
{
    public class ThirdPartyAuthExample : UIBehaviour
    {
        public TMP_Text statusText;
        public Button linkGoogleButton;
        public Button unlinkGoogleButton;
        public Button getAllLinksButton;


        private CrossPlatformBrowser _crossPlatformBrowser;

        protected override void Awake()
        {
            base.Awake();
            _ = SocialAuthentication.Initialize();

            statusText.text = "";

            SocialAuthentication.OnLinkSuccessCallback += OnLinkSuccess;
            SocialAuthentication.OnLinkFailureCallback += OnFailure;
            SocialAuthentication.OnUnlinkSuccessCallback += OnUnlinkSuccess;
            SocialAuthentication.OnUnlinkFailureCallback += OnFailure;
            SocialAuthentication.OnGetAllLinksSuccessCallback += OnGetAllLinksSuccess;
            SocialAuthentication.OnGetAllLinksFailureCallback += OnFailure;
            linkGoogleButton.onClick.AddListener(LinkGoogle);
            unlinkGoogleButton.onClick.AddListener(UnlinkGoogle);
            getAllLinksButton.onClick.AddListener(GetLinks);
            UpdateButtons();
        }

        private static void GetLinks()
        {
            SocialAuthentication.GetLinks();
        }

        private static void UnlinkGoogle()
        {
            SocialAuthentication.Unlink(Constants.ThirdParty.google);
        }

        private static void LinkGoogle()
        {
            SocialAuthentication.Link(Constants.ThirdParty.google);
        }

        private void OnGetAllLinksSuccess(LinkGetResponse linkGetResponse)
        {
            statusText.text = $"Get all links success: {JsonConvert.SerializeObject(linkGetResponse)}";
            UpdateButtons();
        }

        private void OnUnlinkSuccess(UnlinkResponse obj)
        {
            statusText.text = $"Unlink success: {JsonConvert.SerializeObject(obj)}";
            UpdateButtons();
        }

        private void OnFailure(SoilException soilException)
        {
            statusText.text = $"Link failure: {soilException.ErrorCode}";
        }

        private void OnLinkSuccess(LinkPostResponse linkPostResponse)
        {
            statusText.text = $"Link success: {JsonConvert.SerializeObject(linkPostResponse)}";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var links = LinkingPlayerPrefs.Links;
            unlinkGoogleButton.interactable = links.Any(l => l.detail.app_party.party == Constants.ThirdParty.google);
            linkGoogleButton.interactable = !unlinkGoogleButton.interactable;
        }
    }
}