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

### 4. Modifying Virtual Currencies

All currency modification methods automatically update the cached data in `EconomyPlayerPrefs`.

#### Setting Currency Balance

Set a virtual currency to an absolute balance:

```csharp
try
{
    var updatedCurrency = await Economy.SetVirtualCurrency("gold", 500);
    Debug.Log($"Gold set to: {updatedCurrency.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to set currency: {e.Message}");
}
```

#### Increasing Currency Balance

Add to an existing virtual currency balance:

```csharp
try
{
    var updatedCurrency = await Economy.IncreaseVirtualCurrency("gold", 100);
    Debug.Log($"Gold increased to: {updatedCurrency.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to increase currency: {e.Message}");
}
```

#### Decreasing Currency Balance

Subtract from an existing virtual currency balance:

```csharp
try
{
    var updatedCurrency = await Economy.DecreaseVirtualCurrency("gold", 50);
    Debug.Log($"Gold decreased to: {updatedCurrency.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to decrease currency: {e.Message}");
}
```

### 5. Modifying Inventory Items

All inventory modification methods automatically update the cached data in `EconomyPlayerPrefs`.

#### Setting Item Balance

Set an inventory item to an absolute balance:

```csharp
try
{
    var updatedItem = await Economy.SetInventoryItem("sword", 1);
    Debug.Log($"Sword balance set to: {updatedItem.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to set item: {e.Message}");
}
```

#### Increasing Item Balance

Add to an existing inventory item balance:

```csharp
try
{
    var updatedItem = await Economy.IncreaseInventoryItem("potion", 5);
    Debug.Log($"Potion increased to: {updatedItem.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to increase item: {e.Message}");
}
```

#### Decreasing Item Balance

Subtract from an existing inventory item balance:

```csharp
try
{
    var updatedItem = await Economy.DecreaseInventoryItem("potion", 1);
    Debug.Log($"Potion decreased to: {updatedItem.Balance}");
}
catch (SoilException e)
{
    Debug.LogError($"Failed to decrease item: {e.Message}");
}
```

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.