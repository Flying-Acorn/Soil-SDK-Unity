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
        private bool _relativeMode;

        private async void Start()
        {
            SetRelativeText();
            Failed("Press Get Leaderboard");
            try
            {
                await SoilServices.Initialize();
                SetYourScore();
            }
            catch
            {
                Debug.LogError("SoilServices.Init() failed");
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

        private void ReportScore()
        {
            Leaderboard.ReportScore(score.ToString(), "demo_dec_manual", GetLeaderboard, Failed);
        }

        private void GetLeaderboard(UserScore userScore)
        {
            Leaderboard.FetchLeaderboard("demo_dec_manual", resultCount, _relativeMode, GetLeaderboardSuccess, Failed);
        }

        private void Failed(string error)
        {
            resultText.text = error;
        }

        private void GetLeaderboardSuccess(List<UserScore> rows)
        {
            SetYourScore();
            resultText.text = "OK";
            foreach (var userScore in rows)
            {
                var row = Instantiate(leaderboardRowPrefab, leaderboardContainer.transform);
                row.SetData(userScore);
            }
        }

        private void SetRelativeMode()
        {
            _relativeMode = !_relativeMode;
        }

        private void SetRelativeText()
        {
            setRelativeButton.GetComponentInChildren<TextMeshProUGUI>().text =
                "Relative:" + (_relativeMode ? "Yes" : "No");
        }
    }
}