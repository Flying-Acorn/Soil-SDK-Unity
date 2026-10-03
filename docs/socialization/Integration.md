# Socialization Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Socialization service is enabled for your account. Reach out to your Soil contact to enable the service for you.

## Full Socialization Sequence

Follow this complete flow for implementing friend systems:

### 1. Setup and Initialization

Subscribe to events and ensure SDK is ready:

```csharp
using FlyingAcorn.Soil.Socialization;

// Initialize SDK if needed
if (!SoilServices.Ready)
{
    SoilServices.OnServicesReady += OnSDKReady;
    SoilServices.InitializeAsync();
}
else
{
    OnSDKReady();
}

private void OnSDKReady()
{
    Debug.Log("SDK ready - can now use socialization features");
}
```

> **Steps 2-4 are the old instant-add calls**, now marked `[Obsolete]`: adding makes two players friends without
> asking. They keep working for builds already shipped. New games use [Friend requests](#friend-requests-send-accept-and-block).

### 2. Get Friends List (old)

Fetch the current user's friends:

```csharp
private async void LoadFriends()
{
    try
    {
        var friendsResponse = await Socialization.GetFriends();
        
        Debug.Log($"Friends status: {friendsResponse.detail.message}");
        foreach (var friend in friendsResponse.friends)
        {
            Debug.Log($"Friend: {friend.name} (UUID: {friend.uuid})");
            // Add to UI
            AddFriendToUI(friend);
        }
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to load friends: {e.Message}");
    }
}
```

### 3. Add a Friend (old)

Add a friend using their UUID:

```csharp
private async void AddFriend(string friendUuid)
{
    try
    {
        var response = await Socialization.AddFriendWithUUID(friendUuid);
        Debug.Log($"Friend added: {response.detail.message}");
        
        // Refresh friends list
        LoadFriends();
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to add friend: {e.Message}");
    }
}
```

**Note**: See [Finding Other Players](../socialization/Introduction.md#finding-other-players) in the Introduction for ways to find other players: the player code for friend requests, or UUIDs from leaderboards and shared game info.

### 4. Remove a Friend (old)

Remove a friend using their UUID:

```csharp
private async void RemoveFriend(string friendUuid)
{
    try
    {
        var response = await Socialization.RemoveFriendWithUUID(friendUuid);
        Debug.Log($"Friend removed: {response.detail.message}");
        
        // Refresh friends list
        LoadFriends();
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to remove friend: {e.Message}");
    }
}
```

### 5. Get Friends Leaderboard

Fetch leaderboard scores for friends:

```csharp
private async void LoadFriendsLeaderboard(string leaderboardId)
{
    try
    {
        var response = await Socialization.GetFriendsLeaderboard(leaderboardId, count: 20, relative: true);
        
        foreach (var score in response.user_scores)
        {
            Debug.Log($"Friend {score.user_name}: {score.score} (Rank: {score.rank})");
            // Add to leaderboard UI
            AddScoreToLeaderboardUI(score);
        }
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to load friends leaderboard: {e.Message}");
    }
}
```

**Note**: The `relative` parameter determines if ranks are relative to the current user or absolute.

## Friend requests: send, accept and block

Friend requests ask before two players become friends, and let a player block another. They need the
**Friend requests** feature on your app (ask your Soil contact). The old instant-add calls above keep working
beside them on the same friendships, so older builds stay friends with newer ones. Once your older builds are
gone, ask us to turn the old instant add off: until then a modified client could still add friends without
asking.

A refusal is an answer, not an exception. Every action returns a `FriendActionResult`; check `Status`
(`FriendStatus`) and `Succeeded`. Only a transport failure, an expired sign-in or the feature being off throws a
`SocializationException`. Every action is safe to retry after a timeout.

```csharp
using FlyingAcorn.Soil.Socialization;
using FlyingAcorn.Soil.Socialization.Logic;

// The code players share and type: SoilServices.UserInfo.public_id (8 characters, e.g. "K7M29QX4").
var sent = await Socialization.SendFriendRequestByCode(typedCode);
switch (sent.Status)
{
    case FriendStatus.RequestSent: ShowSent(sent.user.name); break;
    case FriendStatus.FriendshipCreated: ShowNowFriends(sent.user.name); break; // they had asked first
    case FriendStatus.FriendNotFound: ShowNoSuchPlayer(); break;
    case FriendStatus.RequestLimitReached:
    case FriendStatus.FriendLimitReached: ShowLimit(); break;
    case FriendStatus.Throttled: RetryIn(sent.RetryAfterSeconds); break;
}

// Badges and lists: one list per call, plus every list's count.
var incoming = await Socialization.GetFriendList(FriendListKind.Incoming);
SetBadge(incoming.counts.incoming);
foreach (var player in incoming.users)
    AddRequestRow(player.name, player.public_id, accept: () => Socialization.AcceptFriendRequest(player.uuid),
        decline: () => Socialization.DeclineFriendRequest(player.uuid));
```

| Call | Does |
|---|---|
| `GetFriendList(FriendListKind kind = Friends)` | `Friends`, `Incoming`, `Outgoing` or `Blocked`, newest first (up to 1000), with `counts` |
| `SendFriendRequest(uuid)` / `SendFriendRequestByCode(code)` | Asks to be friends. If they already asked, you become friends (`FriendshipCreated`) |
| `AcceptFriendRequest(uuid)` / `DeclineFriendRequest(uuid)` | Answers a request this player received |
| `CancelFriendRequest(uuid)` | Withdraws a request this player sent |
| `RemoveFriend(uuid)` | Ends a friendship for both players |
| `BlockPlayer(uuid)` / `UnblockPlayer(uuid)` | Hides a player: ends the friendship and their requests. They are not told |

Things to know:

- **Limits** are set per app on the dashboard (Friends → Settings): friends per player (300),
  requests waiting per player (100) and players blocked per player (500). Sending requests is limited to
  20 a minute and 200 a day, blocking to 30 a minute and 300 a day.
- **Reading** friends - the friend lists, old and new, and the friend leaderboard - is limited to 120 requests a
  minute per player, together. Fetch when a screen opens rather than on a timer.
- **Being blocked looks like waiting**: a request to someone who blocked the player answers `RequestSent` and
  simply never gets an answer. Do not show anything else.
- **After signing in** onto an existing account, the player's friends, requests and blocks move with them. Fetch
  the lists again.
- **The friend leaderboard** (`Socialization.GetFriendsLeaderboard`) works with either feature.
- **Invite rewards**: friendships keep their original `since` when they move. If your game rewards a friend
  it has not seen before whose friendship is recent, a friend made shortly before the player signed in shows up
  as unseen on the real account and would be rewarded again. Record rewarded friends in cloud save on the
  account that earned them, or skip friends whose `since` is older than the sign-in.

## Advanced Integration Patterns

### Handling Friend Invites via Deep Links

In your game, you can handle friend invites through deep links. When a user shares a link to invite friends, the app can parse the deep link to add the friend automatically upon app launch or link activation.

```csharp
// Example deep link handler (integrate with your app's deep link system)
private void OnDeepLinkActivated(string url)
{
    // Parse the URL for friend UUID (e.g., yourapp://invite?friend=uuid123)
    var uri = new Uri(url);
    var query = HttpUtility.ParseQueryString(uri.Query);
    var friendUuid = query["friend"];

    if (!string.IsNullOrEmpty(friendUuid))
    {
        // Add the friend asynchronously
        _ = AddFriendFromInvite(friendUuid);
    }
}

private async Task AddFriendFromInvite(string friendUuid)
{
    try
    {
        // The inviter shared the link, so a request from the invitee is what they asked for.
        var result = await Socialization.SendFriendRequest(friendUuid);
        Debug.Log($"Friend request from invite: {result.Status}");
        
        // Optionally award a prize once you are friends
        if (result.Status == FriendStatus.FriendshipCreated)
            AwardFriendInvitePrize();
        
        // Refresh friends list
        LoadFriends();
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to add friend from invite: {e.Message}");
    }
}
```

### Syncing Friend-Related Data with Cloud Save

Store and retrieve friend-related data, such as prizes awarded for adding friends or friend-specific achievements, using Cloud Save.

```csharp
using FlyingAcorn.Soil.CloudSave;

// Save friend prize data
private async void SaveFriendPrizes(Dictionary<string, int> prizes)
{
    try
    {
        await CloudSave.SaveAsync("friendPrizes", prizes);
        Debug.Log("Friend prizes saved to cloud");
    }
    catch (Exception e)
    {
        Debug.LogError($"Failed to save friend prizes: {e.Message}");
    }
}

// Load friend prize data
private async void LoadFriendPrizes()
{
    try
    {
        var saveModel = await CloudSave.LoadAsync("friendPrizes");
        var prizes = JsonConvert.DeserializeObject<Dictionary<string, int>>(saveModel.value);
        // Use prizes data
    }
    catch (Exception e)
    {
        Debug.LogError($"Failed to load friend prizes: {e.Message}");
    }
}
```

This allows persisting friend-related rewards across devices and sessions.

## Additional Features

### Check Readiness

```csharp
if (Socialization.Ready)
{
    // Safe to call socialization methods
}
else
{
    Debug.Log("SDK not ready for socialization");
}
```

## Demo Scene

See the [demo scenes](../README.md#demo-scenes) for complete working examples: `SoilFriendRequestsExample.unity` (friend requests) and `SoilSocializationExample.unity` (old instant add).

## API Reference

- `Socialization.Ready` (property)
- `Socialization.GetFriendsLeaderboard(string leaderboardId, int count = 10, bool relative = false)` → `Task<LeaderboardResponse>` (either feature)

Old instant add, marked `[Obsolete]`:

- `Socialization.GetFriends()`
- `Socialization.AddFriendWithUUID(string uuid)`
- `Socialization.RemoveFriendWithUUID(string uuid)`

Friend requests (need the Friend requests feature):

- `Socialization.GetFriendList(FriendListKind kind = FriendListKind.Friends)` → `UniTask<FriendList>`
- `Socialization.SendFriendRequest(string uuid)`, `Socialization.SendFriendRequestByCode(string playerCode)` → `UniTask<FriendActionResult>`
- `Socialization.AcceptFriendRequest`, `DeclineFriendRequest`, `CancelFriendRequest`, `RemoveFriend`, `BlockPlayer`, `UnblockPlayer` (`string uuid`) → `UniTask<FriendActionResult>`

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.