# Cloud Save Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Prerequisites

CloudSave requires the Core SDK to be initialized first. Initialize SoilServices before using any CloudSave operations.

### Initializing SoilServices

```csharp
using FlyingAcorn.Soil.Core;

if (!SoilServices.Ready)
{
    SoilServices.OnServicesReady += OnServicesReady;
    SoilServices.InitializeAsync();
}

private void OnServicesReady()
{
    // CloudSave is now ready to use
    Debug.Log("SoilServices initialized, CloudSave ready!");
}
```

## Usage

### Saving Data

Save data to the cloud with a key-value pair. Handle exceptions for success/failure:

```csharp
using FlyingAcorn.Soil.CloudSave;

try
{
    await CloudSave.SaveAsync("playerData", myData);
    Debug.Log("Data saved successfully!");
}
catch (Exception e)
{
    Debug.LogError($"Save failed: {e.Message}");
}
```

You can also save public data (visible to other users):

```csharp
try
{
    await CloudSave.SaveAsync("publicScore", score, isPublic: true);
    Debug.Log("Public data saved successfully!");
}
catch (Exception e)
{
    Debug.LogError($"Public save failed: {e.Message}");
}
```

**Note**: `isPublic` determines if other users can access your saved data. Public data can be loaded by anyone, while private data is only accessible by the owner.

### Loading Data

Load data by key. Handle cases where data doesn't exist or loading fails:

```csharp
try
{
    var saveModel = await CloudSave.LoadAsync("playerData");
    string data = saveModel.value;
    Debug.Log("Data loaded successfully!");
}
catch (Exception e)
{
    Debug.LogError($"Load failed: {e.Message}");
    // Fallback to default or cached data
}
```

Load data from another user (only public data can be accessed):

```csharp
try
{
    var otherUserData = await CloudSave.LoadAsync("publicScore", otherUserID: "user123");
    Debug.Log("Other user's data loaded successfully!");
}
catch (Exception e)
{
    Debug.LogError($"Failed to load other user's data: {e.Message}");
}
```

## Error Handling

CloudSave operations can throw custom exceptions. Handle them appropriately:

### Common Exceptions

- **`SoilException`** with error codes:
  - `InvalidRequest`: Invalid key/value or request parameters
  - `NotReady`: SoilServices not initialized
  - `TransportError`: Network or server errors
  - `InvalidResponse`: Unexpected server response

- **`SoilNotFoundException`**: Thrown when trying to load a key that doesn't exist

### Example Error Handling

```csharp
try
{
    await CloudSave.SaveAsync("playerData", myData);
}
catch (SoilException e)
{
    switch (e.ErrorCode)
    {
        case SoilExceptionErrorCode.InvalidRequest:
            Debug.LogError("Invalid save request");
            break;
        case SoilExceptionErrorCode.NotReady:
            Debug.LogError("SDK not initialized");
            break;
        default:
            Debug.LogError($"Save failed: {e.Message}");
            break;
    }
}

try
{
    var data = await CloudSave.LoadAsync("missingKey");
}
catch (SoilNotFoundException)
{
    Debug.Log("Key not found, using defaults");
}
catch (SoilException e)
{
    Debug.LogError($"Load failed: {e.Message}");
}
```

## Local Caching

Data is automatically cached locally using `CloudSavePlayerPrefs`. Access cached data:

```csharp
// Get all saved keys
var saves = CloudSavePlayerPrefs.Saves;

// Load cached value
string cachedValue = CloudSavePlayerPrefs.Load("key");
```

## Demo Scene

See the [Cloud Save Demo](../README.md#demo-scenes) (`SoilCloudSaveExample.unity`) for a complete working example of saving, loading, and managing cloud data.

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.