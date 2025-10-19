# Leaderboard Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Prerequisites

Leaderboard requires the Core SDK to be initialized first.

### Initializing SoilServices

```csharp
using FlyingAcorn.Soil.Core;

if (!SoilServices.Ready)
{
    SoilServices.OnServicesReady += OnServicesReady;
    SoilServices.InitializeAsync();
}

private void OnServicesReady()
{
    // Leaderboard is now ready to use
    Debug.Log("SoilServices initialized, Leaderboard ready!");
}
```

### Checking Readiness

Ensure Leaderboard is ready before operations:

```csharp
if (Leaderboard.Ready)
{
    // Proceed with score submission, fetching, etc.
}
```

Note: `Leaderboard.Ready` is equivalent to `SoilServices.Ready`.

### Submitting Scores

Submit a score to a leaderboard:

```csharp
using FlyingAcorn.Soil.Leaderboard;

try
{
    var userScore = await Leaderboard.ReportScore(score: 1000, leaderboardId: "global_leaderboard");
    Debug.Log($"Score submitted! Rank: {userScore.rank}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to submit score: {e.Message}");
}
```

You can also submit string scores for large numbers:

```csharp
try
{
    var userScore = await Leaderboard.ReportScore(score: "999999999999", leaderboardId: "big_score_board");
    Debug.Log("Large score submitted!");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to submit score: {e.Message}");
}
```

### Fetching Leaderboards

Get top players from a leaderboard:

```csharp
try
{
    var topPlayers = await Leaderboard.FetchLeaderboardAsync(leaderboardId: "global_leaderboard", count: 10);
    foreach (var player in topPlayers)
    {
        Debug.Log($"{player.name}: {player.score} (Rank {player.rank})");
    }
}
catch (SoilException e)
{
    Debug.LogError($"Failed to fetch leaderboard: {e.Message}");
}
```

Get leaderboard relative to the current player:

```csharp
try
{
    var nearbyPlayers = await Leaderboard.FetchLeaderboardAsync(leaderboardId: "global_leaderboard", count: 10, relative: true);
    // Shows players around your rank
}
catch (SoilException e)
{
    Debug.LogError($"Failed to fetch relative leaderboard: {e.Message}");
}
```

### Deleting Scores

Remove your score from a leaderboard:

```csharp
try
{
    await Leaderboard.DeleteScore(leaderboardId: "global_leaderboard");
    Debug.Log("Score deleted!");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to delete score: {e.Message}");
}
```

## Caching

Leaderboard data is automatically cached locally using `LeaderboardPlayerPrefs`. This provides offline access to previously fetched leaderboards.

## Cancellation Support

All async operations support cancellation tokens:

```csharp
var cts = new CancellationTokenSource();
try
{
    var scores = await Leaderboard.FetchLeaderboardAsync(leaderboardId: "board", count: 10, cancellationToken: cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log("Leaderboard fetch was canceled");
}
```

## Demo Scene

See the [Leaderboard Demo](../README.md#demo-scenes) (`SoilLeaderboardExample.unity`) for a complete working example of score submission, leaderboard fetching, and relative mode.

## API Reference

- `Leaderboard.ReportScore(double score, string leaderboardId, CancellationToken cancellationToken = default)`
- `Leaderboard.ReportScore(string score, string leaderboardId, CancellationToken cancellationToken = default)`
- `Leaderboard.FetchLeaderboardAsync(string leaderboardId, int count = 10, bool relative = false, CancellationToken cancellationToken = default)`
- `Leaderboard.DeleteScore(string leaderboardId, CancellationToken cancellationToken = default)`

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.