using System;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Demo
{
    public class Initializer : MonoBehaviour
    {
        [SerializeField] private bool initOnStart;
        private static Initializer Instance { get; set; }

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

        private static async void Initialize()
        {
            if (Instance == null)
                throw new Exception("Initializer instance is null");
            await SoilServices.Initialize(Instance.gameObject);
        }
    }
}