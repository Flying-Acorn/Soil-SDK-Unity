# Push Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Push notifications service is enabled for your app. Reach out to your Soil
contact to enable it. A superuser uploads the game's Firebase service-account key under **Push → Settings**.

## 1. Firebase

The game needs the Firebase Unity SDK's **Firebase Messaging** package (`com.google.firebase.messaging`, installed as
a UPM package) and its `google-services.json` / `GoogleService-Info.plist`, as any Firebase game does. The SDK's
Firebase bridge (`Push/Firebase/`) compiles only when that package is in the project. A Firebase imported as a
`.unitypackage` is not detected; hand the token over yourself (below) in that case.

Start the bridge once your own Firebase setup reports its dependencies **Available**:

```csharp
using FlyingAcorn.Soil.Push;

FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
{
    if (task.Result == DependencyStatus.Available)
        Push.StartFirebaseBridge();   // Safe to call more than once.
});
```

From then on it hands every token Firebase reports to Soil (which registers it once Soil is ready) and passes
received pushes on to `Push.OnMessageReceived` and `Push.OnOpenedFromNotification`.

The bridge never checks or fixes Firebase's dependencies itself: Firebase allows only one check at a time, and a
second one running beside yours would make your Firebase setup (Analytics, Crashlytics) fail for that session.
That is why the game decides when it starts.

**iOS:** starting the bridge starts Firebase Messaging, which asks the player for notification permission at that
moment. Call `StartFirebaseBridge` when that prompt is welcome. Push also needs an APNs key uploaded to the
Firebase project and the Push Notifications capability on the app.

**Android:**
- Android 13 and later asks the player for permission to show notifications; ask for it as you already do for
  local notifications (Unity Mobile Notifications). Soil does not ask.
- A tap on a push while the game is in the background reaches `Push.OnOpenedFromNotification` only when the main
  activity is Firebase's `com.google.firebase.MessagingUnityPlayerActivity` (generated into
  `Assets/Plugins/Android/` by the Firebase Messaging package; set it as the main activity in your
  `AndroidManifest.xml`, and as Unity Mobile Notifications' custom activity if you use that). With the plain
  `UnityPlayerActivity` the push still shows and opens the game, but the game is not told which push it was.
- Give notifications your own small icon, or Android shows a white square: add
  `<meta-data android:name="com.google.firebase.messaging.default_notification_icon" android:resource="@drawable/<icon>" />`
  inside `<application>`.

### Handing tokens over yourself

A game that handles Firebase Messaging itself, or has no UPM Firebase, passes the token in and never starts the
bridge:

```csharp
private void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
{
    Push.SetToken(token.Token);       // Returns at once, from any thread; registers in the background.
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
Push.ClearToken();   // A "notifications off" setting: Soil forgets this device, and it stays off across launches.
Push.Resume();       // Back on: the device registers again.
```

`Push.OptedOut` says which way the player last chose.

## Behaviour

- Registering never blocks and never throws to the game. It waits until `SoilServices.Ready`, runs once, and only
  when the token, the language or the player changed, or once a week. A failure is tried again on the next launch.
- `Push.Enabled` says whether Soil was sending pushes when the device last registered. Informational only.
- An app without the Push notifications feature answers 403; the SDK stops asking for that session.
