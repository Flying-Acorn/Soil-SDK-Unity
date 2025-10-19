# Cloud Save Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Usage

```csharp
// Save data
await CloudSave.SaveAsync("playerData", myData);

// Load data
var data = await CloudSave.LoadAsync("playerData");
```