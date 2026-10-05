# Socialization

## Introduction

Friend system and close competition features to create an engaging social gaming experience.

Two versions share the same friendships:

- **Friend requests**: the other player accepts or declines, and players can block each other. Players add
  each other by the short code `SoilServices.UserInfo.public_id`, which is easier to share and type than a UUID.
  See [Friend requests](Integration.md#friend-requests-send-accept-and-block).
- **Instant add** (old, marked obsolete): adding a player makes you friends at once, without asking. It keeps
  working for builds already shipped.

**Referrals** reward players for bringing new players into the game. A new player enters the code of the player
who invited them (`Socialization.RedeemReferralCode`, or a friend request by code when the app counts those),
and the server adds both players' rewards to their Soil economy currency balances. The inviter's game finds
their reward by their balance rising. See [Referrals](Integration.md#referrals-invite-codes-and-rewards).

## Finding Other Players

The simplest way is the player code: show `SoilServices.UserInfo.public_id` (for example `K7M29QX4`) in your
game, and let other players type it into `Socialization.SendFriendRequestByCode`. Every friend list and answer
then gives you the UUID the other actions take. The same code is the player's referral code.

The old instant add takes UUIDs only. Here are common ways to obtain them:

### From Leaderboard Scores

When fetching leaderboard data, each `UserScore` contains the player's UUID:

```csharp
using FlyingAcorn.Soil.Leaderboard;

// Fetch leaderboard
var response = await Leaderboard.FetchLeaderboardAsync("my_leaderboard", count: 50);

// Extract UUIDs from scores
foreach (var score in response.user_scores)
{
    string playerUuid = score.uuid;
    string playerName = score.name;
    
    // Show in UI for friend requests
    CreateFriendRequestButton(playerName, playerUuid);
}
```

### Sharing Your Game Info

Include your player UUID when sharing game achievements or invites:

```csharp
using FlyingAcorn.Soil.Core;

// Get current player's UUID
string myUuid = SoilServices.UserInfo.uuid;

// Include in share message
string shareMessage = $"Check out my high score! Add me as a friend: {myUuid}";

// Share via platform (email, social media, etc.)
ShareGameInfo(shareMessage);
```

Players can then copy the UUID from the shared message and use it to send friend requests.

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scenes:

- Friend requests: `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilFriendRequestsExample.unity` - your code, the four lists with their counts, sending by code or UUID, and each list's actions. Needs the Friend requests feature. Its "Enter invite code" and "Invites" buttons try referrals (need the Referrals feature).
- Instant add (old): `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilSocializationExample.unity`

Both are also reachable from the scene switcher (`Assets/FlyingAcorn/Soil/Demo/SoilSceneSwitcher.unity`).

## Dependencies

- Core SDK