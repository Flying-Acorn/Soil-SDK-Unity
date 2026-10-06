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
        private static readonly List<PushMessage> Waiting = new List<PushMessage>();
        private const int MaxWaiting = 5;

        /// <summary>The latest device token reported, or null.</summary>
        public static string Token { get; private set; }

        /// <summary>
        /// Whether the Firebase bridge may start on its own. A game that already hands tokens over itself sets this
        /// to false before its first scene finishes loading (in Awake).
        /// </summary>
        public static bool AutomaticBridge { get; set; } = true;

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
                if (!string.IsNullOrEmpty(token)) value?.Invoke(token);
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

        /// <summary>For tests: forget everything.</summary>
        public static void Reset()
        {
            lock (Lock)
            {
                _tokenReceived = null;
                _messageReceived = null;
                Waiting.Clear();
                Token = null;
                AutomaticBridge = true;
            }
        }
    }
}
