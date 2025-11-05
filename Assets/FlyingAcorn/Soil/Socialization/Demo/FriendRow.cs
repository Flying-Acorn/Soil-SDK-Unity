using FlyingAcorn.Soil.Core.User;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Socialization.Demo
{
    public class FriendRow : MonoBehaviour
    {
        public TextMeshProUGUI uuid;
        public Button uuidButton;
        public TextMeshProUGUI playerName;

        private void Start()
        {
            uuidButton.onClick.AddListener(CopyUUID);
        }

        private void CopyUUID()
        {
            if (string.IsNullOrEmpty(uuid.text))
                return;
            GUIUtility.systemCopyBuffer = uuid.text;
            Debug.Log($"UUID {uuid.text} copied to clipboard");
        }

        public void SetData(UserInfo friend)
        {
            uuid.text = friend.uuid;
            playerName.text = friend.name;
        }
    }
}