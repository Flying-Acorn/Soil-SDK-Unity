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
    private void Start()
    {
        if (!SoilServices.Ready)
        {
            SoilServices.OnServicesReady += OnServicesReady;
            SoilServices.OnInitializationFailed += OnInitializationFailed;
            SoilServices.InitializeAsync();
        }
        else
        {
            OnServicesReady();
        }
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

The safest way to update player information is using the fluent builder API, which automatically handles creating copies to avoid reference issues:

#### Fluent Builder API

```csharp
using FlyingAcorn.Soil.Core.User;

// Ensure SDK is initialized
if (!SoilServices.Ready)
{
    Debug.LogError("SDK not ready");
    return;
}

public async void UpdatePlayerProfile()
{
    try
    {
        // Use the fluent builder API - safest and most convenient
        UserInfo result = await UserApiHandler.UpdatePlayerInfo()
            .WithName("New Display Name")
            .WithUsername("new_username")
            .WithAvatarAsset("https://example.com/avatar.png")
            .WithCustomProperty("player_level", 25)
            .WithCustomProperty("favorite_color", "blue");
        
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
- **Custom Properties**: Use `WithCustomProperty` in the fluent builder to store game-specific data like player stats or preferences. These are automatically merged into the `properties` field when sent to the server.
- **Reserved Property Names**: Property keys starting with `flyingacorn_` are reserved for SDK use and cannot be used for custom properties. Attempting to use reserved keys will throw an exception.
- **Username Uniqueness**: Usernames must be unique across the entire application. Attempting to set a duplicate username will result in an error.
- **Error Handling**: Handle `SoilException` for network errors, invalid data, or SDK not ready.
- **Safe by Design**: The fluent builder automatically creates copies to prevent reference issues that could cause updates to be skipped.

## Demo Scene

See the [Core Demo](./../README.md#demo-scenes) (`SoilExample.unity`) for a complete working example of SDK initialization and basic usage.

## Advanced Integration

For detailed event handling, status checking, error management, and advanced usage patterns, see the [Advanced Integration Guide](AdvancedIntegration.md).


## Next Steps

After setting up the core SDK, you can integrate additional modules as needed:

See the [Services overview](../README.md#services) for information on other available modules.
