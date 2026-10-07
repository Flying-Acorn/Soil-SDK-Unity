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
(`FriendStatus`) and `Succeeded`. Refusals include too many requests (`Throttled`, with `RetryAfterSeconds`), an
invalid request and a server error (`FriendshipError`). A `SocializationException` (`ErrorCode`) is thrown, always
through the awaited task, only when there is no answer of that kind. Reads (`GetFriendList`, and `GetReferralInfo`
below) have no refusal answers: anything but their list throws.

| Case | `ErrorCode` |
|---|---|
| No connection, or the request timed out | `TransportError` / `Timeout` |
| The sign-in expired | `InvalidToken` |
| The app does not have the feature (HTTP 403) | `Forbidden` |
| The player's account was not found (HTTP 404) | `NotFound` |
| `GetFriendList` read too often, or a proxy refusing any call (HTTP 429) | `TooManyRequests`, with `RetryAfterSeconds` set |
| A proxy error page, a server error on a read, or the service is down | `TransportError` / `ServiceUnavailable` |
| A success answer the SDK cannot read | `InvalidResponse` |
| An empty `uuid` or code, or Soil not initialized | `InvalidRequest` / `NotReady` |

Every action is safe to retry after a timeout.

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
    case FriendStatus.RequestCooldown: ShowAskedRecently(sent.user.name); break; // try again tomorrow
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
| `SendFriendRequest(uuid)` / `SendFriendRequestByCode(code)` | Asks to be friends. If they already asked, you become friends (`FriendshipCreated`). Not again within 24 hours of a cancel, decline, or block (`RequestCooldown`) |
| `AcceptFriendRequest(uuid)` / `DeclineFriendRequest(uuid)` | Answers a request this player received |
| `CancelFriendRequest(uuid)` | Withdraws a request this player sent |
| `RemoveFriend(uuid)` | Ends a friendship for both players |
| `BlockPlayer(uuid)` / `UnblockPlayer(uuid)` | Hides a player: ends the friendship and withdraws this player's request to them. Their request to this player stays, hidden, and goes on unblock. They are not told |

Things to know:

- **Limits** are set per app on the dashboard (Friends → Settings): friends per player (300),
  requests waiting per player (100) and players blocked per player (500). Sending requests is limited to
  20 a minute and 200 a day, blocking to 30 a minute and 300 a day.
- **Reading** friends - the friend lists, old and new, and the friend leaderboard - is limited to 120 requests a
  minute per player, together. Fetch when a screen opens rather than on a timer. Past it, `GetFriendList` throws
  `TooManyRequests` with `RetryAfterSeconds`.
- **Asking again waits 24 hours.** After a player cancels their own request to someone (blocking them withdraws it
  the same way), or that player declines it or blocks and later unblocks them, a new request from them to that player answers `RequestCooldown` (HTTP 409, with `user`) for 24 hours. This
  stops request spam: cancel-and-resend, or asking over and over after a no. Tell the player they can try again
  tomorrow. It never blocks becoming friends: if the other player has asked in the meantime, sending still answers
  `FriendshipCreated`, and accepting their request works as usual. (A request the player cannot see, from someone
  who is restricted, does not count: sending then answers `RequestCooldown` as if it were not there.)
- **Too many blocks restrict a player.** When the app's threshold of players blocking someone is reached
  (dashboard, Friends → Settings; off until set), that player's sends and accepts answer `SocializationRestricted`
  (HTTP 409) until enough of them unblock or delete their accounts, or the threshold is raised or turned off. Tell
  them they can't add friends right now; don't send them off to clear a list.
  Only the restricted player is ever told: their waiting requests are hidden from the players they asked, and a
  request to them answers `RequestSent` as usual. When the restriction lifts, the requests they sent are dropped,
  so fetch the outgoing list again rather than assuming they still wait. Blocking, declining and cancelling still
  work.
- **Being blocked looks like waiting**: a request to someone who blocked the player answers `RequestSent` and
  simply never gets an answer. Do not show anything else.
- **After signing in** onto an existing account, the player's friends, requests and blocks move with them. Fetch
  the lists again.
