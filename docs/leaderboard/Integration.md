# Leaderboard Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Setup

1. Initialize Soil SDK.
2. Configure leaderboard settings in dashboard.

## Usage

```csharp
using FlyingAcorn.Soil;

// Submit score
await Leaderboard.SubmitScoreAsync("global", 1000);

// Get top players
var leaders = await Leaderboard.GetTopPlayersAsync("global", 10);
```

## API Reference

- `Leaderboard.SubmitScoreAsync(leaderboardId, score)`
- `Leaderboard.GetTopPlayersAsync(leaderboardId, limit)`
- `Leaderboard.GetPlayerRankAsync(leaderboardId, playerId)`