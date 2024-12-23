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
using UnityEngine;

namespace CredentialBridge
{
    public class CredentialEventHandler : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void OnLogin(string message)
        {
            CredentialUserData data = JsonUtility.FromJson<CredentialUserData>(message);
            if (data.id == null || data.id == string.Empty)
            {
                Debug.LogError(UtilStrings.ErrorCredentialDataNull);
                return;
            }
            CredentialManager.OnLoginSucess.Invoke(data);
        }

        private void OnExcepetion(string message)
        {
            CredentialExceptionData data = JsonUtility.FromJson<CredentialExceptionData>(message);
            if (data.type == null || data.type == string.Empty)
            {
                Debug.LogError(UtilStrings.ErrorExcepetionDataNull);
                return;
            }
            CredentialManager.OnLoginFailed.Invoke(data);
        }
    }
}