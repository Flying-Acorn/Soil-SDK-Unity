// <copyright file="SampleUI.cs" author="Gabriel Oliveira Almeida">
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

using CredentialBridge;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class SampleUI : MonoBehaviour
{
    [Header("Config OathClient")]
    [SerializeField] private string OathClientID = "";
    [Header("Singin Button")]
    [SerializeField] private Button m_button;
    [Header("Config User UI Elements")]
    [SerializeField] private CanvasGroup m_userGroup;
    [SerializeField] private Image m_avatar;
    [SerializeField] private TextMeshProUGUI m_email;
    [SerializeField] private TextMeshProUGUI m_name;
    [Header("Config Exception UI Elements")]
    [SerializeField] private CanvasGroup m_excepetionGroup;
    [SerializeField] private TextMeshProUGUI m_excepetionType;
    [SerializeField] private TextMeshProUGUI m_excepetionMessage;
    [Header("Config Loding UI Elements")]
    [SerializeField] private CanvasGroup m_loading;
    private bool m_running = false;

    private void Awake()
    {
        //Setup Events
        m_button.onClick.AddListener(HandleLoginBtn);
        CredentialManager.OnLoginSucess.AddListener(OnLoginSucess);
        CredentialManager.OnLoginFailed.AddListener(OnLoginFailed);

        //Initial UI State
        ChangeGroupState(m_userGroup, false);
        ChangeGroupState(m_excepetionGroup, false);
        ChangeGroupState(m_loading, false);
    }

    private void OnLoginSucess(CredentialUserData userData)
    {
        ChangeGroupState(m_userGroup, true);
        ChangeGroupState(m_excepetionGroup, false);
        m_name.text = userData.displayName;
        m_email.text = userData.id;
        StartCoroutine(DownloadAvatarSprite(userData.profilePictureUri));
    }

    private void OnLoginFailed(CredentialExceptionData exceptionData)
    {
        ChangeGroupState(m_userGroup, false);
        ChangeGroupState(m_excepetionGroup, true);
        m_excepetionType.text = exceptionData.type;
        m_excepetionMessage.text = exceptionData.message;
    }

    private void HandleLoginBtn()
    {
        //Start Credential Process
        CredentialManager.SetupOathID(OathClientID);
        CredentialManager.StartCredentialProcess();
    }

    private IEnumerator DownloadAvatarSprite(string url)
    {
        if(m_running)
        {
            yield break;
        }

        StartLoading();       

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
            StopLoading();
            yield break;
        }

        Texture2D texture2d = DownloadHandlerTexture.GetContent(request);
        m_avatar.sprite = Sprite.Create(texture2d, new Rect(0, 0, texture2d.width, texture2d.height), Vector2.zero);
        StopLoading();
    }

    private void StartLoading()
    {
        ChangeGroupState(m_loading, true);
        m_running = true;
        m_button.interactable = false;
        Debug.Log("Start loading avatar");
    }

    private void StopLoading()
    {
        ChangeGroupState(m_loading, false);
        m_running = false;
        m_button.interactable = true;
        Debug.Log("Stop loading avatar");
    }

    private void ChangeGroupState(CanvasGroup canvas, bool state)
    {
        canvas.alpha = state == true? 1 : 0;
        canvas.interactable = state;
        canvas.blocksRaycasts = state;
    }
}
