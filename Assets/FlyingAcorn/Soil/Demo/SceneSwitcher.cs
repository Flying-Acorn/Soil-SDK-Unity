using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlyingAcorn.Soil.Demo
{
    public class SceneSwitcher : MonoBehaviour
    {
        private List<FeatureButton> _featureButtons = new List<FeatureButton>();
        public static Scene DefaultScene;
        public static Scene CurrentScene;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _featureButtons = new List<FeatureButton>(GetComponentsInChildren<FeatureButton>());
            DefaultScene = SceneManager.GetActiveScene();
        }

        private void Start()
        {
            foreach (var featureButton in _featureButtons)
            {
                featureButton.button.onClick.AddListener(() => SwitchScene(featureButton.sceneName));
            }
        }

        private static void SwitchScene(string sceneName)
        {
            if (sceneName == SceneManager.GetActiveScene().name) return;
            if (CurrentScene.IsValid())
                SceneManager.UnloadSceneAsync(CurrentScene);
            SceneManager.LoadScene(sceneName);
        }
    }
}