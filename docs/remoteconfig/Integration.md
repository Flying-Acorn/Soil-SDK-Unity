# Remote Config Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Remote Config service is enabled for your account. Reach out to your Soil contact to enable the service for you.

## Full Remote Config Sequence

Follow this complete flow for implementing remote configuration:

### 1. Setup and Initialization

Subscribe to events and initialize the SDK if not already done:

```csharp
using FlyingAcorn.Soil.RemoteConfig;

// Subscribe to events
RemoteConfig.OnSuccessfulFetch += OnConfigFetched;
RemoteConfig.OnServerAnswer += OnFetchResult;

// Initialize SDK if needed
if (!SoilServices.Ready)
{
    SoilServices.OnServicesReady += OnSDKReady;
    SoilServices.InitializeAsync();
}
else
{
    OnSDKReady();
}

private void OnSDKReady()
{
    Debug.Log("SDK ready - can now fetch remote config");
}
```

### 2. Fetch Remote Configuration

Fetch configuration data from the server:

```csharp
private void FetchRemoteConfig()
{
    // Optional: Pass extra properties for server-side logic
    var extraProps = new Dictionary<string, object>
    {
        { "level", currentPlayerLevel },
    };
    
    RemoteConfig.FetchConfig(extraProps);
    // Note: FetchConfig is fire-and-forget, results come via events
}
```

**Note**: `FetchConfig()` is asynchronous and returns immediately. Results are delivered through events.

### 3. Handle Fetch Results

Respond to fetch success/failure:

```csharp
private void OnConfigFetched()
{
    var userConfigs = RemoteConfig.UserDefinedConfigs;
    Debug.Log($"Received config: {userConfigs}");
}

private void OnFetchResult(bool success)
{
    if (success)
    {
        Debug.Log("Remote config fetched successfully");
        
        // At this point, all 4 configuration properties are populated:
        // - UserDefinedConfigs: Available for game logic
        // - ExchangeRates: Ready for currency conversions
        // - UserInfo: Updated with latest user data
        // - PurchasingSettings: Configured for purchase flows
        
        ApplyConfiguration();
    }
    else
    {
        Debug.LogError("Failed to fetch remote config");
        // Fall back to cached/default values
        LoadFallbackConfig();
    }
}
```

### 4. Access Configuration Data

Access different types of configuration:

```csharp
// Check if config is ready
if (RemoteConfig.IsFetchedAndReady)
{
    // Access user-defined configs
    var userConfigs = RemoteConfig.UserDefinedConfigs;
    if (userConfigs != null)
    {
        var gameDifficulty = userConfigs["game_difficulty"]?.ToString();
        var maxLevel = (int?)userConfigs["max_level"];
    }
    
    // Access exchange rates
    var rates = RemoteConfig.ExchangeRates;
    if (rates != null)
    {
        var usdToEur = (decimal?)rates["USD_EUR"];
    }
    
    // Access purchasing settings
    var purchaseSettings = RemoteConfig.PurchasingSettings;
    // Note: This is internal, use purchasing module for purchase logic
}
else
{
    Debug.Log("Remote config not ready yet");
}
```

### 5. Handle A/B Testing

If using A/B testing, ensure cohort IDs are included in analytics:

```csharp
// In your analytics setup (e.g., UserInfo.cs)
{ $"{KeysPrefix}cohort_id", ABTestingPlayerPrefs.GetLastExperimentId() }
```

**Note**: A/B testing experiments are automatically initialized when config is fetched successfully.

## Additional Features

### Check Fetch Status

```csharp
// Check if currently fetching
if (RemoteConfig.IsFetching)
{
    Debug.Log("Config fetch in progress...");
}

// Check if ready
if (RemoteConfig.IsFetchedAndReady)
{
    // Safe to access config data
}
```

### Manual Re-fetch

```csharp
// Force refresh config
RemoteConfig.FetchConfig();
```

## Demo Scene

See the [Remote Config Demo](../README.md#demo-scenes) (`SoilRemoteConfigExample.unity`) for a complete working example.

## API Reference

- `RemoteConfig.FetchConfig(Dictionary<string, object> extraProperties = null)`
- `RemoteConfig.IsFetchedAndReady` (property)
- `RemoteConfig.IsFetching` (property)
- `RemoteConfig.UserDefinedConfigs` (property)
- `RemoteConfig.ExchangeRates` (property)

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.