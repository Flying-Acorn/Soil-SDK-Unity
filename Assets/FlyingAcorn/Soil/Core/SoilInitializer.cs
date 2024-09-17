using FlyingAcorn.Soil.Core.Models;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public class SoilInitializer : MonoBehaviour
    {
        // singleton
        private static SoilInitializer _instance;
        private static PlayerInfo _currentPlayerInfo;
        
        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            Initialize();
        }

        private static async void Initialize()
        {
            _currentPlayerInfo = AuthenticatePlayerPrefs.PlayerInfo;
            await Authenticate.AuthenticateUser(_currentPlayerInfo);
        }
    }
}
