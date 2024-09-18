using FlyingAcorn.Soil.Core.User;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public class SoilInitializer : MonoBehaviour
    {
        [SerializeField] private string appID;
        [SerializeField] private string sdkToken;
        [SerializeField] private bool initOnStart;
        internal static SoilInitializer Instance { get; private set; }

        private static UserInfo _currentUserInfo;
        
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            if (initOnStart)
                Initialize();
        }

        private async void Initialize()
        {
            await Authenticate.AuthenticateUser(appID, sdkToken);
        }
    }
}
