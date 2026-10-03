# Socialization

## Introduction

Friend system and close competition features to create an engaging social gaming experience.

Two versions share the same friendships:

- **Friends v1** (`Socialization`): adding a player makes you friends at once.
- **Friends v2** (`FriendsV2`): requests the other player accepts or declines, plus blocking. Players can add
  each other by the short code `SoilServices.UserInfo.public_id`, which is easier to share and type than a UUID.
  See [Friends v2](Integration.md#friends-v2-requests-accept-and-block).

## Getting Player UUIDs

To add friends, you need their UUID. Here are common ways to obtain UUIDs:

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

- Friends v1: `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilSocializationExample.unity`
- Friends v2: `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilFriendsV2Example.unity` - your code, the four lists with their counts, sending by code or UUID, and each list's actions. Needs the Socialization v2 feature.

Both are also reachable from the scene switcher (`Assets/FlyingAcorn/Soil/Demo/SoilSceneSwitcher.unity`).

## Dependencies

- Core SDK