# Installation

## Step 1: Download and Import the SDK

Download and import the latest `.unitypackage` from [here](https://github.com/Flying-Acorn/Soil-SDK-Unity/releases/latest).

## Step 2: Ensure Dependencies

Ensure the following are present in your project:
- [Newtonsoft JSON](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
- [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html)
- [External Dependency Manager for Google integrations](https://developers.google.com/unity/archive#external_dependency_manager_for_unity)
- [FlyingAcorn/Analytics-Middleware-for-Unity](https://github.com/Flying-Acorn/Analytics-Middleware-for-Unity)
- [Cysharp/UniTask](https://github.com/Cysharp/UniTask)

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

## Installation Complete

Your Soil SDK is now installed and configured! You can start integrating services. We recommend beginning with the [Core module](./core/Integration.md) for basic setup, then add other modules as needed. See the [Services overview](./README.md#services) for all available integrations.
