using TMPro;
using UnityEngine;

namespace FlyingAcorn.Soil.CloudSave.Demo
{
    public class SaveRow : MonoBehaviour
    {
        public TextMeshProUGUI keyText;
        public TextMeshProUGUI valueText;

        public void SetData(string key, string value)
        {
            keyText.text = key;
            valueText.text = value;
        }
    }
}