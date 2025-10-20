# Core Integration

## Prerequisites

Before integrating the Soil SDK Core, ensure you have completed the [Installation](../Installation.md) setup. This includes:
- Adding the SDK to your Unity project
- Configuring SDK settings (App ID, SDK Token, etc.)
- Installing required dependencies

## Basic Integration

To integrate the Soil SDK Core, initialize it at the start of your application and handle success/failure events:

```csharp
using FlyingAcorn.Soil;
using FlyingAcorn.Soil.Core;

public class GameStart : MonoBehaviour
{
    private void Awake()
    {
        SoilServices.OnServicesReady += OnServicesReady;
        SoilServices.OnInitializationFailed += OnInitializationFailed;
    }

    private void Start()
    {
        SoilServices.InitializeAsync();
    }

    private void OnServicesReady()
    {
        Debug.Log("SDK initialized successfully!") 
        Debug.Log($"User: {SoilServices.UserInfo.name}");
    }

    private void OnInitializationFailed(SoilException exception)
    {
        Debug.LogError($"SDK initialization failed: {exception.Message}");
    }
}
```

That's it! The SDK will handle the rest automatically.

## Updating Player Information

To update player profile information such as name, username, avatar asset, or custom properties, use the `UserApiHandler.UpdatePlayerInfoAsync` method. This method compares the provided `UserInfo` with the current user info and only sends changed fields to the server for efficiency.

### Modifying User Info

First, create a modified copy of the current user info using the fluent methods:

```csharp
using FlyingAcorn.Soil.Core.User;

// Ensure SDK is initialized
if (!SoilServices.Ready)
{
    Debug.LogError("SDK not ready");
    return;
}

// Modify the user info
UserInfo updatedUser = SoilServices.UserInfo
    .RecordName("New Display Name")
    .RecordUsername("new_username")
    .RecordAvatarAsset("https://example.com/avatar.png")
    .RecordCustomProperty("player_level", 25)
    .RecordCustomProperty("favorite_color", "blue");
```

### Sending the Update

Then, call the update method asynchronously:

```csharp
using FlyingAcorn.Soil.Core.User;

public async void UpdatePlayerProfile()
{
    try
    {
        UserInfo result = await UserApiHandler.UpdatePlayerInfoAsync(updatedUser);
        Debug.Log($"Profile updated successfully! New name: {result.name}");
        
        // The local UserInfo is automatically updated
        // SoilServices.UserInfo now contains the latest data
    }
    catch (SoilException e)
    {
        if (e.ErrorCode == SoilExceptionErrorCode.NotReady)
        {
            Debug.LogError("SDK not initialized");
        }
        else
        {
            Debug.LogError($"Update failed: {e.Message}");
        }
    }
}
```

### Important Notes

- **Incremental Updates**: Only fields that differ from the current user info are sent to the server. If no changes are detected, no request is made.
- **Validation**: The method validates that the SDK is ready before proceeding.
- **Automatic Local Update**: Upon successful server update, the local `SoilServices.UserInfo` is automatically replaced with the updated data.
- **Custom Properties**: Use `RecordCustomProperty` to store game-specific data like player stats or preferences.
- **Username Uniqueness**: Usernames must be unique across the entire application. Attempting to set a duplicate username will result in an error.
- **Error Handling**: Handle `SoilException` for network errors, invalid data, or SDK not ready.

## Demo Scene

See the [Core Demo](./../README.md#demo-scenes) (`SoilExample.unity`) for a complete working example of SDK initialization and basic usage.

## Advanced Integration

For detailed event handling, status checking, error management, and advanced usage patterns, see the [Advanced Integration Guide](AdvancedIntegration.md).

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.

## Next Steps

After setting up the core SDK, you can integrate additional modules as needed:

- [Advertisement](../advertisement/Integration.md)
- [Cloud Save](../cloudsave/Integration.md)
- [Economy](../economy/Integration.md)
- [Leaderboards](../leaderboard/Integration.md)
- [Purchasing](../purchasing/Integration.md)
- [Remote Config](../remoteconfig/Integration.md)
- [Socialization](../socialization/Integration.md)