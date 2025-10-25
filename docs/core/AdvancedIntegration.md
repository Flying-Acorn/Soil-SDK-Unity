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

## Build Configuration

The SDK includes build-time configuration to ensure proper store targeting and analytics setup. Create a `FA_Build_Settings.asset` in `Assets/Resources/` as described in the [Installation guide](../Installation.md).

### Store Settings

Set the target store for your build using the `StoreName` field. Available options:

- **Unknown**: Default, not set
- **BetaChannel**: For beta testing channels
- **Postman**: For Postman testing
- **GooglePlay**: Google Play Store
- **AppStore**: Apple App Store
- **CafeBazaar**: Android store
- **Myket**: Android store
- **Github**: GitHub releases
- **LandingPage**: Web landing page

### Enforce Store on Build

The `EnforceStoreOnBuild` option controls whether the build process requires store selection:

- **Enabled**: If no store is selected (StoreName is Unknown), the build process will display a modal dialog prompting you to choose a store. The build will fail if you cancel without selecting a store. This ensures proper store attribution for analytics tracking.
- **Disabled**: Allows building without setting a store (useful for development). If not set, you cannot track which players are coming from which store. Options:
  - Disable EnforceStoreOnBuild, but set `AnalyticsManager.SetStore()` during runtime.
  - **Suggested**: Enable EnforceStoreOnBuild and set the store in build settings before building.

### Automatic Build Data

During the build process, the SDK automatically populates:

- **BuildNumber**: Platform-specific build number (e.g., bundle version code for Android, build number for iOS)
- **LastBuildTime**: Timestamp of the build in `yyyy/MM/dd-HH:mm:ss` format
- **ScriptingBackend**: The Unity scripting backend used (Mono or IL2CPP)

This data is used internally for analytics and can be accessed via `BuildDataUtils` in code.

**Build Process Validation**: The SDK validates build settings during the build process and provides clear error messages for common issues:
- Missing BuildData asset: `"Couldn't find Build Settings, please create one!"`
- Invalid asset type: `"Found asset at '{path}' but failed to load as BuildData"`
- Store selection cancelled: `"Store name is not set, either set it from 'Build Settings' or disable 'Enforce Store On Build'"`