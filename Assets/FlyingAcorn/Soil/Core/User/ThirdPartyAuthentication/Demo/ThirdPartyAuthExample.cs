using System;
using System.Collections.Generic;
using System.Linq;
using Cdm.Authentication.Browser;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Demo
{
    public class ThirdPartyAuthExample : UIBehaviour
    {
        public TMP_Text statusText;
        public Button linkGoogleButton;
        public Button linkAppleButton;
        public Button unlinkButton;
        public Button getAllLinksButton;
        [SerializeField] private List<ThirdPartySettings> mySettings; 


        private CrossPlatformBrowser _crossPlatformBrowser;

        protected override void Awake()
        {
            base.Awake();
            _ = SocialAuthentication.Initialize(mySettings);

            statusText.text = "";

            SocialAuthentication.OnLinkSuccessCallback += OnLinkSuccess;
            SocialAuthentication.OnLinkFailureCallback += OnFailure;
            SocialAuthentication.OnUnlinkSuccessCallback += OnUnlinkSuccess;
            SocialAuthentication.OnUnlinkFailureCallback += OnFailure;
            SocialAuthentication.OnGetAllLinksSuccessCallback += OnGetAllLinksSuccess;
            SocialAuthentication.OnGetAllLinksFailureCallback += OnFailure;
            SocialAuthentication.OnAccessRevoked += OnAccessRevoked;
            linkGoogleButton.onClick.AddListener(LinkGoogle);
            linkAppleButton.onClick.AddListener(LinkApple);
            unlinkButton.onClick.AddListener(Unlink);
            getAllLinksButton.onClick.AddListener(GetLinks);
            UpdateButtons();
        }

        private void OnAccessRevoked(Constants.ThirdParty obj)
        {
            statusText.text = $"Access revoked for {obj}";
            UpdateButtons();
        }

        private void Update()
        {
            SocialAuthentication.Update();
        }

        private static void GetLinks()
        {
            SocialAuthentication.GetLinks();
        }

        private static async void Unlink()
        {
            var link = LinkingPlayerPrefs.Links.FirstOrDefault();
            if (link == null)
            {
                Debug.LogError("No link found to unlink");
                return;
            }

            await SocialAuthentication.Unlink(link.detail.app_party.party);
            
        }

        private static void LinkGoogle()
        {
            SocialAuthentication.Link(Constants.ThirdParty.google);
        }

        private static void LinkApple()
        {
            SocialAuthentication.Link(Constants.ThirdParty.apple);
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
            unlinkButton.interactable = links != null && links.Any();
            linkGoogleButton.interactable = !unlinkButton.interactable;
            linkAppleButton.interactable = !unlinkButton.interactable;
        }
    }
}