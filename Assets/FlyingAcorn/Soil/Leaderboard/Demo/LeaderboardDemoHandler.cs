using System;
using FlyingAcorn.Soil.Core.Data;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Leaderboard.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Leaderboard.Demo
{
    public class LeaderboardDemoHandler : MonoBehaviour
    {
        [SerializeField] private Button setRelativeButton;
        [SerializeField] private LeaderboardRow leaderboardRowPrefab;
        [SerializeField] private TextMeshProUGUI yourScore;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private VerticalLayoutGroup leaderboardContainer;
    [SerializeField] private Button getLeaderboardButton;
    [SerializeField] private long score = 100;
    [SerializeField] private int resultCount = 100;
    private List<LeaderboardRow> _rows = new();
    private bool _relativeMode;

        private void Start()
        {
            SetRelativeText();
            Failed("Initializing...");

            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            if (SoilServices.Ready)
            {
                OnSoilServicesReady();
            }
            else
            {
                SoilServices.InitializeAsync();
            }

            setRelativeButton.onClick.AddListener(SetRelativeMode);
            getLeaderboardButton.onClick.AddListener(ReportScore);
        }

        private void OnDestroy()
        {
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;

            if (setRelativeButton != null)
                setRelativeButton.onClick.RemoveListener(SetRelativeMode);
            if (getLeaderboardButton != null)
                getLeaderboardButton.onClick.RemoveListener(ReportScore);
        }

        private void OnSoilServicesReady()
        {
            Failed("Ready. Press Get Leaderboard");
            SetYourScore();
        }

        private void SetYourScore()
        {
            if (SoilServices.UserInfo == null)
            {
                return;
            }

            yourScore.text = SoilServices.UserInfo.name + ":" + score;
        }

        private void ReportScore()
        {
            _ = ReportScoreAsync();
        }

        private async System.Threading.Tasks.Task ReportScoreAsync()
        {
            Failed("Reporting score...");
            try
            {
                var userScore = await Leaderboard.ReportScore(score, "demo_dec_manual");
                GetLeaderboard(userScore);
            }
            catch (Exception e)
            {
                Failed(e.Message);
            }
        }

        private void GetLeaderboard(UserScore userScore)
        {
            _ = GetLeaderboardAsync(userScore);
        }

        private async System.Threading.Tasks.Task GetLeaderboardAsync(UserScore userScore)
        {
            Failed("Loading...");
            try
            {
                var rows = _relativeMode
                    ? await Leaderboard.FetchLeaderboardAsync("demo_dec_manual", resultCount, true)
                    : await Leaderboard.FetchLeaderboardAsync("demo_dec_manual", resultCount);
                GetLeaderboardSuccess(rows);
            }
            catch (Exception e)
            {
                Failed(e.Message);
            }
        }

        private void Failed(string error)
        {
            resultText.text = error;
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            Failed($"SDK initialization failed: {exception.Message}");
        }

        private void GetLeaderboardSuccess(List<UserScore> rows)
        {
            SetYourScore();
            resultText.text = "OK";
            foreach (var row in _rows)
            {
                Destroy(row.gameObject);
            }

            _rows = new List<LeaderboardRow>();

            foreach (var userScore in rows)
            {
                var row = Instantiate(leaderboardRowPrefab, leaderboardContainer.transform);
                row.SetData(userScore);
                row.SetPlayer(userScore.uuid == SoilServices.UserInfo.uuid);
                _rows.Add(row);
            }
        }

        private void SetRelativeMode()
        {
            _relativeMode = !_relativeMode;
            SetRelativeText();
        }

        private void SetRelativeText()
        {
            setRelativeButton.GetComponentInChildren<TextMeshProUGUI>().text =
                "Relative:" + (_relativeMode ? "Yes" : "No");
        }
    }
}