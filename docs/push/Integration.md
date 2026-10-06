# Push Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Push notifications service is enabled for your app. Reach out to your Soil
contact to enable it. A superuser uploads the game's Firebase service-account key under **Push → Settings**.

## 1. Firebase

The game needs the Firebase Unity SDK's **Firebase Messaging** package (`com.google.firebase.messaging`) and its
`google-services.json` / `GoogleService-Info.plist`, as any Firebase game does. The SDK's Firebase bridge
(`Push/Firebase/`) compiles only when that package is in the project, and then starts on its own after the first
scene loads:

- it waits for Firebase's dependencies to be available (it only checks them; your Firebase setup fixes them);
- it hands every token Firebase reports to Soil, which registers it once Soil is ready;
- it passes received pushes on to `Push.OnMessageReceived` and `Push.OnOpenedFromNotification`.

No code is needed. On iOS, push also needs an APNs key uploaded to the Firebase project and the Push Notifications
capability on the app.

Android 13 and later asks the player for permission to show notifications; ask for it as you already do for local
notifications (Unity Mobile Notifications). Soil does not ask.

### Handing tokens over yourself

A game that already handles Firebase Messaging can turn the bridge off and pass the token in:

```csharp
using FlyingAcorn.Soil.Push;

private void Awake()
{
    Push.UseFirebaseBridge = false;   // In the first scene, before it finishes loading.
}

private void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
{
    Push.SetToken(token.Token);       // Returns at once; registers in the background.
}
```

## 2. Language (optional)

```csharp
Push.SetLanguage(LocalizationManager.CurrentLanguage);   // "fa", "en", "Persian", "English"...
```

Without it, Soil uses the `language` player property, then the game's default language.

## 3. Taps (optional)

```csharp
using FlyingAcorn.Soil.Push;
using FlyingAcorn.Soil.Push.Logic;

private void OnEnable() => Push.OnOpenedFromNotification += OnPushOpened;
private void OnDisable() => Push.OnOpenedFromNotification -= OnPushOpened;

private void OnPushOpened(PushMessage push)
{
    switch (push.Kind)
    {
        case PushKind.FriendRequest:
        case PushKind.FriendAccepted:
            OpenFriends(highlight: push.Ref);        // The other player's public id.
            break;
        case PushKind.LeaderboardPrize:
            OpenLeaderboard(push.Ref);               // The leaderboard identifier.
            break;
        case PushKind.ReferralReward:
            OpenInvites();
            break;
    }
}
```

## 4. Foreground pushes and the tray (optional)

```csharp
Push.OnMessageReceived += push => ShowToast(push.Body);   // The game is open; the phone showed nothing.

private void OnFriendsScreenOpened() => Push.ClearDelivered();
```

On Android, `ClearDelivered()` also removes the game's own local notifications that are showing.

## 5. Stopping pushes on one device (optional)

```csharp
Push.ClearToken();   // For a "notifications off" setting in the game. Hand a token over again to start.
```

## Behaviour

- Registering never blocks and never throws to the game. It waits until `SoilServices.Ready`, runs once, and only
  when the token, the language or the player changed, or once a week. A failure is tried again on the next launch.
- `Push.Enabled` says whether Soil was sending pushes when the device last registered. Informational only.
- An app without the Push notifications feature answers 403; the SDK stops asking for that session.