- **The friend leaderboard** (`Socialization.GetFriendsLeaderboard`) works with either feature.
- **Invite rewards**: to reward players for bringing new players, use [Referrals](#referrals-invite-codes-and-rewards),
  which the server rewards once per new player. If you reward friendships yourself instead: friendships keep their original `since` when they move. If your game rewards a friend
  it has not seen before whose friendship is recent, a friend made shortly before the player signed in shows up
  as unseen on the real account and would be rewarded again. Record rewarded friends in cloud save on the
  account that earned them, or skip friends whose `since` is older than the sign-in.

### Codes

`FriendStatus` (`detail.code`); the server only appends new ones, so treat an unknown number as a refusal you
cannot name (`Succeeded` still tells you whether it went through).

| Code | `FriendStatus` | HTTP | Meaning |
|---|---|---|---|
| 0 | `FriendshipExists` | 200 | Already friends (a request or accept that went through before) |
| 1 | `FriendshipCreated` | 200 | Now friends: an accept, or a request to a player who had already asked |
| 2 | `FriendshipDeleted` | 200 | The friendship is gone (also when there was none) |
| 3 | `FriendNotFound` | 404 | No such player in this game |
| 4 | `FriendshipIllegalSelf` | 400 | The player named themselves |
| 5 | `FriendshipError` | 500 | A server error; try again later |
| 6 | `Throttled` | 429 | Too many actions; `RetryAfterSeconds` says how long to wait |
| 7 | `RequestSent` | 200 | The request is waiting for an answer (now, or sent before) |
| 8 | `RequestDeclined` | 200 | The request was declined (also when there was none) |
| 9 | `RequestCancelled` | 200 | The request was withdrawn (also when there was none) |
| 10 | `RequestNotFound` | 404 | No request from that player to accept |
| 11 | `UserBlocked` | 200 | The player is blocked |
| 12 | `UserUnblocked` | 200 | The player is unblocked |
| 13 | `FriendBlocked` | 409 | This player blocked that one; unblock them first |
| 14 | `FriendLimitReached` | 409 | One of the two friend lists is full |
| 15 | `RequestLimitReached` | 409 | Too many sent requests waiting for an answer |
| 16 | `InvalidRequest` | 400, 405 | The request was malformed, or used the wrong method |
| 17 | `FriendsListed` | 200 | The answer of `GetFriendList` |
| 18 | `BlockLimitReached` | 409 | Too many blocked players |
| 19 | `SocializationRestricted` | 409 | Too many players block this one: no sends or accepts for now |
| 20 | `RequestCooldown` | 409 | Asked again within 24 hours of cancelling, of being declined, or of a block between them |

## Referrals: invite codes and rewards

Referrals let a new player name the player who invited them, and reward both. They need the **Referrals**
feature on your app (ask your Soil contact). A player's referral code is their player code,
`SoilServices.UserInfo.public_id` - the same code friend requests use, so there is nothing new to show.

### The flow

1. **The inviter shares their code**: show `SoilServices.UserInfo.public_id` with a share button that sends
   the code and your game's store link (for example "Play with me! Enter my code K7M29QX4: https://...").
2. **The new player enters it**, with `Socialization.RedeemReferralCode(code)` on an "Enter invite code"
   screen. Or, if your app turned on **A friend request counts as entering the code** (dashboard, Friends →
   Settings), simply by sending a friend request with `Socialization.SendFriendRequestByCode(code)`: when that
   request made the invite, the answer carries it in `FriendActionResult.invite`.
3. **Rewards are given on the server**, at that moment, to both players' Soil economy currency balances: the
   currency and amount each side gets are set on the dashboard. Nothing needs to be called to grant them, and
   they are never taken back. The new player's reward is also in the answer (`reward`, null when the app gives
   none), **for showing only**: it is already in their balance. Do not add it to your own wallet as well; if your
   game moves a Soil currency into its own wallet as below, the new player's reward arrives that way too, so
   granting `reward` yourself would pay it twice.
4. **The inviter learns of their reward by their currency balance rising** - there is no other call or
   notification. Read the balance (`Economy.GetSummary()`, for example when the game starts or the invite
   screen opens) and turn it into what your game uses. A simple way is a currency of its own, for example
   `ReferralGem`, that the game drains into its local gems:

```csharp
using FlyingAcorn.Soil.Economy;

// Call when the game starts and when the invite screen opens, once Economy is initialized.
private async UniTask CollectReferralGems()
{
    var summary = await Economy.GetSummary();
    var pending = summary.virtual_currencies.Find(c => c.Identifier == "ReferralGem")?.Balance ?? 0;
    if (pending <= 0) return;
    // Take them from Soil first, then give them locally, and save the wallet at once. This order never pays twice;
    // its risk is the opposite one: a crash (or a decrease that went through but timed out) between the two lines
    // loses these gems for the player.
    await Economy.DecreaseVirtualCurrency("ReferralGem", pending);
    LocalWallet.AddGems(pending);
    LocalWallet.Save();
    ShowToast($"Your friends joined! +{pending} gems");
}
```

