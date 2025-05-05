using FlyingAcorn.Soil.Core.User;
using TMPro;
using UnityEngine;

namespace FlyingAcorn.Soil.Socialization.Demo
{
    public class FriendRow : MonoBehaviour
    {
        public TextMeshProUGUI uuid;
        public TextMeshProUGUI name;
        
        public void SetData(UserInfo friend)
        {
            uuid.text = friend.uuid;
            name.text = friend.name;
        }
    }
}