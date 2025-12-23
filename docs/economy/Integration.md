# Economy Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

**Service Enablement**: Ensure the Economy service is enabled for your account. Reach out to your Soil contact to enable the service for you.

## Full Economy Sequence

Follow this complete flow for implementing economy features:

### 1. Setup and Initialization

Subscribe to events and initialize the Economy service. This will automatically initialize `SoilServices` if needed and fetch the latest economy summary.

```csharp
using FlyingAcorn.Soil.Economy;

// Subscribe to events
Economy.OnEconomyInitialized += OnEconomyReady;
Economy.OnEconomyInitializationFailed += OnEconomyFailed;

// Initialize Economy
Economy.Initialize();

private void OnEconomyReady()
{
    Debug.Log("Economy system ready!");
    // You can now access cached data or call economy methods
}

private void OnEconomyFailed(SoilException e)
{
    Debug.LogError($"Economy initialization failed: {e.Message}");
}
```

### 2. Get Economy Summary

While `Initialize()` automatically fetches the summary and caches it, you can manually fetch the latest state at any time using `GetSummary()`:

```csharp
private async void FetchLatestEconomy()
{
    try
    {
        // Fetches the latest virtual currencies and inventory items
        var summary = await Economy.GetSummary();
        
        Debug.Log($"Fetched {summary.virtual_currencies.Count} currencies and {summary.inventory_items.Count} items.");
        
        // Note: GetSummary() returns the data but does NOT automatically update EconomyPlayerPrefs.
        // The Initialize() method handles the initial caching.
    }
    catch (SoilException e)
    {
        Debug.LogError($"Failed to fetch economy summary: {e.Message}");
    }
}
```

### 3. Accessing Cached Data

The Economy module provides `EconomyPlayerPrefs` for quick, synchronous access to the last fetched data. This is useful for updating UI without waiting for network requests.

```csharp
using FlyingAcorn.Soil.Economy;

// Get all virtual currencies
var currencies = EconomyPlayerPrefs.VirtualCurrencies;
foreach (var currency in currencies)
{
    Debug.Log($"{currency.Identifier}: {currency.Balance}");
}

// Get all inventory items
var items = EconomyPlayerPrefs.InventoryItems;
foreach (var item in items)
{
    Debug.Log($"{item.Identifier}: {item.Balance}");
}
```

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.