using TMPro;
using UnityEngine;
using FlyingAcorn.Soil.Core.Data;
using System.Collections.Generic;
using UnityEngine.UI;
using JetBrains.Annotations;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Economy.Models.Responses;

namespace FlyingAcorn.Soil.Economy.Demo
{
    public class UserEconomyDemoHandler : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusTitleText;
        [SerializeField] private DemoEconomyItem DemoEconomyItemPrefab;
        [SerializeField] private Transform DemoInventoryItemContainer;
        [SerializeField] private Transform DemoVirtualCurrencyContainer;
        [SerializeField] private Button RetryInitButton;
        [SerializeField] private Button SetButton;
        [SerializeField] private Button IncreaseButton;
        [SerializeField] private Button DecreaseButton;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private TMP_Dropdown EconomyItemToChangeDropdown;
        private List<DemoEconomyItem> demoInventoryItems = new List<DemoEconomyItem>();
        private List<DemoEconomyItem> demoVirtualCurrencies = new List<DemoEconomyItem>();

        private void Start()
        {
            statusTitleText.text = "Initializing Economy...";

            Economy.OnEconomyInitialized += OnEconomyInitializationSuccess;
            Economy.OnEconomyInitializationFailed += OnEconomyInitializationFailed;

            RetryInitButton.interactable = false;
            RetryInitButton.onClick.AddListener(OnRetryInitButtonClick);

            SetButtonsInteractable(false);

            Economy.Initialize();

            SetButton.onClick.AddListener(OnSetButtonClick);
            IncreaseButton.onClick.AddListener(OnIncreaseButtonClick);
            DecreaseButton.onClick.AddListener(OnDecreaseButtonClick);
        }

        private void OnRetryInitButtonClick()
        {
            statusTitleText.text = "Retrying Economy Initialization...";
            RetryInitButton.interactable = false;
            Economy.Initialize();
        }

        private void OnDestroy()
        {
            Economy.OnEconomyInitialized -= OnEconomyInitializationSuccess;
            Economy.OnEconomyInitializationFailed -= OnEconomyInitializationFailed;
        }

        private void OnEconomyInitializationSuccess()
        {
            statusTitleText.text = "Economy Ready";
            RetryInitButton.interactable = false;
            SetButtonsInteractable(true);
            InitializeEconomyItems();
        }

        private void InitializeEconomyItems()
        {
            foreach (var item in demoVirtualCurrencies)
            {
                Destroy(item.gameObject);
            }
            demoVirtualCurrencies.Clear();
            var virtualCurrencies = EconomyPlayerPrefs.VirtualCurrencies;
            foreach (var currency in virtualCurrencies)
            {
                var demoCurrencyItem = Instantiate(DemoEconomyItemPrefab, DemoVirtualCurrencyContainer);
                demoCurrencyItem.Setup(currency.Identifier, currency.Balance);
                demoVirtualCurrencies.Add(demoCurrencyItem);
            }

            foreach (var item in demoInventoryItems)
            {
                Destroy(item.gameObject);
            }
            demoInventoryItems.Clear();
            var inventoryItems = EconomyPlayerPrefs.InventoryItems;
            foreach (var item in inventoryItems)
            {
                var demoInventoryItem = Instantiate(DemoEconomyItemPrefab, DemoInventoryItemContainer);
                demoInventoryItem.Setup(item.Identifier, item.Balance);
                demoInventoryItems.Add(demoInventoryItem);
            }

            List<string> newIdentifiers = new List<string>();
            foreach (var demoItem in demoInventoryItems)
            {
                newIdentifiers.Add(demoItem.Identifier);
            }
            foreach (var demoCurrency in demoVirtualCurrencies)
            {
                newIdentifiers.Add(demoCurrency.Identifier);
            }

            bool needsUpdate = false;
            if (EconomyItemToChangeDropdown.options.Count != newIdentifiers.Count)
            {
                needsUpdate = true;
            }
            else
            {
                for (int i = 0; i < newIdentifiers.Count; i++)
                {
                    if (EconomyItemToChangeDropdown.options[i].text != newIdentifiers[i])
                    {
                        needsUpdate = true;
                        break;
                    }
                }
            }

            if (needsUpdate)
            {
                EconomyItemToChangeDropdown.ClearOptions();
                EconomyItemToChangeDropdown.AddOptions(newIdentifiers);
            }
        }

