# Purchasing Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Full Purchase Sequence

Follow this complete flow for implementing in-app purchases:

### 1. Setup and Initialization

```csharp
using FlyingAcorn.Soil.Purchasing;

// Subscribe to events first
Purchasing.OnPurchasingInitialized += OnPurchasingReady;
Purchasing.OnItemsReceived += OnItemsLoaded;
Purchasing.OnPurchaseStart += OnPurchaseStarted;
Purchasing.OnPurchaseSuccessful += OnPurchaseCompleted;

// Initialize purchasing
if (!Purchasing.Ready)
{
    Purchasing.Initialize(verifyOnInitialize: true);
}
else
{
    OnPurchasingReady();
}
```

**Note**: Setting `verifyOnInitialize: true` enables automatic verification of any previously unverified purchases when the purchasing system initializes. This ensures that purchases completed while the app was closed are properly validated and items are granted to the player.

**Recommendation**: Your application must be ready to act on `Purchasing.OnPurchaseSuccessful` events immediately after calling `Initialize()`, as automatic verification may trigger these events. To be safer, you can set `verifyOnInitialize: false` and manually call verification methods (like `SafeVerifyAllPurchases()`) only when your app is fully ready to reward users and update game state.

### 2. Load Available Items

Once initialized, the system automatically fetches available items. Handle the response:

```csharp
private void OnPurchasingReady()
{
    Debug.Log("Purchasing system ready!");
    // Items will be received via OnItemsReceived event
}

private void OnItemsLoaded(List<Item> items)
{
    Debug.Log($"Loaded {items.Count} purchasable items");
    foreach (var item in items)
    {
        // Display items in your UI
        CreateItemButton(item);
    }
}
```

### 3. Initiate Purchase

When user selects an item:

```csharp
private async void PurchaseItem(string sku)
{
    // Show loading UI and disable buttons immediately
    ShowPurchaseLoading(true);
    DisablePurchaseButtons();
    
    try
    {
        await Purchasing.BuyItem(sku);
        // Purchase flow initiated - wait for events
    }
    catch (SoilException e)
    {
        Debug.LogError($"Purchase initiation failed: {e.Message}");
        ShowPurchaseLoading(false);
        EnablePurchaseButtons();
    }
}
```

**Note**: After calling `BuyItem()` and before `OnPurchaseStart` event fires, the SDK will attempt to open the payment URL in the device's browser. This will cause the Unity application to **pause** (`Application.isPaused = true`). The app will resume when the user returns from the payment flow.

### 4. Handle Payment Deeplinks (Optional)

**Optional**: Properly setting up deeplink handling may increase purchase conversion by ensuring timely verification when users return from payment.

When the user completes payment in the external browser, they are redirected back to the app via a deep link. The `DeeplinkHandler` automatically processes payment-related deep links and provides the payment parameters.

Subscribe to the deeplink event:

```csharp
using FlyingAcorn.Soil.Purchasing;

// In your initialization
DeeplinkHandler.OnPaymentDeeplinkActivated += OnPaymentDeeplinkReceived;

private void OnPaymentDeeplinkReceived(Dictionary<string, string> parameters)
{
    Debug.Log("Payment deeplink received with parameters:");
    foreach (var param in parameters)
    {
        Debug.Log($"{param.Key}: {param.Value}");
    }
    
    // Process payment result - typically trigger verification
    Purchasing.SafeVerifyAllPurchases();
}
```

**How it works**:
- The `DeeplinkHandler` is a static class that initializes automatically when first accessed.
- It loads the expected payment deeplink URL from `PurchasingPlayerPrefs.GetPurchaseDeeplink()`.
- Registers for deep link events from the `DeepLinkHandler` system.
- When a deep link is activated, it checks if the authority matches the payment deeplink.
- If matched, parses the query string into key-value pairs and invokes `OnPaymentDeeplinkActivated`.

**Note**: Use this event to detect when the user returns from payment and trigger purchase verification.

### 5. Handle Purchase Events

Respond to purchase lifecycle events:

