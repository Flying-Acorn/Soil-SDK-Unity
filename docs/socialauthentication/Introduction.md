# Social Authentication

## Introduction

Third-party authentication integration for linking external accounts (Google, Apple, etc.) to Soil SDK user accounts. This module enables users to authenticate using their existing social media or platform accounts while maintaining a unified gaming experience.

## Supported Platforms

- **Google**: Android, iOS, and other platforms
- **Apple Sign-In**: iOS devices

## Key Features

- **Cross-platform authentication**: Unified API across different platforms
- **Account linking**: Link multiple third-party accounts to a single Soil user
- **Automatic token management**: Handles authentication tokens and refresh cycles
- **Event-driven callbacks**: Comprehensive event system for authentication states
- **Persistent linking**: Remembers linked accounts across sessions
- **Silent unlink handling**: Automatically handles failed unlinks in background

## Architecture Overview

The Social Authentication module consists of several key components:

- **`SocialAuthentication`**: Main static class providing the public API
- **`ThirdPartySettings`**: Configuration assets for each third-party provider
- **`IPlatformAuthentication`**: Platform-specific authentication handlers
- **`ThirdPartyAPIHandler`**: Backend communication for linking/unlinking accounts
- **`LinkingPlayerPrefs`**: Local storage for linked account information

## Prerequisites

Before using Social Authentication, ensure:

1. Soil SDK is properly [installed](../Installation.md) (initialization is automatic)
2. Third-party provider credentials are configured (API keys, client IDs, etc.)
3. `ThirdPartySettings` assets are created for each supported platform
4. Required third-party SDKs are integrated (Google Play Services, Sign In with Apple, etc.)

## Quick Start

```csharp
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication;

// Social Authentication automatically initializes the Soil SDK if needed
SocialAuthentication.Initialize();

// Handle authentication events
SocialAuthentication.OnInitializationSuccess += () =>
{
    Debug.Log("Social Authentication ready!");
};

SocialAuthentication.OnLinkSuccessCallback += (response) =>
{
    Debug.Log($"Successfully linked {response.detail.app_party.party}");
};
```

## Basic Usage Flow

1. **Initialize**: Call `SocialAuthentication.Initialize()` (handles SDK setup automatically)
2. **Link accounts**: Use `SocialAuthentication.Link()` to authenticate with Google, Apple, etc.
3. **Handle results**: Process success/failure callbacks
4. **Check links**: Retrieve linked accounts with `SocialAuthentication.GetLinks()`
5. **Unlink accounts**: Remove links with `SocialAuthentication.Unlink()` when needed
6. **Update loop**: Call `SocialAuthentication.Update()` in your game loop

See [Integration Guide](Integration.md) for detailed implementation with complete code examples.</content>
<parameter name="filePath">/Users/amir/Workspace/Soil-SDK-Unity/docs/socialauthentication/Introduction.md