using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using TMPro;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Demo
{
    public class DemoDataFiller : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dataBox;

        private void Awake()
        {
            dataBox.text = "";
            Authenticate.OnPlayerInfoFetched += LogInfoFetched;
            Authenticate.OnUserReady += FillData;

            Authenticate.OnUserRegistered += FillTokenDataRegister;
            Authenticate.OnTokenRefreshed += FillRefreshTokenData;
        }

        private void OnDestroy()
        {
            Authenticate.OnPlayerInfoFetched -= LogInfoFetched;
            Authenticate.OnUserReady -= FillData;
            Authenticate.OnUserRegistered -= FillTokenDataRegister;
            Authenticate.OnTokenRefreshed -= FillRefreshTokenData;
        }

        private void FillTokenDataRegister(TokenData obj)
        {
            dataBox.text += $"\n\nRegistered!\nAccess Token: {obj.Access}\nRefresh Token: {obj.Refresh}";
        }

        private static void LogInfoFetched(UserInfo obj)
        {
            Debug.Log($"User info fetched: {obj.uuid}");
        }

        private void FillData(UserInfo obj)
        {
            dataBox.text += $"\n\nReady\nUsername: {obj.username}\nName: {obj.name}";
        }

        private void FillRefreshTokenData(TokenData obj)
        {
            dataBox.text += $"\n\nRefreshed!\nAccess Token: {obj.Access}\nRefresh Token: {obj.Refresh}";
        }
    }
}