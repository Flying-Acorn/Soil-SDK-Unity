# Core

## Introduction

The core module orchestrates service lifecycles and provides unified initialization.

## Architecture (High-Level)

Core orchestrates service lifecycles. Each service exposes a facade (e.g., `RemoteConfig`, `Purchasing`, `CloudSave`) with:
- Initialization method (async)
- Data models under `Models/`
- Local persistence via `*PlayerPrefs` where appropriate
- Optional demo handler demonstrating typical flows

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scene: `Assets/FlyingAcorn/Soil/Core/Demo/SoilExample.unity`

## Dependencies
* [Newtonsoft JSON](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
* [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/TextMeshPro/index.html)
* Internal Analytics
* <a href="https://github.com/Cysharp/UniTask" target="_blank">Cysharp/UniTask</a>
