# Installation

## Step 1: Download and Import the SDK

Based on your needs, only import the package you need from <a href="https://github.com/Flying-Acorn/Soil-SDK-Unity/releases/latest" target="_blank">here</a>.

### How to choose
- BoosterPack - `Soil-X.Y.Z-BoosterPack.unitypackage`
    - [Core](./core/Introduction.md) - Unified initialization and orchestration.
        - [SocialAuthentication](./socialauthentication/Integration.md) (Only when using, import SocialAuthentication - `Soil-X.Y.Z-SocialAuthentication_Extension.unitypackage` along with the BoosterPack)
    - [Leaderboards](./leaderboard/Introduction.md) - Player rankings.
    - [Cloud Save](./cloudsave/Introduction.md) - Data persistence.
    - [Remote Config](./remoteconfig/Introduction.md) - Runtime configurations.
    - [Social Authentication](./socialauthentication/Introduction.md) - Third-party authentication.
    - [Economy](./economy/Introduction.md) - Virtual currencies and inventory.
    - [Socialization](./socialization/Introduction.md) - Friend systems.

- Purchasing - `Soil-X.Y.Z-Purchasing.unitypackage`
    - [Purchasing](./purchasing/Introduction.md) - In-app purchases.

- Advertisement - `Soil-X.Y.Z-Advertisement.unitypackage`
    - [Advertisement](./advertisement/Introduction.md) - Ad monetization.


## Platform Support

The Soil SDK officially supports **Android** and **iOS** platforms. While the SDK may work on other platforms, unexpected behavior may occur. For the best experience and full feature support, we recommend developing and deploying on Android or iOS platforms.

## Step 2: Ensure Dependencies

Ensure the following are present in your project:
- <a href="https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html" target="_blank">Newtonsoft JSON</a>: Within Package Manager, import `com.unity.nuget.newtonsoft-json` package by name
- <a href="https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html" target="_blank">TextMeshPro</a> - Embedded within Unity
- <a href="https://github.com/Flying-Acorn/Analytics-Middleware-for-Unity" target="_blank">FlyingAcorn/Analytics-Middleware-for-Unity</a>(Embedded within downloaded packages)
- <a href="https://github.com/Cysharp/UniTask" target="_blank">Cysharp/UniTask</a>(Embedded within downloaded packages)

**Note for Demo Scenes**: If you plan to use the demo scenes included with the SDK, you must import the TextMeshPro Essential Resources. Go to `Window > TextMeshPro > Import TMP Essential Resources` in the Unity Editor.

## Step 3: Create SDKSettings File

Inside your `Assets/Resources/` create a `SDKSettings.asset` using the following navigation:

<img src="./images/SDKSettings1.jpeg" width="800" alt="Create SDKSettings.asset" />

Set your app ID and SDK token (ask your Soil contact for these):

<img src="./images/SDKSettings2.jpeg" width="800" alt="Setup SDKSettings.asset" />

## Step 4: Create FA_Build_Settings File

Inside your `Assets/Resources/` create a `FA_Build_Settings.asset` using the following navigation:

<img src="./images/FA_Build_Settings1.jpeg" width="800" alt="Create FA_Build_Settings.asset" />

**Note**: Whenever you want to build, set the build store target within the Inspector of this file.

<img src="./images/FA_Build_Settings2.jpeg" width="800" alt="Set store FA_Build_Settings.asset" />

### Build Settings Configuration

- **StoreName**: Select the target store for your build (e.g., GooglePlay, AppStore, CafeBazaar). This ensures analytics and other services are configured correctly for the platform.
- **EnforceStoreOnBuild**: When enabled, the build process will prompt you to select a store if unknown is set. This ensures proper store attribution for analytics tracking.

## Installation Complete

Your Soil SDK is now installed and configured! You can start integrating services. We recommend beginning with the [Core module](./core/Integration.md) for basic setup, then add other modules as needed. See the [Services overview](./README.md#services) for all available integrations.
