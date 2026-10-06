using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Messaging;
using FlyingAcorn.Soil.Push.Logic;
using UnityEngine;

namespace FlyingAcorn.Soil.Push.FirebaseBridge
{
    /// <summary>
    /// Hands Firebase Messaging's token and pushes to Soil. Compiled only when the project has the
    /// com.google.firebase.messaging package (see the asmdef's version define), so the SDK itself never depends on
    /// Firebase. Starts on its own after the first scene loads, unless the game set Push.UseFirebaseBridge = false.
    /// <para>
    /// It only checks Firebase's dependencies, never fixes them: the game's own Firebase setup does that (and shows
    /// any Google Play services prompt). Devices without Google Play services, or where Google refuses a token,
    /// simply never get a token; nothing waits on it.
    /// </para>
    /// </summary>
    internal static class SoilFirebaseMessagingBridge
    {
        private const int Attempts = 6;
        private const int RetryMilliseconds = 10000;
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (!PushHub.AutomaticBridge || _subscribed) return;
            Run();
        }

        private static async void Run()
        {
            try
            {
                for (var attempt = 0; attempt < Attempts && !_subscribed; attempt++)
                {
                    var status = await FirebaseApp.CheckDependenciesAsync();
                    if (status == DependencyStatus.Available)
                    {
                        Subscribe();
                        return;
                    }
                    // The game's own Firebase setup may still be fixing them (a Play services update, say).
                    await Task.Delay(RetryMilliseconds);
                }
                Debug.Log("[Soil-Push] Firebase Messaging is not available on this device; no push token.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Soil-Push] Firebase Messaging could not start: {e.Message}");
            }
        }

        private static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            FirebaseMessaging.TokenReceived += OnTokenReceived;
            FirebaseMessaging.MessageReceived += OnMessageReceived;
        }

        private static void OnTokenReceived(object sender, TokenReceivedEventArgs args)
        {
            PushHub.ReportToken(args?.Token);
        }

        private static void OnMessageReceived(object sender, MessageReceivedEventArgs args)
        {
            var message = args?.Message;
            if (message == null) return;
            PushHub.ReportMessage(PushProtocol.FromData(message.Data, message.Notification?.Title,
                message.Notification?.Body, message.NotificationOpened));
        }
    }
}
