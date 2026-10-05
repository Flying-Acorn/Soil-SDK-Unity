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
    - [Socialization](./socialization/Introduction.md) - Friends: requests, blocking, friend leaderboards and referrals.
    - [Feedback](./feedback/Introduction.md) - Support messages, ratings with a reason and suggestions from players.

- Purchasing - `Soil-X.Y.Z-Purchasing.unitypackage`
    - [Purchasing](./purchasing/Introduction.md) - In-app purchases.

- Advertisement - `Soil-X.Y.Z-Advertisement.unitypackage`
    - [Advertisement](./advertisement/Introduction.md) - Ad monetization.


## Platform Support

The Soil SDK officially supports **Android** and **iOS** platforms. While the SDK may work on other platforms, unexpected behavior may occur. For the best experience and full feature support, we recommend developing and deploying on Android or iOS platforms.

## Step 2: Ensure Dependencies

Install these from the Package Manager:
- <a href="https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html" target="_blank">Newtonsoft JSON</a> (`com.unity.nuget.newtonsoft-json`): required by every package. Add it by name.
- Unity UI (`com.unity.ugui`): installed in new projects by default.
- <a href="https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html" target="_blank">TextMeshPro</a> (`com.unity.textmeshpro` before Unity 6, part of `com.unity.ugui` from Unity 6): only the demo scenes use it. To open them, also import the TextMeshPro Essential Resources from `Window > TextMeshPro > Import TMP Essential Resources`.

These come inside the downloaded packages. When your project already has one of them, untick its folder in Unity's *Import Unity Package* window, so the package does not overwrite your copy. Importing never deletes files but does overwrite them, including with older versions:
- <a href="https://github.com/Flying-Acorn/Analytics-Middleware-for-Unity" target="_blank">FlyingAcorn/Analytics-Middleware-for-Unity</a> in `Assets/FlyingAcorn/Analytics` (every package except the SocialAuthentication extension). The packages carry only the core of the middleware. A game that uses the full middleware, with its `Services` folder for analytics providers, must keep its own copy.
- <a href="https://github.com/Cysharp/UniTask" target="_blank">Cysharp/UniTask</a> in `Assets/Plugins/UniTask` (every package except the SocialAuthentication extension).
- <a href="https://github.com/googlesamples/unity-jar-resolver" target="_blank">External Dependency Manager for Unity</a> (EDM4U) in `Assets/ExternalDependencyManager` (the SocialAuthentication extension). Social Authentication on Android needs it to resolve its Android libraries. When you use the EDM4U UPM package instead, untick this folder when importing.

The packages need nothing else: they ship no tests, so the Unity Test Framework is not required, and Advertisement no longer needs RTL Text Mesh Pro (it shipped with Advertisement up to 2.3.0).

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

## Upgrading

Importing a `.unitypackage` adds and overwrites files but never deletes any. When a new version removes or renames a file, the old one stays in your project. Stale scripts can break compilation, because they still use types the new version no longer has, and stale libraries can clash at build time.

To upgrade cleanly:

1. Commit or back up your project.
2. In Unity's Project window, delete the folders the Soil packages installed:
    - `Assets/FlyingAcorn/Soil`
    - `Assets/FlyingAcorn/Analytics`, only when it came from the Soil packages. Keep it when your game uses the full Analytics middleware (it has a `Services` folder), and untick it when importing.
    - `Assets/Plugins/UniTask`, only when it came from the Soil packages
    - `Assets/ExternalDependencyManager` (when you use SocialAuthentication and have this folder rather than the EDM4U UPM package)
    - `Assets/RTLTMPro`, when upgrading Advertisement from 2.3.0 or earlier and your game does not use RTL Text Mesh Pro itself. Newer versions do not ship or need it.
3. Import the new version of every package you use, as in [Step 1](#step-1-download-and-import-the-sdk), unticking the folders you kept. When you use SocialAuthentication, import `Soil-X.Y.Z-SocialAuthentication_Extension.unitypackage` too, since it lives inside `Assets/FlyingAcorn/Soil`.

Your settings live in `Assets/Resources/` (`SDKSettings.asset`, `FA_Build_Settings.asset` and the `ThirdParties` settings), outside these folders, so they are kept. Keep your own files out of these folders too. The new files keep the same asset GUIDs, so your scenes and prefabs still reference any SDK asset that exists in the new version.

The release notes of each version list the files it removed.
