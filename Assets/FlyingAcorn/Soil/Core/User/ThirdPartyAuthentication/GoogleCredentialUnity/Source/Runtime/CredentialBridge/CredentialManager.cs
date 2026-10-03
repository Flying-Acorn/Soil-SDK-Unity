// <copyright file="CredentialManager.cs" author="Gabriel Oliveira Almeida">
// Copyright (C) 2018 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace CredentialBridge
{
    public class CredentialManager : MonoBehaviour
    {
        // Fire for every result, whoever started the request. Callers that need their own result should pass
        // callbacks to StartCredentialProcess instead of listening here.
        public static readonly UnityEvent<CredentialUserData> OnLoginSucess = new UnityEvent<CredentialUserData>();
        public static readonly UnityEvent<CredentialExceptionData> OnLoginFailed = new UnityEvent<CredentialExceptionData>();

        // Reported when the bridge itself fails, so a request never ends without a result.
        public const string BridgeErrorType = "CredentialBridge.BridgeError";
        public const string InvalidResponseType = "CredentialBridge.InvalidResponse";

        private static CredentialManager m_instance;
        private static GameObject m_gameObject;

        private const string m_objectName = "JavaBridge";
        private const string m_methodSucessName = "OnLogin";
        private const string m_methodExceptionName = "OnException";
        private const string m_libaryObjectName = "com.flyingacorn.soil.credential.SoilCredentialBridge";
        private const string m_libaryObjectMethod = "getUserDataUnity";
        private string m_oathID = "";

        // Only the latest request is answered; results that echo an older id are dropped.
        private static int s_lastRequestId;
        private static string s_pendingRequestId;
        private static Action<CredentialUserData> s_pendingSuccess;
        private static Action<CredentialExceptionData> s_pendingFailure;

        // Tests replace the Java call with this (oauth client id, request id); null means call the Java bridge.
        internal static Action<string, string> BridgeInvoker;

        private CredentialManager(){}

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public static CredentialManager GetInstance()
        {
            if(m_instance == null && m_gameObject == null)
            {
                m_gameObject = new GameObject(m_objectName, typeof(CredentialEventHandler));
                m_instance = m_gameObject.AddComponent<CredentialManager>();
            }

            return m_instance;
        }

        public static void SetupOathID(string oathID)
        {
            CredentialManager instance = GetInstance();
            instance.m_oathID = oathID;
        }

        public static void StartCredentialProcess()
        {
            StartCredentialProcess(null, null);
        }

        /// <summary>
        /// Starts a sign-in and reports its outcome exactly once to the given callbacks, unless a newer request
        /// supersedes it first, in which case it reports nothing. A superseded request is not told it was
        /// replaced: Soil's sign-in callbacks are global, so a late failure would land on the newer attempt.
        /// Callers that need to know should not start a second request while one is pending.
        /// </summary>
        public static void StartCredentialProcess(Action<CredentialUserData> onSuccess,
            Action<CredentialExceptionData> onFailure)
        {
            var requestId = (++s_lastRequestId).ToString();
            s_pendingRequestId = requestId;
            s_pendingSuccess = onSuccess;
            s_pendingFailure = onFailure;

            try
            {
                var oathID = GetInstance().m_oathID;
                if (BridgeInvoker != null)
                {
                    BridgeInvoker(oathID, requestId);
                    return;
                }

                using var bridge = new AndroidJavaObject(m_libaryObjectName);
                bridge.Call(m_libaryObjectMethod, oathID, requestId, m_objectName,
                    m_methodSucessName, m_methodExceptionName);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Complete(requestId, null, new CredentialExceptionData { type = BridgeErrorType, message = e.Message });
            }
        }

        /// <param name="requestId">The id the result echoes; null when it could not be read, which is treated as
        /// the pending request so a malformed payload still ends it.</param>
        internal static void Complete(string requestId, CredentialUserData? success, CredentialExceptionData? failure)
        {
            if (s_pendingRequestId == null || (requestId != null && requestId != s_pendingRequestId))
            {
                Debug.LogWarning($"[CredentialManager] Ignoring result of superseded request {requestId}");
                return;
            }

            var onSuccess = s_pendingSuccess;
            var onFailure = s_pendingFailure;
            s_pendingRequestId = null;
            s_pendingSuccess = null;
            s_pendingFailure = null;

            // A throwing caller must not stop the shared events or escape into whoever delivered the result.
            if (success.HasValue)
            {
                SafeInvoke(onSuccess, success.Value);
                SafeInvoke(OnLoginSucess.Invoke, success.Value);
            }
            else if (failure.HasValue)
            {
                SafeInvoke(onFailure, failure.Value);
                SafeInvoke(OnLoginFailed.Invoke, failure.Value);
            }
        }

        private static void SafeInvoke<T>(Action<T> callback, T value)
        {
            if (callback == null)
                return;

            try
            {
                callback(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // Tests start every case from a clean state.
        internal static void ResetForTests()
        {
            s_pendingRequestId = null;
            s_pendingSuccess = null;
            s_pendingFailure = null;
            BridgeInvoker = null;
        }
    }
}
