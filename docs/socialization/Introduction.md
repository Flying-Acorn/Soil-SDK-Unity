# Socialization

## Introduction

Friend system and close competition features to create an engaging social gaming experience.

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

Demo scene: `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilSocializationExample.unity`

## Dependencies

- Core SDK