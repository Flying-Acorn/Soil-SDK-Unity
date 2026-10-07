using System;
using System.Collections.Generic;

namespace FlyingAcorn.Soil.Push.Logic
{
    /// <summary>
    /// Where a push provider (the Firebase bridge, or a game's own code) hands over device tokens and received
    /// pushes, and where the Push module picks them up. It lives in this small assembly because the bridge's
    /// assembly cannot see the SDK's own code, and the SDK must not depend on Firebase.
    /// <para>
    /// Nothing is lost to timing: the latest token is replayed to a late subscriber, and a push that opened the
    /// game before anyone listened is kept until someone does.
    /// </para>
    /// </summary>
    public static class PushHub
    {
        private static readonly object Lock = new object();
        private static Action<string> _tokenReceived;
        private static Action<PushMessage> _messageReceived;
        private static Action _firebaseStart;
        private static bool _firebaseStartRequested;
        private static readonly List<PushMessage> Waiting = new List<PushMessage>();
        private const int MaxWaiting = 5;

        /// <summary>The latest device token reported, or null.</summary>
        public static string Token { get; private set; }

        /// <summary>
        /// Raised once the game says Firebase is ready (Push.StartFirebaseBridge); the Firebase bridge listens. A
        /// listener that subscribes after the request still gets it.
        /// </summary>
        public static event Action FirebaseStartRequested
        {
            add
            {
                bool requested;
                lock (Lock)
                {
                    _firebaseStart += value;
                    requested = _firebaseStartRequested;
                }
                if (requested) value?.Invoke();
            }
            remove
            {
                lock (Lock) _firebaseStart -= value;
            }
        }

        public static void RequestFirebaseStart()
        {
            Action handlers;
            lock (Lock)
            {
                if (_firebaseStartRequested) return;
                _firebaseStartRequested = true;
                handlers = _firebaseStart;
            }
            handlers?.Invoke();
        }

        /// <summary>Fired for every new token; a late subscriber gets the latest one at once.</summary>
        public static event Action<string> TokenReceived
        {
            add
            {
                string token;
                lock (Lock)
                {
                    _tokenReceived += value;
                    token = Token;
                }
                // A newer token reported meanwhile already reached this subscriber; never replay an older one after it.
                if (!string.IsNullOrEmpty(token) && token == Token) value?.Invoke(token);
            }
            remove
            {
                lock (Lock) _tokenReceived -= value;
            }
        }

        /// <summary>Fired for every push; pushes that arrived before the first subscriber are delivered to it.</summary>
        public static event Action<PushMessage> MessageReceived
        {
            add
            {
                List<PushMessage> waiting;
                lock (Lock)
                {
                    _messageReceived += value;
                    waiting = new List<PushMessage>(Waiting);
                    Waiting.Clear();
                }
                foreach (var message in waiting) value?.Invoke(message);
            }
            remove
            {
                lock (Lock) _messageReceived -= value;
            }
        }

        public static void ReportToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            Action<string> handlers;
            lock (Lock)
            {
                if (token == Token) return;
                Token = token;
                handlers = _tokenReceived;
            }
            handlers?.Invoke(token);
        }

        public static void ReportMessage(PushMessage message)
        {
            if (message == null) return;
            Action<PushMessage> handlers;
            lock (Lock)
            {
                handlers = _messageReceived;
                if (handlers == null)
                {
                    if (Waiting.Count >= MaxWaiting) Waiting.RemoveAt(0);
                    Waiting.Add(message);
                    return;
                }
            }
            handlers.Invoke(message);
        }

        /// <summary>Forget everything: tests, and play mode without domain reload.</summary>
        public static void Reset()
        {
            lock (Lock)
            {
                _tokenReceived = null;
                _messageReceived = null;
                _firebaseStart = null;
                _firebaseStartRequested = false;
                Waiting.Clear();
                Token = null;
            }
        }
    }
}
