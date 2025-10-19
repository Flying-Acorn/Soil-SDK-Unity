# Remote Config Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Usage

```csharp
// Fetch config
var config = await RemoteConfig.FetchAsync();

// Get value
var value = config.GetString("key");
```

## A/B Testing

To surface experiment cohort ids in analytics / requests, open `UserInfo.cs` and uncomment:
```csharp
{ $"{KeysPrefix}cohort_id", ABTestingPlayerPrefs.GetLastExperimentId() }
```

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.