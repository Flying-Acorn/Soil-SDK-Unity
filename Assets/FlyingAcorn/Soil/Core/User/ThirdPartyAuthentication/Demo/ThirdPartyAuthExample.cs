using System.Collections.Generic;
using System.Linq;
using Cdm.Authentication.Browser;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;

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
            _ = ThirdPartyHandler.Initialize();

            statusText.text = "";

            ThirdPartyHandler.OnLinkSuccessCallback += OnLinkSuccess;
            ThirdPartyHandler.OnLinkFailureCallback += OnFailure;
            ThirdPartyHandler.OnUnlinkSuccessCallback += OnUnlinkSuccess;
            ThirdPartyHandler.OnUnlinkFailureCallback += OnFailure;
            ThirdPartyHandler.OnGetAllLinksSuccessCallback += OnGetAllLinksSuccess;
            ThirdPartyHandler.OnGetAllLinksFailureCallback += OnFailure;
            linkGoogleButton.onClick.AddListener(LinkGoogle);
            unlinkGoogleButton.onClick.AddListener(UnlinkGoogle);
            getAllLinksButton.onClick.AddListener(GetAllLinks);
            UpdateButtons();
        }

        private static void GetAllLinks()
        {
            ThirdPartyHandler.GetAllLinks();
        }

        private static void UnlinkGoogle()
        {
            ThirdPartyHandler.Unlink(Constants.ThirdParty.google);
        }

        private static void LinkGoogle()
        {
            ThirdPartyHandler.Link(Constants.ThirdParty.google);
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

        private void OnFailure(string obj)
        {
            statusText.text = $"Link failure: {obj}";
        }

        private void OnLinkSuccess(LinkPostResponse linkPostResponse)
        {
            statusText.text = $"Link success: {JsonConvert.SerializeObject(linkPostResponse)}";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var links = LinkingPlayerPrefs.Links;
            unlinkGoogleButton.interactable = links.Any(l => l.detail.app_party.party == Constants.ThirdParty.google.ToString());
            linkGoogleButton.interactable = !unlinkGoogleButton.interactable;
        }
    }
}