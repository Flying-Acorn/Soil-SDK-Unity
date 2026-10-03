// <copyright file="CredentialEventHandler.cs" author="Gabriel Oliveira Almeida">
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
using UnityEngine;

namespace CredentialBridge
{
    public class CredentialEventHandler : MonoBehaviour
    {
        // The Java bridge echoes the id it was started with next to the payload fields.
        [Serializable]
        private struct RequestEnvelope
        {
#pragma warning disable 0649 // Assigned by JsonUtility.
            public string requestId;
#pragma warning restore 0649
        }

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        internal void OnLogin(string message)
        {
            var requestId = ReadRequestId(message);
            CredentialUserData data;
            try
            {
                data = JsonUtility.FromJson<CredentialUserData>(message);
            }
            catch (Exception e)
            {
                Fail(requestId, $"{UtilStrings.ErrorCredentialDataNull}: {e.Message}");
                return;
            }

            if (string.IsNullOrEmpty(data.id))
            {
                Fail(requestId, UtilStrings.ErrorCredentialDataNull);
                return;
            }

            CredentialManager.Complete(requestId, data, null);
        }

        internal void OnException(string message)
        {
            var requestId = ReadRequestId(message);
            CredentialExceptionData data;
            try
            {
                data = JsonUtility.FromJson<CredentialExceptionData>(message);
            }
            catch (Exception e)
            {
                Fail(requestId, $"{UtilStrings.ErrorExcepetionDataNull}: {e.Message}");
                return;
            }

            if (string.IsNullOrEmpty(data.type))
            {
                Fail(requestId, UtilStrings.ErrorExcepetionDataNull);
                return;
            }

            CredentialManager.Complete(requestId, null, data);
        }

        private static string ReadRequestId(string message)
        {
            try
            {
                // JsonUtility reads a missing field as "", which must count as "no id", not as another request.
                var requestId = JsonUtility.FromJson<RequestEnvelope>(message).requestId;
                return string.IsNullOrEmpty(requestId) ? null : requestId;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Fail(string requestId, string message)
        {
            Debug.LogError(message);
            CredentialManager.Complete(requestId, null,
                new CredentialExceptionData { type = CredentialManager.InvalidResponseType, message = message });
        }
    }
}