Decreasing by the amount you read (not setting the balance to 0) keeps a reward that lands in between for the
next time. If your game keeps its gems in Soil economy itself, give the reward in that currency and skip the
conversion.

Balances are kept on the server as 64-bit numbers, while `Balance` is an `int`: a balance above `int.MaxValue`
(2,147,483,647) reads as `int.MaxValue`. Decreasing by that much leaves the rest for the next read: calling
`CollectReferralGems` again collects the rest.

### Entering a code

```csharp
using FlyingAcorn.Soil.Socialization;
using FlyingAcorn.Soil.Socialization.Logic;

var info = await Socialization.GetReferralInfo();
ShowMyCode(SoilServices.UserInfo.public_id);
ShowInvitedCount(info.invited_count);
SetEnterCodeVisible(info.can_redeem); // hide the screen once the player is invited or the window has closed
if (info.RedeemUntilUtc is { } until) ShowDeadline(until.ToLocalTime());

var result = await Socialization.RedeemReferralCode(typedCode);
switch (result.Status)
{
    case ReferralStatus.Invited:
        ShowThanks(result.inviter.name, result.reward); // reward: { currency, amount } or null
        break;
    case ReferralStatus.CodeNotFound: ShowNoSuchPlayer(); break;
    case ReferralStatus.OwnCode: ShowThatIsYou(); break;
    case ReferralStatus.AlreadyInvited:
    case ReferralStatus.WindowClosed: HideEnterCode(); break;
    case ReferralStatus.MutualInvite: ShowYouInvitedThem(); break;
    case ReferralStatus.Throttled: RetryIn(result.RetryAfterSeconds); break;
}
```

When the app counts friend requests as invites, check `invite` on the request's answer. It is there only when this
request made a new invite: `Invited`, with the new player's `reward` (null when the app gives none; already in their
balance, so show it only). The invite and the request stand on their own: an invite can be made while the request
itself is refused (for example a full request list), and the other way round.

```csharp
var sent = await Socialization.SendFriendRequestByCode(typedCode);
ShowRequestOutcome(sent.Status);
// Only present when this request made the invite; reward is already in the balance.
if (sent.invite is { Succeeded: true }) ShowThanks(sent.user.name, sent.invite.reward);
```

`invite` is null whenever the request did not make an invite: the player already has an inviter (the same one
again too: unlike `RedeemReferralCode`, a request does not repeat an earlier invite), is past the window, sent their
own code, or invited that player themselves; the code matches no player (`FriendNotFound`) or staff stopped it; the
request was sent by UUID or was invalid; or the app has Referrals or the switch off. A friend request never answers
why no invite was made: to tell the player, use `RedeemReferralCode`. One more case: when counting the invite
failed on the server, `invite` has `Status` `ReferralError` and no reward, while the request itself was still
handled (and may have been sent or refused on its own). Let the player enter the code with `RedeemReferralCode`.

As with friend requests, a refusal is an answer, not an exception. A `SocializationException` is thrown in the
same cases as for friend requests: no connection or a timeout, an expired sign-in, the feature being off (HTTP 403,
`SoilExceptionErrorCode.Forbidden`), the account not found (`NotFound`), and `GetReferralInfo` read too often
(`TooManyRequests`, with `RetryAfterSeconds`). Entering a code is safe to retry, for example after a timeout: the
same code again answers `Invited` with the reward given the first time (it is granted only once), unless staff
stopped that code or its owner's account is gone since (`CodeNotFound`). Another player's code answers
`AlreadyInvited`.

### Rules

- **One inviter.** Once a player has an inviter the game cannot change it, whichever way it was entered. Only
  staff can remove it (to fix a wrong code); the player can then enter a code again.
- **A window.** Only players whose account is younger than the app's window can enter a code (7 days by
  default; it can be turned off so every player can enter one). `GetReferralInfo` gives `can_redeem` and
  `redeem_until` (null when there is no window).
- **A cap.** An inviter is rewarded for at most the app's cap of invites (20 by default; it can be turned off).
  Invites past it still count in `invited_count`, unrewarded. The cap never applies to the new player: they are
  rewarded whenever the app sets a reward for new players.
- **Not their own code, and not mutual**: a player cannot enter a code of someone they invited themselves.
- Codes are matched within the game only; casing, spaces and dashes do not matter. A code staff have stopped
  answers as not found.
- Entering codes is limited to 10 a minute and 30 a day per player (answered `Throttled`); `GetReferralInfo` to 60
  a minute (throws `TooManyRequests` with `RetryAfterSeconds`).
