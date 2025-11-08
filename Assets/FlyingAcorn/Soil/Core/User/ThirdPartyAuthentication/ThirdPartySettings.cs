using UnityEngine;
using UnityEditor;
using System.IO;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public class ThirdPartySettings : ScriptableObject
    {
        [SerializeField] private RuntimePlatform platform;
        [SerializeField] private ThirdParty thirdParty;
        [SerializeField] private string clientId;
        [SerializeField] private string clientSecret;
        [SerializeField] private string scope = "email profile";
        [SerializeField] private string redirectUri;

        public RuntimePlatform Platform => platform;
        public string ClientId => clientId;
        public string Scope => scope;
        public string RedirectUri => redirectUri;
        public string ClientSecret => clientSecret;
        public ThirdParty ThirdParty => thirdParty;

        [MenuItem("FlyingAcorn/Soil/Core/Auth/ThirdPartySetting")]
        private static void CreateThirdPartySettings()
        {
            ThirdPartySettings asset = ScriptableObject.CreateInstance<ThirdPartySettings>();
            string folderPath = "Assets/Resources/ThirdParties";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string[] folders = folderPath.Split('/');
                string currentPath = "";
                foreach (string folder in folders)
                {
                    if (string.IsNullOrEmpty(currentPath))
                        currentPath = folder;
                    else
                        currentPath = Path.Combine(currentPath, folder).Replace("\\", "/");
                    if (!AssetDatabase.IsValidFolder(currentPath))
                        AssetDatabase.CreateFolder(Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
                }
            }
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/ThirdPartySetting.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
    }
}