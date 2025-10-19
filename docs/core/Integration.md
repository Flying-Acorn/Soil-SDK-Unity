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
    private void Awake()
    {
        SoilServices.OnServicesReady += OnServicesReady;
        SoilServices.OnInitializationFailed += OnInitializationFailed;
    }

    private void Start()
    {
        SoilServices.InitializeAsync();
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

## Demo Scene

See the [Core Demo](./../README.md#demo-scenes) (`SoilExample.unity`) for a complete working example of SDK initialization and basic usage.

## Advanced Integration

For detailed event handling, status checking, error management, and advanced usage patterns, see the [Advanced Integration Guide](AdvancedIntegration.md).