using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;
using Button = UnityEngine.UI.Button;

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

        protected override void Awake()
        {
            base.Awake();

            // Initialize Soil SDK first
            statusText.text = "Initializing Soil SDK...";
            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            if (SoilServices.Ready)
            {
                OnSoilServicesReady();
            }
            else
            {
                SoilServices.InitializeAsync();
            }

            linkGoogleButton.onClick.AddListener(LinkGoogle);
            linkAppleButton.onClick.AddListener(LinkApple);
            unlinkButton.onClick.AddListener(Unlink);
            getAllLinksButton.onClick.AddListener(GetLinks);
            UpdateButtons();
        }

        private void OnSoilServicesReady()
        {
            statusText.text = "Soil SDK ready. Initializing Social Authentication...";
            SocialAuthentication.OnInitializationSuccess += OnSocialAuthenticationInitialized;
            SocialAuthentication.OnInitializationFailed += OnSocialAuthenticationFailed;
            SocialAuthentication.Initialize(mySettings);
        }

        private void OnSocialAuthenticationFailed(SoilException exception)
        {
            statusText.text = "Social Authentication initialization failed: " + exception.Message;
        }

        private void OnSocialAuthenticationInitialized()
        {
            statusText.text = "Social Authentication initialized successfully.";
            UpdateButtons();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SocialAuthentication.OnLinkSuccessCallback += OnLinkSuccess;
            SocialAuthentication.OnLinkFailureCallback += OnFailure;
            SocialAuthentication.OnUnlinkSuccessCallback += OnUnlinkSuccess;
            SocialAuthentication.OnUnlinkFailureCallback += OnFailure;
            SocialAuthentication.OnGetAllLinksSuccessCallback += OnGetAllLinksSuccess;
            SocialAuthentication.OnGetAllLinksFailureCallback += OnFailure;
            SocialAuthentication.OnAccessRevoked += OnAccessRevoked;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Comprehensive event unsubscription for thread safety
            SoilServices.OnServicesReady -= OnSoilServicesReady;
            SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;
            SocialAuthentication.OnInitializationSuccess -= OnSocialAuthenticationInitialized;
            SocialAuthentication.OnInitializationFailed -= OnSocialAuthenticationFailed;
            SocialAuthentication.OnLinkSuccessCallback -= OnLinkSuccess;
            SocialAuthentication.OnLinkFailureCallback -= OnFailure;
            SocialAuthentication.OnUnlinkSuccessCallback -= OnUnlinkSuccess;
            SocialAuthentication.OnUnlinkFailureCallback -= OnFailure;
            SocialAuthentication.OnGetAllLinksSuccessCallback -= OnGetAllLinksSuccess;
            SocialAuthentication.OnGetAllLinksFailureCallback -= OnFailure;
            SocialAuthentication.OnAccessRevoked -= OnAccessRevoked;
            
            // UI event unsubscription
            if (linkGoogleButton != null)
                linkGoogleButton.onClick.RemoveListener(LinkGoogle);
            if (linkAppleButton != null)
                linkAppleButton.onClick.RemoveListener(LinkApple);
            if (unlinkButton != null)
                unlinkButton.onClick.RemoveListener(Unlink);
            if (getAllLinksButton != null)
                getAllLinksButton.onClick.RemoveListener(GetLinks);
        }

        private void OnAccessRevoked(ThirdParty obj)
        {
            statusText.text = $"Access revoked for {obj}";
            UpdateButtons();
        }

        private void Update()
        {
            SocialAuthentication.Update();
        }

        private void GetLinks()
        {
            statusText.text = "Getting all links...";
            SocialAuthentication.GetLinks();
        }

        private void Unlink()
        {
            var link = LinkingPlayerPrefs.Links.FirstOrDefault();
            if (link == null)
            {
                statusText.text = "No link found to unlink";
                return;
            }

            statusText.text = $"Unlinking {link.detail.app_party.party}...";
            SocialAuthentication.Unlink(link.detail.app_party.party);

        }

        private void LinkGoogle()
        {
            statusText.text = "Linking Google account...";
            SocialAuthentication.Link(ThirdParty.google);
        }

        private void LinkApple()
        {
            statusText.text = "Linking Apple account...";
            SocialAuthentication.Link(ThirdParty.apple);
        }

        private void OnGetAllLinksSuccess(LinkGetResponse linkGetResponse)
        {
            statusText.text = $"Get all links success: {JsonConvert.SerializeObject(linkGetResponse)}";
            UpdateButtons();
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            statusText.text = $"SDK initialization failed: {exception.Message}";
        }

        private void OnUnlinkSuccess(UnlinkResponse obj)
        {
            statusText.text = $"Unlink success: {JsonConvert.SerializeObject(obj)}";
            UpdateButtons();
        }

        private void OnFailure(ThirdParty thirdParty, SoilException soilException)
        {
            statusText.text = $"Link failure: {thirdParty} - {soilException}";
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