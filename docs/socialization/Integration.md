# Socialization Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

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

### 2. Get Friends List

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

### 3. Add a Friend

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

**Note**: See [Getting Player UUIDs](../socialization/Introduction.md#getting-player-uuids) in the Introduction for ways to obtain other players' UUIDs (from leaderboards or shared game info).

### 4. Remove a Friend

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
        var scores = await Socialization.GetFriendsLeaderboard(leaderboardId, count: 20, relative: true);
        
        foreach (var score in scores)
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

See the [Socialization Demo](../README.md#demo-scenes) (`SoilSocializationExample.unity`) for a complete working example.

## API Reference

- `Socialization.Ready` (property)
- `Socialization.GetFriends()`
- `Socialization.AddFriendWithUUID(string uuid)`
- `Socialization.RemoveFriendWithUUID(string uuid)`
- `Socialization.GetFriendsLeaderboard(string leaderboardId, int count = 10, bool relative = false)`

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.