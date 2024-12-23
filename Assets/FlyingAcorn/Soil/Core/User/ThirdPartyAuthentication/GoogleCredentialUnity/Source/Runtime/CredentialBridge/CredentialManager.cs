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
using UnityEngine;
using UnityEngine.Events;

namespace CredentialBridge
{
    public class CredentialManager : MonoBehaviour
    {
        public static readonly UnityEvent<CredentialUserData> OnLoginSucess = new UnityEvent<CredentialUserData>();
        public static readonly UnityEvent<CredentialExceptionData> OnLoginFailed = new UnityEvent<CredentialExceptionData>();

        private static CredentialManager m_instance;
        private static GameObject m_gameObject;

        private const string m_objectName = "JavaBridge";
        private const string m_methodSucessName = "OnLogin";
        private const string m_methodExceptionName = "OnException";
        private const string m_libaryObjectName = "com.example.loginlibary.LibraryMain";
        private const string m_libaryObjectMethod = "getUserDataUnity";
        private string m_oathID = "";

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
            AndroidJavaObject mainFunctions = new AndroidJavaObject(m_libaryObjectName);
            object[] userParam = new object[] { GetInstance().m_oathID, m_objectName, m_methodSucessName, m_methodExceptionName };
            try
            {
                mainFunctions.Call(m_libaryObjectMethod, userParam);
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }            
        }        
    }
}
