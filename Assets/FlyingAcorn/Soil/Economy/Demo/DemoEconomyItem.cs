using TMPro;
using UnityEngine;

namespace FlyingAcorn.Soil.Economy.Demo
{
    public class DemoEconomyItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI identifierText;
        [SerializeField] private TextMeshProUGUI balanceText;

        public string Identifier { get; private set; }

        internal void Setup(string identifier, int balance)
        {
            Identifier = identifier;
            identifierText.text = identifier;
            balanceText.text = balance.ToString();
        }
    }
}
