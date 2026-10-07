using System;
using Firebase.Messaging;
using FlyingAcorn.Soil.Push.Logic;
using UnityEngine;

namespace FlyingAcorn.Soil.Push.FirebaseBridge
{
    /// <summary>
    /// Hands Firebase Messaging's token and pushes to Soil. Compiled only when the project has the
    /// com.google.firebase.messaging package (see the asmdef's version define), so the SDK itself never depends on
    /// Firebase.
    /// <para>
    /// It starts only when the game calls <c>Push.StartFirebaseBridge()</c>, after the game's own Firebase setup
    /// reported its dependencies Available. It never checks or fixes Firebase's dependencies itself: Firebase allows
    /// one check at a time, and a second one running beside the game's would break the game's Firebase setup.
    /// On iOS, starting it is when the system asks the player for notification permission (Firebase Messaging asks
    /// when it starts), so call it when that prompt is welcome.
    /// </para>
    /// </summary>
    internal static class SoilFirebaseMessagingBridge
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => _subscribed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Listen() => PushHub.FirebaseStartRequested += Subscribe;

        private static void Subscribe()
        {
            if (_subscribed) return;
            try
            {
                // Firebase's events outlive a play session when the domain is not reloaded: never add a second copy.
                FirebaseMessaging.TokenReceived -= OnTokenReceived;
                FirebaseMessaging.MessageReceived -= OnMessageReceived;
                FirebaseMessaging.TokenReceived += OnTokenReceived;
                FirebaseMessaging.MessageReceived += OnMessageReceived;
                _subscribed = true;
            }
            catch (Exception e)
            {
                // Firebase not usable on this device (no Google Play services, say): no token, nothing waits on it.
                try
                {
                    FirebaseMessaging.TokenReceived -= OnTokenReceived;
                }
                catch (Exception)
                {
                    // Nothing was subscribed.
                }
                Debug.LogWarning($"[Soil-Push] Firebase Messaging could not start: {e.Message}");
            }
        }

        private static void OnTokenReceived(object sender, TokenReceivedEventArgs args)
        {
            // Firebase calls this on its own thread; Push moves the work to the main thread.
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
