# Social Authentication Integration

Before integrating, ensure you have completed the [Installation](../Installation.md) and understand the [Introduction](Introduction.md).

> Important: Google authentication on Android needs a compile SDK of API level 34 or higher (required by `androidx.credentials`) and runs on devices with API level 28 (Android 9) or higher — verify your Android project's minSdkVersion and compile/target SDK.

> Android dependencies: Google sign-in on Android resolves `androidx.credentials:credentials`, `androidx.credentials:credentials-play-services-auth` and `com.google.android.libraries.identity.googleid:googleid` through [External Dependency Manager for Unity](https://github.com/googlesamples/unity-jar-resolver) (EDM4U), so EDM4U must be in your project — without it the Android build fails to compile the sign-in bridge. Keep a single copy of EDM4U: either the UPM package or the `Assets/ExternalDependencyManager` folder, not both. The SDK does not add `androidx.appcompat` or `kotlinx-serialization-json`; if your project uses them, declare them yourself.

**Service Enablement**: Ensure the Social Authentication service is enabled for your account. Reach out to your Soil contact to enable the service for you.

## Complete Account Linking Sequence

Follow this complete flow to implement third-party account linking:

### 1. Setup and Initialization

Initialize Social Authentication and subscribe to events:

**Important**: Always subscribe to events BEFORE calling `Initialize()` to ensure you don't miss any initialization callbacks.

```csharp
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

public class SocialAuthManager : MonoBehaviour
{
    void Awake()
    {
        // Subscribe to events
        SocialAuthentication.OnInitializationSuccess += OnAuthInitialized;
        SocialAuthentication.OnInitializationFailed += OnAuthFailed;
        SocialAuthentication.OnLinkSuccessCallback += OnLinkSuccess;
        SocialAuthentication.OnLinkFailureCallback += OnLinkFailed;
        SocialAuthentication.OnUnlinkSuccessCallback += OnUnlinkSuccess;
        SocialAuthentication.OnUnlinkFailureCallback += OnUnlinkFailed;
        SocialAuthentication.OnGetAllLinksSuccessCallback += OnGetLinksSuccess;
        SocialAuthentication.OnGetAllLinksFailureCallback += OnGetLinksFailed;
        SocialAuthentication.OnAccessRevoked += OnAccessRevoked;

        // Initialize Social Authentication (handles SDK initialization automatically)
        SocialAuthentication.Initialize();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from all events
        SocialAuthentication.OnInitializationSuccess -= OnAuthInitialized;
        SocialAuthentication.OnInitializationFailed -= OnAuthFailed;
        SocialAuthentication.OnLinkSuccessCallback -= OnLinkSuccess;
        SocialAuthentication.OnLinkFailureCallback -= OnLinkFailed;
        SocialAuthentication.OnUnlinkSuccessCallback -= OnUnlinkSuccess;
        SocialAuthentication.OnUnlinkFailureCallback -= OnUnlinkFailed;
        SocialAuthentication.OnGetAllLinksSuccessCallback -= OnGetLinksSuccess;
        SocialAuthentication.OnGetAllLinksFailureCallback -= OnGetLinksFailed;
        SocialAuthentication.OnAccessRevoked -= OnAccessRevoked;
    }
}
```

### 2. Link a Third-Party Account

Trigger the authentication flow for a specific platform:

```csharp
public void LinkGoogleAccount()
{
    SocialAuthentication.Link(ThirdParty.google);
}

public void LinkAppleAccount()
{
    SocialAuthentication.Link(ThirdParty.apple);
}
```

### 3. Handle Link Results

Process the authentication results:

```csharp
private void OnLinkSuccess(LinkPostResponse response)
{
    Debug.Log($"Successfully linked {response.detail.app_party.party}");
    
    // Update UI to show linked status
    UpdateLinkedAccountsUI();
    
    // Store user preferences or update game state
    PlayerPrefs.SetString($"linked_{response.detail.app_party.party}", "true");
}

private void OnLinkFailed(ThirdParty party, SoilException exception)
{
    Debug.LogError($"Failed to link {party}: {exception.Message}");
    
    // Show user-friendly error message
    ShowErrorDialog($"Unable to link {party} account. Please try again.");
    
    // Handle specific error types
    switch (exception.ErrorCode)
    {
        case SoilExceptionErrorCode.ServiceUnavailable:
            ShowErrorDialog("This service is currently unavailable.");
            break;
        case SoilExceptionErrorCode.AuthenticationFailed:
            ShowErrorDialog("Authentication failed. Please check your credentials.");
            break;
    }
}
```

#### When linking switches to an existing account

If the account a player signs in with is already linked to another Soil account (for example, a returning
player on a new device), the SDK switches to that account instead of linking the current one. The current
account is abandoned, and the server moves what the player built on it:

- **Leaderboards**: their scores in each board's current period (a board keeping the best score keeps the
  higher one; one keeping the latest keeps the newer one). Past periods stay where they are.
- **Friends** (apps with the Friend requests feature only): friends, requests and blocks, keeping their dates.
  A block always wins over a friendship. Apps with only the old instant add keep the old behaviour, where friends
  stay on the old account.

Cloud saves, purchases and economy balances are not moved. Nothing moves when the current account has a sign-in
of its own, because it can still be reached.

Reload anything per-player after a switch: `UserApiHandler.OnUserFilled` is raised with `true` when the user
changed.

```csharp
UserApiHandler.OnUserFilled += userChanged =>
{
    if (!userChanged) return;
    ReloadFriends();
    ReloadLeaderboards();
};
```

### 4. Check Linked Accounts

Retrieve and display all linked accounts:

```csharp
public void RefreshLinkedAccounts()
{
    SocialAuthentication.GetLinks();
}

private void OnGetLinksSuccess(LinkGetResponse response)
{
    Debug.Log($"Found {response.linked_accounts.Count} linked accounts");
    
    foreach (var account in response.linked_accounts)
    {
        Debug.Log($"Linked: {account.detail.app_party.party}");
    }
    
    UpdateLinkedAccountsUI();
}

private void OnGetLinksFailed(ThirdParty party, SoilException exception)
{
    Debug.LogError($"Failed to get links: {exception.Message}");
}
```

### 5. Unlink Accounts

Remove linked accounts when needed:

```csharp
public void UnlinkAccount(ThirdParty party)
{
    SocialAuthentication.Unlink(party);
}

private void OnUnlinkSuccess(UnlinkResponse response)
{
    Debug.Log($"Successfully unlinked account");
    
    // Update UI and preferences
    UpdateLinkedAccountsUI();
    PlayerPrefs.DeleteKey($"linked_{response.detail?.app_party?.party}");
}

private void OnUnlinkFailure(ThirdParty party, SoilException exception)
{
    Debug.LogError($"Failed to unlink {party}: {exception.Message}");
    ShowErrorDialog($"Unable to unlink {party} account. Please try again.");
}
```

### 6. Handle Access Revocation

Respond to external account access changes:

```csharp
private void OnAccessRevoked(ThirdParty party)
{
    Debug.Log($"Access revoked for {party}");
    
    // Clean up local state
    UpdateLinkedAccountsUI();
    PlayerPrefs.DeleteKey($"linked_{party}");
    
    // Optionally show notification to user
    ShowNotification($"Your {party} account access has been revoked.");
}
```

### 7. Update Loop

Keep authentication handlers updated:

```csharp
void Update()
{
    // Required for some platforms (Google Play Games, etc.)
    SocialAuthentication.Update();
}
```

## Complete Integration Example

Here's a complete working example based on the `ThirdPartyAuthExample.cs`:

```csharp
using System.Linq;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

public class SocialAuthManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text statusText;
    public Button linkGoogleButton;
    public Button linkAppleButton;
    public Button unlinkButton;
    public Button refreshButton;

    [Header("Settings")]
    [SerializeField] private List<ThirdPartySettings> thirdPartySettings;

    void Awake()
    {
        // Setup UI
        linkGoogleButton.onClick.AddListener(() => LinkAccount(ThirdParty.google));
        linkAppleButton.onClick.AddListener(() => LinkAccount(ThirdParty.apple));
        unlinkButton.onClick.AddListener(UnlinkFirstAccount);
        refreshButton.onClick.AddListener(RefreshLinkedAccounts);

        // Subscribe to events BEFORE initializing
        SocialAuthentication.OnInitializationSuccess += OnAuthInitialized;
        SocialAuthentication.OnLinkSuccessCallback += OnLinkSuccess;
        SocialAuthentication.OnLinkFailureCallback += OnLinkFailed;
        SocialAuthentication.OnUnlinkSuccessCallback += OnUnlinkSuccess;
        SocialAuthentication.OnGetAllLinksSuccessCallback += OnGetLinksSuccess;

        // Initialize with custom settings
        SocialAuthentication.Initialize(thirdPartySettings);
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        SocialAuthentication.OnInitializationSuccess -= OnAuthInitialized;
        SocialAuthentication.OnLinkSuccessCallback -= OnLinkSuccess;
        SocialAuthentication.OnLinkFailureCallback -= OnLinkFailed;
        SocialAuthentication.OnUnlinkSuccessCallback -= OnUnlinkSuccess;
        SocialAuthentication.OnGetAllLinksSuccessCallback -= OnGetLinksSuccess;
    }

    void Update()
    {
        SocialAuthentication.Update();
    }

    #region Authentication Methods

    private void LinkAccount(ThirdParty party)
    {
        statusText.text = $"Linking {party} account...";
        SocialAuthentication.Link(party);
    }

    private void UnlinkFirstAccount()
    {
        var linkedAccounts = SocialAuthentication.LinkedInfo;
        if (linkedAccounts.Any())
        {
            var account = linkedAccounts.First();
            statusText.text = $"Unlinking {account.detail.app_party.party}...";
            SocialAuthentication.Unlink(account.detail.app_party.party);
        }
        else
        {
            statusText.text = "No accounts to unlink";
        }
    }

    private void RefreshLinkedAccounts()
    {
        statusText.text = "Refreshing linked accounts...";
        SocialAuthentication.GetLinks();
    }

    #endregion

    #region Event Handlers

    private void OnAuthInitialized()
    {
        statusText.text = "Social Authentication ready!";
        UpdateButtons();
        RefreshLinkedAccounts();
    }

    private void OnLinkSuccess(LinkPostResponse response)
    {
        statusText.text = $"✓ Linked {response.detail.app_party.party} account";
        UpdateButtons();
    }

    private void OnLinkFailed(ThirdParty party, SoilException exception)
    {
        statusText.text = $"✗ Failed to link {party}: {exception.Message}";
    }

    private void OnUnlinkSuccess(UnlinkResponse response)
    {
        statusText.text = "✓ Account unlinked successfully";
        UpdateButtons();
    }

    private void OnGetLinksSuccess(LinkGetResponse response)
    {
        statusText.text = $"Found {response.linked_accounts.Count} linked accounts";
        UpdateButtons();
    }

    #endregion

    private void UpdateButtons()
    {
        var hasLinkedAccounts = SocialAuthentication.LinkedInfo.Any();
        
        linkGoogleButton.interactable = !hasLinkedAccounts;
        linkAppleButton.interactable = !hasLinkedAccounts;
        unlinkButton.interactable = hasLinkedAccounts;
    }
}
```