```csharp
private void OnPurchaseStarted(Item item)
{
    Debug.Log($"Purchase started for: {item.sku}");
    // Update UI to show purchase in progress
    UpdatePurchaseStatus("Processing payment...");
}

private void OnPurchaseCompleted(Purchase purchase)
{
    Debug.Log($"Purchase completed: {purchase.sku}");
    // Hide loading, enable buttons, grant item
    ShowPurchaseLoading(false);
    EnablePurchaseButtons();
    GrantPurchasedItem(purchase);
}
```

### 6. Verify Purchases on App Resume

Ensure purchases are verified when app regains focus:

```csharp
private void OnApplicationFocus(bool hasFocus)
{
    if (hasFocus)
    {
        // Verify any pending purchases
        Purchasing.SafeVerifyAllPurchases();
    }
}
```

**Important Notes on Verification**:
- `OnPurchaseSuccessful` events will **not fire** unless you call verification methods
- **When to call**: 
  - When the application unpauses and the user returns to the game (after payment)
  - At the beginning of app/shop initialization to catch any missed verifications
- This ensures completed purchases are properly validated and rewards are granted

### 7. Grant Purchase Rewards

When `OnPurchaseSuccessful` fires (after verification), grant the purchased items to the player by matching the purchase SKU with the item data:

```csharp
private void OnPurchaseCompleted(Purchase purchase)
{
    Debug.Log($"Purchase completed: {purchase.sku}");
    
    // Find the purchased item by SKU
    var purchasedItem = Purchasing.AvailableItems.FirstOrDefault(item => item.sku == purchase.sku);
    
    if (purchasedItem != null)
    {
        // Grant inventory items
        foreach (var inventoryItem in purchasedItem.inventory_items)
        {
            GrantInventoryItem(inventoryItem);
        }
        
        // Grant virtual currencies
        foreach (var virtualCurrency in purchasedItem.virtual_currencies)
        {
            AddVirtualCurrency(virtualCurrency);
        }
        
        // Handle normal item if present
        if (purchasedItem.normal_item != null)
        {
            GrantNormalItem(purchasedItem.normal_item);
        }
        
        // Update UI and save progress
        UpdatePlayerInventory();
        SaveGameProgress();
    }
    else
    {
        Debug.LogError($"Purchased item not found in available items: {purchase.sku}");
    }
    
    // Hide loading and re-enable buttons
    ShowPurchaseLoading(false);
    EnablePurchaseButtons();
}
```

**Note**: Always verify the purchase details (SKU, amount, etc.) before granting rewards to prevent exploitation. The item data contains the server-defined rewards that should be granted for each SKU.

## Additional Features

### Manual Verification

Verify specific purchases manually:

```csharp
// Single purchase
await Purchasing.VerifyPurchase("purchase_id_123");

// Multiple purchases
var ids = new List<string> { "id1", "id2" };
await Purchasing.BatchVerifyPurchases(ids);
```

### Opening Invoices

Show payment details for completed purchases:

```csharp
Purchasing.OpenInvoice("purchase_id_123");
```

### Deinitialization

Clean up when no longer needed:

```csharp
Purchasing.DeInitialize();
```

## Advanced Functions

### RollbackUnpaidPurchases()

**Purpose**: Removes unpaid purchases from local storage to prevent accumulation of failed transactions.

**Usage**: Call this periodically to clean up expired or failed purchase attempts:

```csharp
Purchasing.RollbackUnpaidPurchases();
```

**When to use**: On app startup or periodically to maintain clean purchase state. This doesn't affect completed purchases.

## Demo Scene

See the [Purchasing Demo](../README.md#demo-scenes) (`SoilPurchasingExample.unity`) for a complete working example of the full purchase sequence.

## API Reference

- `Purchasing.Initialize(bool verifyOnInitialize = true)`
- `Purchasing.Ready` (property)
- `Purchasing.AvailableItems` (property)
- `Purchasing.BuyItem(string sku)`
- `Purchasing.VerifyPurchase(string purchaseId)`
- `Purchasing.BatchVerifyPurchases(List<string> purchaseIds)`
- `Purchasing.SafeVerifyAllPurchases()`
- `Purchasing.OpenInvoice(string purchaseId)`
- `Purchasing.RollbackUnpaidPurchases()`
- `Purchasing.DeInitialize()`

## Other Documentations

See the [Services overview](../README.md#services) for information on other available modules.