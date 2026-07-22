using System;
using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Purchasing.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Purchasing.Demo
{
    /// <summary>
    /// Reference integration for the Purchasing module.
    ///
    /// Beyond the happy path, this demo makes the two awkward cases visible, because both are
    /// easy to get wrong and neither shows up while testing a single build against a single shop:
    ///
    ///  1. A purchase reported by the server whose sku this build does not know. It is a real,
    ///     paid purchase — bought from a web store, or for an item since disabled or excluded
    ///     from this build. The rewards were already granted server-side, so the game must NOT
    ///     try to resolve them from the local catalog. See <see cref="OnPurchaseSuccessful"/>.
    ///  2. A pending id that no longer resolves at all. Verification reports it as expired and
    ///     the SDK drops it, so the list cannot fill up with dead ids. Reproduce it with the
    ///     "inject unknown id" hook below.
    /// </summary>
    public class PurchasingDemoHandler : MonoBehaviour
    {
        private const int MaxLogLines = 12;

        [SerializeField] private ItemRow itemRowPrefab;
        [SerializeField] private VerticalLayoutGroup shopContainer;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button verifyButton;

        [Header("Optional — leave unassigned to keep the stock scene layout")]
        [Tooltip("Adds a purchase id the server has never heard of, to show how the SDK discards " +
                 "ids that no longer resolve. Wire to any spare Button.")]
        [SerializeField] private Button injectUnknownIdButton;

        private readonly List<ItemRow> _rows = new();
        private readonly List<string> _log = new();
        private string _state = "Starting...";

        private void Start()
        {
            Purchasing.OnPurchasingInitialized += OnPurchasingInitialized;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            Purchasing.OnItemsReceived += FillItems;
            Purchasing.OnItemsFailed += OnItemsFailed;
            Purchasing.OnPurchaseSuccessful += OnPurchaseSuccessful;
            Purchasing.OnPurchaseStart += OnPurchaseStart;
            Purchasing.OnPendingPurchasesDiscovered += OnPendingPurchasesDiscovered;

            verifyButton.onClick.AddListener(VerifyAllPurchases);
            if (injectUnknownIdButton != null)
                injectUnknownIdButton.onClick.AddListener(InjectUnknownPurchaseId);

            if (Purchasing.Ready)
            {
                OnPurchasingInitialized();
                FillItems(Purchasing.AvailableItems);
            }
            else
            {
                SetState("Initializing...");
                Purchasing.Initialize(verifyOnInitialize: true);
            }
        }

        private void OnDestroy()
        {
            Purchasing.OnPurchasingInitialized -= OnPurchasingInitialized;
            SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;
            Purchasing.OnItemsReceived -= FillItems;
            Purchasing.OnItemsFailed -= OnItemsFailed;
            Purchasing.OnPurchaseSuccessful -= OnPurchaseSuccessful;
            Purchasing.OnPurchaseStart -= OnPurchaseStart;
            Purchasing.OnPendingPurchasesDiscovered -= OnPendingPurchasesDiscovered;

            if (verifyButton != null)
                verifyButton.onClick.RemoveListener(VerifyAllPurchases);
            if (injectUnknownIdButton != null)
                injectUnknownIdButton.onClick.RemoveListener(InjectUnknownPurchaseId);
        }

        private void OnApplicationFocus(bool focusStatus)
        {
            if (focusStatus)
                VerifyAllPurchases();
        }

        // --- lifecycle ------------------------------------------------------

        private void OnPurchasingInitialized()
        {
            SetState("Ready");
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            SetState($"SDK initialization failed: {exception.Message}");
        }

        private void OnItemsFailed(SoilException exception)
        {
            SetState($"Could not load items: {exception.Message}");
        }

        // --- purchasing -----------------------------------------------------

        private void OnPurchaseStart(Item item)
        {
            // item is null when the purchase was started for a sku this build has no entry for.
            SetState(item != null ? $"Buying {item.sku}..." : "Buying...");
        }

        private void OnPurchaseSuccessful(Purchase purchase)
        {
            Log(Describe(purchase));

            var item = FindItem(purchase.sku);
            if (item == null)
            {
                // Case 1. Not an error: the purchase is real and the server has already granted
                // its rewards. This build simply has no catalog entry to read them from — the sku
                // may be web-only, disabled, or excluded from this build. Refresh balances from
                // the Economy module instead of granting anything locally.
                Log($"  -> sku '{purchase.sku}' is not in this build's catalog; " +
                    "rewards were granted server-side. Refresh balances rather than granting locally.");
                SetState($"Purchased {purchase.sku} (not in local catalog)");
                return;
            }

            Log($"  -> granting: {DescribeRewards(item)}");
            SetState($"Purchased {purchase.sku}");
        }

        private void OnPendingPurchasesDiscovered(List<Purchase> purchases)
        {
            // Fires once per session, before verification, and only when the server knows about
            // purchases this client did not. Everything here was created outside this client or
            // lost with local storage.
            Log($"Server reported {purchases.Count} unknown purchase(s):");
            foreach (var purchase in purchases)
            {
                var known = FindItem(purchase.sku) != null ? "known sku" : "UNKNOWN sku";
                Log($"  {Shorten(purchase.purchase_id)}  {purchase.sku ?? "(no sku)"}  [{known}]");
            }
        }

        private void VerifyAllPurchases()
        {
            // Deliberately no local emptiness check: the first call of a session also reads the
            // server's pending list, which is how purchases created outside this client are found.
            SetState("Verifying purchases...");
            Purchasing.SafeVerifyAllPurchases();
        }

        /// <summary>
        /// Case 2. Adds an id the server has never issued. Verification returns "not found", the
        /// SDK treats it as expired and drops it, so the pending list self-cleans instead of
        /// retrying a dead id forever. Watch the "Pending" count below before and after verifying.
        /// </summary>
        private void InjectUnknownPurchaseId()
        {
            var fakeId = Guid.NewGuid().ToString();
            PurchasingPlayerPrefs.AddUnverifiedPurchaseId(fakeId);
            Log($"Injected unknown id {Shorten(fakeId)} — verify to watch it get dropped");
            SetState("Unknown id injected");
        }

        // --- items ----------------------------------------------------------

        private void FillItems(List<Item> items)
        {
            foreach (var row in _rows)
            {
                row.OnClick -= BuyItem;
                Destroy(row.gameObject);
            }
            _rows.Clear();

            foreach (var item in items)
            {
                var row = Instantiate(itemRowPrefab, shopContainer.transform);
                row.SetData(item);
                row.OnClick += BuyItem;
                _rows.Add(row);
            }

            SetState($"{items.Count} item(s) available");
        }

        private void BuyItem(string sku)
        {
            SetState($"Buying {sku}...");
            BuyItemAsync(sku);
        }

        private async void BuyItemAsync(string sku)
        {
            // BuyItem throws on a rejected purchase (unknown sku, disabled item, no gateway).
            // Awaiting it is what surfaces that; discarding the task would swallow the failure.
            try
            {
                await Purchasing.BuyItem(sku);
            }
            catch (Exception e)
            {
                Log($"Purchase of {sku} rejected: {e.Message}");
                SetState($"Could not buy {sku}");
            }
        }

        private static Item FindItem(string sku)
        {
            return string.IsNullOrEmpty(sku)
                ? null
                : Purchasing.AvailableItems.FirstOrDefault(item => item.sku == sku);
        }

        // --- display --------------------------------------------------------

        private static string Shorten(string id)
        {
            return string.IsNullOrEmpty(id) ? "(none)" : id.Substring(0, Math.Min(8, id.Length));
        }

        private static string Describe(Purchase purchase)
        {
            var amount = purchase.price_taxed ?? purchase.price;
            var money = amount.HasValue ? $"{amount.Value} {purchase.currency}" : "no price";
            var status = purchase.paid ? "paid" : purchase.expired ? "expired" : "pending";
            var line = $"{Shorten(purchase.purchase_id)}  {purchase.sku ?? "(no sku)"}  {money}  {status}";
            if (purchase.vat_amount.HasValue && purchase.vat_amount.Value > 0)
                line += $"  (vat {purchase.vat_amount.Value}, {purchase.tax_mode})";
            if (!string.IsNullOrEmpty(purchase.transaction_id))
                line += $"  txn {purchase.transaction_id}";
            return line;
        }

        private static string DescribeRewards(Item item)
        {
            var parts = new List<string>();
            if (item.normal_item != null)
                parts.Add($"{item.normal_item.Quantity}x {item.sku}");
            if (item.virtual_currencies != null)
                parts.AddRange(item.virtual_currencies.Select(c => $"{c.Quantity}x currency"));
            if (item.inventory_items != null)
                parts.AddRange(item.inventory_items.Select(i => $"{i.Quantity}x inventory"));
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing declared";
        }

        private void Log(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveRange(0, _log.Count - MaxLogLines);
            Render();
        }

        private void SetState(string state)
        {
            _state = state;
            Render();
        }

        private void Render()
        {
            if (resultText == null)
                return;
            var pending = PurchasingPlayerPrefs.UnverifiedPurchaseIds;
            var header = $"{_state}\nPending: {pending.Count}";
            if (pending.Count > 0)
                header += $" [{string.Join(", ", pending.Select(Shorten))}]";
            resultText.text = _log.Count == 0 ? header : $"{header}\n\n{string.Join("\n", _log)}";
        }
    }
}
