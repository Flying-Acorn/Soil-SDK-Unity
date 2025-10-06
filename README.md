<div align="center">

# Soil SDK for Unity

Modular live game services (Auth, Economy, Remote Config, Ads, Cloud Save, Leaderboards, Social, Analytics) unified under a lightweight, async-first (UniTask) architecture.

Unity Version: 2022.3.62f1 (LTS)

</div>

## Table of Contents
1. Overview
2. Features
3. Architecture
4. Installation
5. Demo Scenes
6. Services & Dependencies
7. Advertisement Video Encoding Guide
8. Development Notes (A/B Testing etc.)
9. Contributing
10. License

## 1. Overview
Soil SDK provides a cohesive layer over Unity and third‑party backend/game services to speed up prototyping and production. Each service is optional and can be enabled independently while sharing a unified user context and analytics/event pipeline.

## 2. Features
- Unified initialization pipeline (single entry point)
- Anonymous, first‑party JWT & third‑party (Google / Apple) authentication
- Remote Config + built‑in lightweight A/B testing helper
- Embedded Economy & Purchasing integration hooks
- Cloud Save (local caching + sync pattern)
- Leaderboards (read/update + cached state)
- Socialization (friends model placeholder layer)
- Advertisement management (load/show events + example)
- Analytics middleware compatible event dispatch
- Async UniTask-based APIs for all service calls

## 3. Architecture (High-Level)
Core orchestrates service lifecycles. Each service exposes a facade (e.g., `RemoteConfig`, `Purchasing`, `CloudSave`) with:
- Initialization method (async)
- Data models under `Models/`
- Local persistence via `*PlayerPrefs` where appropriate
- Optional demo handler demonstrating typical flows

## 4. Installation
Choose one of the following:
1. Clone the repository into your Unity `Packages` or `Assets` folder.
2. Add as a Git dependency in `manifest.json` (if hosted on Git later).

Ensure the following Unity packages (or equivalents) are present:
- Newtonsoft JSON
- TextMeshPro
- (Optional) External Dependency Manager for Google integrations

Third‑party submodules / embedded code provide Google & Apple auth helpers.

## 5. Demo Scenes
All demo Unity scenes are located under `Assets/FlyingAcorn/Soil/**/Demo/`.

| Service        | Scene Path                                                                 |
|----------------|-----------------------------------------------------------------------------|
| Core / Auth    | `Assets/FlyingAcorn/Soil/Core/Demo/SoilExample.unity`                      |
| Purchasing     | `Assets/FlyingAcorn/Soil/Purchasing/Demo/SoilPurchasingExample.unity`      |
| Remote Config  | `Assets/FlyingAcorn/Soil/RemoteConfig/Demo/SoilRemoteConfigExample.unity`  |
| Advertisement  | `Assets/FlyingAcorn/Soil/Advertisement/Demo/SoilAdvertisementExample.unity`|
| Cloud Save     | `Assets/FlyingAcorn/Soil/CloudSave/Demo/SoilCloudSaveExample.unity`        |
| Leaderboard    | `Assets/FlyingAcorn/Soil/Leaderboard/Demo/SoilLeaderboardExample.unity`    |
| Socialization  | `Assets/FlyingAcorn/Soil/Socialization/Demo/SoilSocializationExample.unity`|
| Analytics      | `Assets/FlyingAcorn/Analytics/Demo/DemoInitCall.unity`                     |
| Scene Switcher | `Assets/FlyingAcorn/Soil/Demo/SoilSceneSwitcher.unity`                     |

> Tip: Start with the Scene Switcher to jump across feature demos during evaluation.

## 6. Services & Dependencies

### Core
Depends on:
* [newtonsoft-json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
* [FlyingAcorn/Analytics-Middleware-for-Unity](https://github.com/Flying-Acorn/Analytics-Middleware-for-Unity)
* [Cysharp/UniTask](https://github.com/Cysharp/UniTask)
Demo: `SoilExample.unity`

### Authentication
First‑party & Anonymous:
* [jwt-dotnet/jwt](https://github.com/jwt-dotnet/jwt)

Third‑party:
* [gabriel01913/UnityGoogleCredentials](https://github.com/gabriel01913/UnityGoogleCredentials)
  * [com.google.external-dependency-manager](https://github.com/googlesamples/unity-jar-resolver)
* [cdmvision/authentication-unity](https://github.com/cdmvision/authentication-unity)
* [lupidan/apple-signin-unity](https://github.com/lupidan/apple-signin-unity)
Demo: Reuse `SoilExample.unity` (Core) for auth flows.

### Purchasing & Economy
* Embedded Soil Economy layer
* Integrates with Remote Config for price/offer experimentation
Demo: `SoilPurchasingExample.unity`

### Remote Config / A/B Testing
Provides runtime tunables + experiment cohort support.
Demo: `SoilRemoteConfigExample.unity`

### Advertisement
Relies on (optional) localized text (RTL support) via:
* [RTL Text Mesh Pro](https://github.com/pnarimani/RTLTMPro/)
Demo: `SoilAdvertisementExample.unity`

### Cloud Save
Local cache + push/pull sync pattern.
Demo: `SoilCloudSaveExample.unity`

### Leaderboard
Simple submission & retrieval facade.
Demo: `SoilLeaderboardExample.unity`

### Socialization
Friend list placeholder layer + row prefab example.
Demo: `SoilSocializationExample.unity`

### Analytics
Middleware aggregator; forward gameplay + economy + ad events.
Demo: `DemoInitCall.unity`

## 7. Advertisement Video Encoding Guide
Use these FFmpeg commands to optimize video ads for Unity `VideoPlayer` with consistent decode performance and sync stability.

Universal Command (iOS & Android, preserves dimensions):
```bash
ffmpeg -i input_video.mov \
  -c:v libx264 \
  -profile:v main \
  -crf 25 \
  -preset medium \
  -pix_fmt yuv420p \
  -c:a aac \
  -b:a 128k \
  -movflags +faststart \
  -fs 15000000 \
  -y output_video_ad.mp4
```

One-Liner:
```bash
ffmpeg -i "input_video.mov" -c:v libx264 -crf 25 -c:a aac -b:a 128k -movflags +faststart -fs 15000000 -y "output_video_ad.mp4"
```

Specifications:
- Max file size: 15MB (matches cache limit)
- RGB565 friendly (reduced bandwidth on low-end devices)
- Sync modes: DSPTime (iOS) / GameTime (Android)
- Preserves original resolution
- Codec: H.264 video + AAC audio for broad compatibility

## 8. Development Notes
### A/B Testing
To surface experiment cohort ids in analytics / requests, open `UserInfo.cs` and uncomment:
```csharp
{ $"{KeysPrefix}cohort_id", ABTestingPlayerPrefs.GetLastExperimentId() }
```

## 9. Contributing
PRs and issue reports welcome. Please include:
- Context (feature / bug / optimization)
- Repro steps (if bug)
- Platform / Unity version

Guidelines (proposed):
- Follow existing folder & namespace conventions (`FlyingAcorn.Soil.<Service>`)
- Keep async APIs UniTask-based
- Provide a demo usage snippet or extend an existing demo when adding a new capability

## 10. License
This repository aggregates multiple third‑party components under their respective licenses (see nested LICENSE files). Add a top-level license file before public distribution if not already defined.

---
Feel free to open an issue to request additional integrations or clarifications.