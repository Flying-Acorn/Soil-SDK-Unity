using System;
using FlyingAcorn.Soil.Socialization.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Socialization.Demo
{
    /// <summary>One player in a friends list, with up to two actions for the list it is in.</summary>
    public class FriendRequestRow : MonoBehaviour
    {
        public TextMeshProUGUI playerName;
        public TextMeshProUGUI detail;
        public Button detailButton;
        public Button primaryButton;
        public Button secondaryButton;

        private string _publicId;

        private void Start()
        {
            detailButton.onClick.AddListener(CopyCode);
        }

        private void CopyCode()
        {
            if (string.IsNullOrEmpty(_publicId))
                return;
            GUIUtility.systemCopyBuffer = _publicId;
            Debug.Log($"Player code {_publicId} copied to clipboard");
        }

        public void SetData(FriendEntry entry, string since)
        {
            _publicId = entry.public_id;
            playerName.text = string.IsNullOrEmpty(entry.name) ? entry.username : entry.name;
            detail.text = $"{entry.public_id}\n<size=80%>{since}</size>";
        }

        /// <summary>Shows an action button, or hides it when <paramref name="label"/> is null.</summary>
        public void SetAction(bool primary, string label, Action onClick)
        {
            var button = primary ? primaryButton : secondaryButton;
            button.gameObject.SetActive(label != null);
            button.onClick.RemoveAllListeners();
            if (label == null) return;
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;
            button.onClick.AddListener(() => onClick());
        }
    }
}
