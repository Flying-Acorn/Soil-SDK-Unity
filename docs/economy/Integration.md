# Economy Integration

Before integrating, ensure you have completed the [Installation](../Installation.md).

## Usage

```csharp
// Add currency
await Economy.AddCurrencyAsync("coins", 100);

// Purchase item
await Economy.PurchaseItemAsync(itemId);
```