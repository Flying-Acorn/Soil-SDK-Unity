using System;
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

        private async void Start()
        {
            SetRelativeText();
            Failed("Press Get Leaderboard");
            try
            {
                await Leaderboard.Initialize();
                SetYourScore();
            }
            catch
            {
                Debug.LogError("Failed to initialize SoilServices");
            }

            setRelativeButton.onClick.AddListener(SetRelativeMode);
            getLeaderboardButton.onClick.AddListener(ReportScore);
        }

        private void SetYourScore()
        {
            if (SoilServices.UserInfo == null)
            {
                return;
            }

            yourScore.text = SoilServices.UserInfo.name + ":" + score;
        }

        private async void ReportScore()
        {

            try
            {
                var userScore = await Leaderboard.ReportScore(score.ToString(), "demo_dec_manual");
                GetLeaderboard(userScore);
            }
            catch (Exception e)
            {
                Failed(e.Message);
            }
        }

        private async void GetLeaderboard(UserScore userScore)
        {
            try
            {
                var leaderboard = await Leaderboard.FetchLeaderboard("demo_dec_manual", resultCount, _relativeMode);
                GetLeaderboardSuccess(leaderboard);
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