# Social Authentication Documentation

Third-party authentication integration for the Soil SDK, enabling users to link external accounts (Google, Apple, etc.) to their Soil user accounts.

## Quick Start

```csharp
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;

// Initialize after Soil SDK is ready
SoilServices.OnServicesReady += () =>
{
    SocialAuthentication.Initialize();
};

// Link a Google account
SocialAuthentication.Link(Constants.ThirdParty.google);

// Handle success/failure
SocialAuthentication.OnLinkSuccessCallback += (response) =>
{
    Debug.Log("Account linked successfully!");
};
```

## Documentation Contents

- **[Introduction](Introduction.md)** - Overview, features, and architecture
- **[Integration Guide](Integration.md)** - Complete implementation with examples and API reference

## Supported Platforms

- ✅ **Google**: Android, iOS, Web
- ✅ **Apple Sign-In**: iOS
- 🚧 **Facebook**: Planned
- 🚧 **Unity**: Planned

## Key Features

- Cross-platform authentication
- Account linking and unlinking
- Automatic token management
- Event-driven architecture
- Persistent link storage
- Thread-safe operations

## Requirements

- Soil SDK (initialized)
- Third-party provider credentials
- `ThirdPartySettings` configuration assets
- Platform-specific SDKs (Google Play Services, etc.)

## Example Project

See `Assets/FlyingAcorn/Soil/Core/User/ThirdPartyAuthentication/Demo/ThirdPartyAuthExample.cs` for a complete working example.

---

For questions or issues, refer to the main [Soil SDK Documentation](../README.md).</content>
<parameter name="filePath">/Users/amir/Workspace/Soil-SDK-Unity/docs/socialauthentication/README.md