- **After signing in** onto an existing account, the player's inviter and the players they invited move with
  them (an account keeps its own inviter if it already has one).

### Codes

`ReferralStatus` (`detail.code`); the server only appends new ones.

| Code | `ReferralStatus` | HTTP | Meaning |
|---|---|---|---|
| 0 | `Invited` | 200 | The code was entered (now, or by this same code before); `inviter` is set, and `reward` unless the app gives new players none |
| 1 | `CodeNotFound` | 404 | No player in this game has that code |
| 2 | `OwnCode` | 400 | The player's own code |
| 3 | `AlreadyInvited` | 409 | The player already has an inviter |
| 4 | `WindowClosed` | 409 | The player's account is older than the window |
| 5 | `MutualInvite` | 409 | The code's owner was invited by this player |
| 6 | `InvalidRequest` | 400, 405 | The request was malformed, or used the wrong method |
| 7 | `ReferralError` | 500 | A server error; try again later |
| 8 | `Throttled` | 429 | Too many tries; `RetryAfterSeconds` says how long to wait |
| 9 | `ReferralInfo` | 200 | The answer of `GetReferralInfo` |

## Advanced Integration Patterns

### Handling Friend Invites via Deep Links

In your game, you can handle friend invites through deep links. When a player shares a link with their player code, the app can send the friend request when the link opens it.

```csharp
// Example deep link handler (integrate with your app's deep link system)
private void OnDeepLinkActivated(string url)
{
    // The link carries the inviter's player code, e.g. yourapp://invite?code=K7M29QX4
    var uri = new Uri(url);
    var query = HttpUtility.ParseQueryString(uri.Query);
    var code = query["code"];

    if (!string.IsNullOrEmpty(code))
        _ = SendRequestFromInvite(code);
}

private async UniTask SendRequestFromInvite(string code)
{
    try
    {
        // The inviter shared the link, so a request from the invitee is what they asked for.
        var result = await Socialization.SendFriendRequestByCode(code);
        Debug.Log($"Friend request from invite: {result.Status}");

        // Do not reward friendships yourself (see "Invite rewards" above): with Referrals and
        // "A friend request counts as entering the code" on, this request is the invite, rewarded by the server.
        if (result.invite is { Succeeded: true })
            ShowThanks(result.user.name, result.invite.reward); // already in the balance: show only

        // Refresh the lists
        var friends = await Socialization.GetFriendList(FriendListKind.Outgoing);
        ShowSentRequests(friends.users);
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to send a friend request from invite: {e.Message}");
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

See the [demo scenes](../README.md#demo-scenes) for complete working examples: `SoilFriendRequestsExample.unity` (friend requests, and referrals: "Enter invite code" and "Invites") and `SoilSocializationExample.unity` (old instant add).

## API Reference

- `Socialization.Ready` (property)
- `Socialization.GetFriendsLeaderboard(string leaderboardId, int count = 10, bool relative = false)` → `UniTask<LeaderboardResponse>` (either feature)

Old instant add, marked `[Obsolete]`:

- `Socialization.GetFriends()`
- `Socialization.AddFriendWithUUID(string uuid)`
- `Socialization.RemoveFriendWithUUID(string uuid)`

Friend requests (need the Friend requests feature):

- `Socialization.GetFriendList(FriendListKind kind = FriendListKind.Friends)` → `UniTask<FriendList>`
- `Socialization.SendFriendRequest(string uuid)`, `Socialization.SendFriendRequestByCode(string playerCode)` → `UniTask<FriendActionResult>`
- `Socialization.AcceptFriendRequest`, `DeclineFriendRequest`, `CancelFriendRequest`, `RemoveFriend`, `BlockPlayer`, `UnblockPlayer` (`string uuid`) → `UniTask<FriendActionResult>`

Referrals (need the Referrals feature):

- `Socialization.GetReferralInfo()` → `UniTask<ReferralInfo>` (`invited`, `invited_by`, `can_redeem`, `redeem_until` / `RedeemUntilUtc`, `invited_count`)
- `Socialization.RedeemReferralCode(string code)` → `UniTask<ReferralRedeemResult>` (`Status`, `Succeeded`, `reward`, `inviter`, `RetryAfterSeconds`)
- `FriendActionResult.invite` → `ReferralInvite` (`Status`, `Succeeded`, `reward`), set on `SendFriendRequestByCode` only when that request made an invite (or counting it failed: `ReferralError`), in apps that count friend requests as invites

Errors: `SocializationException` (a `SoilException`) with `ErrorCode`, `Operation` and `RetryAfterSeconds` (set when the server sent `Retry-After`, for example on `TooManyRequests`).

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.