        private void OnEconomyInitializationFailed(SoilException exception)
        {
            statusTitleText.text = $"Economy Initialization Failed: {exception.Message}";
            RetryInitButton.interactable = true;
            SetButtonsInteractable(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            SetButton.interactable = interactable;
            IncreaseButton.interactable = interactable;
            DecreaseButton.interactable = interactable;
        }

        private bool ValidateAmountInput()
        {
            if (int.TryParse(amountInput.text, out int value))
            {
                return value >= 0;
            }
            return false;
        }

        private bool IsInventoryItem(string identifier)
        {
            return demoInventoryItems.Exists(item => item.Identifier == identifier);
        }

        private bool IsVirtualCurrency(string identifier)
        {
            return demoVirtualCurrencies.Exists(item => item.Identifier == identifier);
        }

        [CanBeNull]
        private string GetSelectedItemType(string identifier)
        {
            if (IsInventoryItem(identifier))
            {
                return "InventoryItem";
            }
            else if (IsVirtualCurrency(identifier))
            {
                return "VirtualCurrency";
            }
            return null;
        }

        private async void OnSetButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            
            if (!ValidateAmountInput())
            {
                statusTitleText.text = "Invalid amount input, must be a non-negative integer";
                return;
            }
            string itemType = GetSelectedItemType(selectedIdentifier);
            if (itemType == null)
            {
                statusTitleText.text = "Selected item type not found";
                return;
            }
            try
            {
                SetButtonsInteractable(false);
                statusTitleText.text = $"Setting {selectedIdentifier} to {amountInput.text}...";
                await DoButtonAction(selectedIdentifier, itemType, "Set", amountInput.text);
                statusTitleText.text = $"{selectedIdentifier} updated successfully!";
            }
            catch (EconomyException ee)
            {
                statusTitleText.text = $"Economy Error: {ee.Message} (Code: {ee.EconomyErrorCode})";
            }
            catch (SoilException se)
            {
                statusTitleText.text = $"Soil Error: {se.Message} (Code: {se.ErrorCode})";
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Unexpected Error: {e.Message}";
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void OnIncreaseButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            
            if (!ValidateAmountInput())
            {
                statusTitleText.text = "Invalid amount input, must be a non-negative integer";
                return;
            }
            string itemType = GetSelectedItemType(selectedIdentifier);
            if (itemType == null)
            {
                statusTitleText.text = "Selected item type not found";
                return;
            }
            try
            {
                SetButtonsInteractable(false);
                statusTitleText.text = $"Increasing {selectedIdentifier} by {amountInput.text}...";
                await DoButtonAction(selectedIdentifier, itemType, "Increase", amountInput.text);
                statusTitleText.text = $"{selectedIdentifier} increased successfully!";
            }
            catch (EconomyException ee)
            {
                statusTitleText.text = $"Economy Error: {ee.Message} (Code: {ee.EconomyErrorCode})";
            }
            catch (SoilException se)
            {
                statusTitleText.text = $"Soil Error: {se.Message} (Code: {se.ErrorCode})";
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Unexpected Error: {e.Message}";
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async void OnDecreaseButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            
            if (!ValidateAmountInput())
            {
                statusTitleText.text = "Invalid amount input, must be a non-negative integer";
                return;
            }
            string itemType = GetSelectedItemType(selectedIdentifier);
            if (itemType == null)
            {
                statusTitleText.text = "Selected item type not found";
                return;
            }
            try
            {
                SetButtonsInteractable(false);
                statusTitleText.text = $"Decreasing {selectedIdentifier} by {amountInput.text}...";
                await DoButtonAction(selectedIdentifier, itemType, "Decrease", amountInput.text);
                statusTitleText.text = $"{selectedIdentifier} decreased successfully!";
            }
            catch (EconomyException ee)
            {
                statusTitleText.text = $"Economy Error: {ee.Message} (Code: {ee.EconomyErrorCode})";
            }
            catch (SoilException se)
            {
                statusTitleText.text = $"Soil Error: {se.Message} (Code: {se.ErrorCode})";
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Unexpected Error: {e.Message}";
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private async UniTask DoButtonAction(string identifier, string itemType, string actionType, string amountText)
        {
            if (!ValidateAmountInput())
            {
                throw new System.Exception("Invalid amount input, must be a non-negative integer");
            }
            int amount = int.Parse(amountText);
            if (itemType == "InventoryItem")
            {
                if (actionType == "Set")
                {
                    await Economy.SetInventoryItem(identifier, amount);
                }
                else if (actionType == "Increase")
                {
                    await Economy.IncreaseInventoryItem(identifier, amount);
                }
                else if (actionType == "Decrease")
                {
                    await Economy.DecreaseInventoryItem(identifier, amount);
                }
                else
                {
                    throw new System.Exception("Invalid action type");
                }
            }
            else if (itemType == "VirtualCurrency")
            {
                if (actionType == "Set")
                {
                    await Economy.SetVirtualCurrency(identifier, amount);
                }
                else if (actionType == "Increase")
                {
                    await Economy.IncreaseVirtualCurrency(identifier, amount);
                }
                else if (actionType == "Decrease")
                {
                    await Economy.DecreaseVirtualCurrency(identifier, amount);
                }
                else
                {
                    throw new System.Exception("Invalid action type");
                }
            }
            else
            {
                throw new System.Exception("Invalid item type");
            }

            InitializeEconomyItems();
        }
    }
}