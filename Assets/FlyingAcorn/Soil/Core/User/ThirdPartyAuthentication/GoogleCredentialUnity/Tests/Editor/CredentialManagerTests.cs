using System;
using System.Collections.Generic;
using System.Linq;
using CredentialBridge;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Tests
{
    /// <summary>
    /// Drives CredentialManager the way the Java bridge does: results arrive at the JavaBridge object's
    /// OnLogin/OnException handlers as JSON carrying the request id they were started with. The handlers are
    /// called directly because SendMessage asserts in edit mode.
    /// </summary>
    public class CredentialManagerTests
    {
        private readonly List<string> _startedRequestIds = new();
        private readonly List<CredentialUserData> _successes = new();
        private readonly List<CredentialExceptionData> _failures = new();
        private readonly CapturingLogHandler _logs = new();
        private ILogHandler _originalLogHandler;

        [SetUp]
        public void SetUp()
        {
            // Expected errors would otherwise still print to the Console even when LogAssert accepts them.
            _logs.Entries.Clear();
            _originalLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = _logs;
            CredentialManager.ResetForTests();
            CredentialManager.BridgeInvoker = (_, requestId) => _startedRequestIds.Add(requestId);
            _startedRequestIds.Clear();
            _successes.Clear();
            _failures.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Debug.unityLogger.logHandler = _originalLogHandler;
            CredentialManager.ResetForTests();
            var bridge = GameObject.Find("JavaBridge");
            if (bridge != null)
                Object.DestroyImmediate(bridge);

            // Same rule LogAssert enforced: an error or exception no test expected fails the test.
            var unexpected = _logs.Entries.Where(e => e.type is LogType.Error or LogType.Exception or LogType.Assert)
                .Select(e => $"{e.type}: {e.message}").ToList();
            Assert.IsEmpty(unexpected, "Unexpected logs");
        }

        private void ExpectLog(LogType type, Func<string, bool> matches)
        {
            var index = _logs.Entries.FindIndex(e => e.type == type && matches(e.message));
            Assert.GreaterOrEqual(index, 0, $"Expected a {type} log");
            _logs.Entries.RemoveAt(index);
        }

        private string Start()
        {
            CredentialManager.StartCredentialProcess(_successes.Add, _failures.Add);
            return _startedRequestIds[^1];
        }

        private static CredentialEventHandler Handler =>
            CredentialManager.GetInstance().GetComponent<CredentialEventHandler>();

        private static void DeliverSuccess(string requestId, string id = "player@example.com")
        {
            var json = JsonUtility.ToJson(new SuccessPayload { requestId = requestId, id = id, idToken = "token" });
            Handler.OnLogin(json);
        }

        private static void DeliverFailure(string requestId, string type)
        {
            var json = JsonUtility.ToJson(new FailurePayload { requestId = requestId, type = type, message = "m" });
            Handler.OnException(json);
        }

        [Test]
        public void Success_IsDeliveredOnceToTheRequestThatStartedIt()
        {
            var requestId = Start();

            DeliverSuccess(requestId);
            DeliverSuccess(requestId);

            Assert.AreEqual(1, _successes.Count);
            Assert.AreEqual("player@example.com", _successes[0].id);
            Assert.AreEqual("token", _successes[0].idToken);
            Assert.IsEmpty(_failures);
        }

        [Test]
        public void ResultOfSupersededRequest_IsIgnored()
        {
            var first = Start();
            var secondSuccesses = new List<CredentialUserData>();
            // A second request with its own callbacks; the first one's result must reach neither caller.
            CredentialManager.StartCredentialProcess(secondSuccesses.Add, _failures.Add);
            var second = _startedRequestIds[^1];

            DeliverSuccess(first);
            ExpectLog(LogType.Warning, m => m == $"[CredentialManager] Ignoring result of superseded request {first}");
            Assert.IsEmpty(secondSuccesses);

            DeliverSuccess(second);
            Assert.AreEqual(1, secondSuccesses.Count);
            Assert.IsEmpty(_successes);
        }

        [Test]
        public void ResultWithoutRequestId_CompletesThePendingRequest()
        {
            Start();

            // No requestId field at all, as JsonUtility would read from a payload that lost it.
            Handler.OnException(
                "{\"type\":\"android.credentials.GetCredentialException.TYPE_USER_CANCELED\",\"message\":\"m\"}");

            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual("android.credentials.GetCredentialException.TYPE_USER_CANCELED", _failures[0].type);
        }

        [Test]
        public void SuccessWithoutId_EndsAsInvalidResponse()
        {
            var requestId = Start();

            DeliverSuccess(requestId, id: "");
            ExpectLog(LogType.Error, m => m == UtilStrings.ErrorCredentialDataNull);

            Assert.IsEmpty(_successes);
            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(CredentialManager.InvalidResponseType, _failures[0].type);
        }

        [Test]
        public void MalformedPayload_EndsAsInvalidResponse()
        {
            Start();

            Handler.OnLogin("{not json");
            ExpectLog(LogType.Error, m => m.StartsWith(UtilStrings.ErrorCredentialDataNull + ": "));

            Assert.IsEmpty(_successes);
            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(CredentialManager.InvalidResponseType, _failures[0].type);
        }

        [Test]
        public void BridgeThatThrows_EndsAsBridgeError()
        {
            CredentialManager.BridgeInvoker = (_, _) => throw new InvalidOperationException("no bridge");

            CredentialManager.StartCredentialProcess(_successes.Add, _failures.Add);
            ExpectLog(LogType.Exception, m => m.Contains("no bridge"));

            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(CredentialManager.BridgeErrorType, _failures[0].type);
        }

        [Test]
        public void ResultWithNothingPending_IsIgnored()
        {
            var requestId = Start();
            DeliverSuccess(requestId);

            DeliverFailure(requestId, "android.credentials.GetCredentialException.TYPE_UNKNOWN");
            ExpectLog(LogType.Warning,
                m => m == $"[CredentialManager] Ignoring result of superseded request {requestId}");

            Assert.AreEqual(1, _successes.Count);
            Assert.IsEmpty(_failures);
        }

        [Test]
        public void ThrowingCallback_StillRaisesTheSharedEvent()
        {
            var shared = 0;
            void OnShared(CredentialUserData _) => shared++;
            CredentialManager.OnLoginSucess.AddListener(OnShared);
            try
            {
                CredentialManager.StartCredentialProcess(_ => throw new InvalidOperationException("caller bug"),
                    _failures.Add);
                var requestId = _startedRequestIds[^1];

                DeliverSuccess(requestId);
                ExpectLog(LogType.Exception, m => m.Contains("caller bug"));

                Assert.AreEqual(1, shared);
            }
            finally
            {
                CredentialManager.OnLoginSucess.RemoveListener(OnShared);
            }
        }

        [TestCase("android.credentials.GetCredentialException.TYPE_USER_CANCELED", SoilExceptionErrorCode.Canceled)]
        [TestCase("android.credentials.GetCredentialException.TYPE_NO_CREDENTIAL", SoilExceptionErrorCode.NotFound)]
        [TestCase("android.credentials.GetCredentialException.TYPE_INTERRUPTED", SoilExceptionErrorCode.TransportError)]
        [TestCase("androidx.credentials.TYPE_GET_CREDENTIAL_PROVIDER_CONFIGURATION_EXCEPTION",
            SoilExceptionErrorCode.ServiceUnavailable)]
        [TestCase("androidx.credentials.TYPE_GET_CREDENTIAL_UNSUPPORTED_EXCEPTION",
            SoilExceptionErrorCode.ServiceUnavailable)]
        [TestCase(CredentialManager.BridgeErrorType, SoilExceptionErrorCode.MisConfiguration)]
        [TestCase(CredentialManager.InvalidResponseType, SoilExceptionErrorCode.InvalidResponse)]
        [TestCase("android.credentials.GetCredentialException.TYPE_UNKNOWN", SoilExceptionErrorCode.Unknown)]
        [TestCase(null, SoilExceptionErrorCode.Unknown)]
        public void CredentialErrors_MapToSoilCodes(string type, SoilExceptionErrorCode expected)
        {
            Assert.AreEqual(expected, GoogleAndroidAuthentication.MapErrorCode(type));
        }

        // The JSON the Java bridge sends; field names match SoilCredentialBridge.toJson / sendError.
        [Serializable]
        private struct SuccessPayload
        {
            public string requestId;
            public string id;
            public string idToken;
        }

        [Serializable]
        private struct FailurePayload
        {
            public string requestId;
            public string type;
            public string message;
        }

        private class CapturingLogHandler : ILogHandler
        {
            public readonly List<(LogType type, string message)> Entries = new();

            public void LogFormat(LogType logType, Object context, string format, params object[] args) =>
                Entries.Add((logType, args is { Length: > 0 } ? string.Format(format, args) : format));

            public void LogException(Exception exception, Object context) =>
                Entries.Add((LogType.Exception, exception.ToString()));
        }
    }
}
