# Advanced Core Integration

This document covers advanced integration patterns, event handling, status checking, and error management for the Soil SDK Core. For basic integration, see [Integration.md](Integration.md).

## Event Handling

The SDK provides events for initialization status:

```csharp
using FlyingAcorn.Soil.Core;

// Subscribe to events
SoilServices.OnServicesReady += HandleServicesReady;
SoilServices.OnInitializationFailed += HandleInitializationFailed;

private void HandleServicesReady()
{
    // SDK is fully initialized and ready
    var userId = SoilServices.UserInfo?.uuid;
    Debug.Log($"SDK ready for user: {userId}");
}

private void HandleInitializationFailed(SoilException exception)
{
    // Handle initialization failure
    Debug.LogError($"Initialization failed: {exception.Message}");

    // Check error type for specific handling
    switch (exception.ErrorCode)
    {
        case SoilExceptionErrorCode.NetworkError:
            // Handle network issues
            break;
        case SoilExceptionErrorCode.AuthenticationError:
            // Handle auth failures
            break;
        case SoilExceptionErrorCode.Timeout:
            // Handle timeouts
            break;
    }
}
```

## Checking SDK Status

### Readiness Checks

```csharp
using FlyingAcorn.Soil;

// Check if SDK is fully ready
if (SoilServices.Ready)
{
    // All services are initialized and authenticated
}

// Check basic readiness (without full auth validation)
if (SoilServices.BasicReady)
{
    // Core services are initialized
}

// Check network availability
if (SoilServices.IsNetworkAvailable)
{
    // Network is available
}
```

### User Information

```csharp
using FlyingAcorn.Soil;

// Get current user info
var userInfo = SoilServices.UserInfo;
if (userInfo != null)
{
    Debug.Log($"User ID: {userInfo.uuid}");
    Debug.Log($"Username: {userInfo.name}");
}
```

## Error Handling

The SDK uses custom exceptions for better error categorization:

```csharp
using FlyingAcorn.Soil.Core.Data;

try
{
    await SoilServices.InitializeAwaitableAsync();
}
catch (SoilException ex)
{
    switch (ex.ErrorCode)
    {
        case SoilExceptionErrorCode.NetworkError:
            // Network connectivity issues
            break;
        case SoilExceptionErrorCode.AuthenticationError:
            // Authentication failed
            break;
        case SoilExceptionErrorCode.Timeout:
            // Request timed out
            break;
        case SoilExceptionErrorCode.InvalidConfiguration:
            // SDK configuration issues
            break;
        default:
            // Other errors
            break;
    }
}
```

## Advanced Usage

### Custom Initialization Timeout

```csharp
using FlyingAcorn.Soil.Core.User;

// Set custom timeout (in seconds)
UserPlayerPrefs.RequestTimeout = 30; // Default is usually 10-15 seconds
```

### Handling User Changes

The SDK automatically handles user changes (login/logout), but you can listen for these events:

```csharp
using FlyingAcorn.Soil.Core.User;

// Listen for user data changes
UserApiHandler.OnUserFilled += (userChanged) =>
{
    if (userChanged)
    {
        // User has changed, SDK will reinitialize automatically
        Debug.Log("User changed, reinitializing SDK...");
    }
};
```