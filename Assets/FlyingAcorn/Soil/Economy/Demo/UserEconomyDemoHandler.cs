using TMPro;
using UnityEngine;
using FlyingAcorn.Soil.Core.Data;
using System.Collections.Generic;
using UnityEngine.UI;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Economy.Demo
{
    public class UserEconomyDemoHandler : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusTitleText;
        [SerializeField] private DemoEconomyItem DemoEconomyItemPrefab;
        [SerializeField] private Transform DemoInventoryItemContainer;
        [SerializeField] private Transform DemoVirtualCurrencyContainer;
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

            Economy.Initialize();

            SetButton.onClick.AddListener(OnSetButtonClick);
            IncreaseButton.onClick.AddListener(OnIncreaseButtonClick);
            DecreaseButton.onClick.AddListener(OnDecreaseButtonClick);
        }

        private void OnDestroy()
        {
            Economy.OnEconomyInitialized -= OnEconomyInitializationSuccess;
            Economy.OnEconomyInitializationFailed -= OnEconomyInitializationFailed;
        }

        private void OnEconomyInitializationSuccess()
        {
            statusTitleText.text = "Economy Ready";
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

            EconomyItemToChangeDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            foreach (var demoItem in demoInventoryItems)
            {
                options.Add(new TMP_Dropdown.OptionData(demoItem.Identifier));
            }
            foreach (var demoCurrency in demoVirtualCurrencies)
            {
                options.Add(new TMP_Dropdown.OptionData(demoCurrency.Identifier));
            }
            EconomyItemToChangeDropdown.AddOptions(options);
        }

        private void OnEconomyInitializationFailed(SoilException exception)
        {
            statusTitleText.text = "Economy Initialization Failed";
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

        private void OnSetButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            statusTitleText.text = $"Set {selectedIdentifier} to {amountInput.text}";
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
                DoButtonAction(itemType, "Set", amountInput.text);
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Error: {e.Message}";
            }
        }

        private void OnIncreaseButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            statusTitleText.text = $"Increased {selectedIdentifier} by {amountInput.text}";
            string itemType = GetSelectedItemType(selectedIdentifier);
            if (itemType == null)
            {
                statusTitleText.text = "Selected item type not found";
                return;
            }
            try
            {
                DoButtonAction(itemType, "Increase", amountInput.text);
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Error: {e.Message}";
            }
        }

        private void OnDecreaseButtonClick()
        {
            int selectedIndex = EconomyItemToChangeDropdown.value;
            string selectedIdentifier = EconomyItemToChangeDropdown.options[selectedIndex].text;
            statusTitleText.text = $"Decreased {selectedIdentifier} by {amountInput.text}";
            string itemType = GetSelectedItemType(selectedIdentifier);
            if (itemType == null)
            {
                statusTitleText.text = "Selected item type not found";
                return;
            }
            try
            {
                DoButtonAction(itemType, "Decrease", amountInput.text);
            }
            catch (System.Exception e)
            {
                statusTitleText.text = $"Error: {e.Message}";
            }
        }

        private void DoButtonAction(string itemType, string actionType, string amountText)
        {
            if (!ValidateAmountInput())
            {
                throw new System.Exception("Invalid amount input, must be a non-negative integer");
            }
            if (itemType == "InventoryItem")
            {
                int amount = int.Parse(amountText);
                if (actionType == "Set")
                {
                    throw new System.NotImplementedException();
                }
                else if (actionType == "Increase")
                {
                    throw new System.NotImplementedException();
                }
                else if (actionType == "Decrease")
                {
                    throw new System.NotImplementedException();
                }
                else
                {
                    throw new System.Exception("Invalid action type");
                }
            }
            else if (itemType == "VirtualCurrency")
            {
                int amount = int.Parse(amountText);
                if (actionType == "Set")
                {
                    throw new System.NotImplementedException();
                }
                else if (actionType == "Increase")
                {
                    throw new System.NotImplementedException();
                }
                else if (actionType == "Decrease")
                {
                    throw new System.NotImplementedException();
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
        }
    }
}