using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Leaderboard.Demo;
using FlyingAcorn.Soil.Leaderboard.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Socialization.Demo
{
    public class SocializationDemoHandler : MonoBehaviour
    {
        [SerializeField] public FriendRow friendRowPrefab;
        [SerializeField] private LeaderboardRow leaderboardRowPrefab;
        [SerializeField] private VerticalLayoutGroup rowsContainer;
        [SerializeField] private TMP_InputField idInput;
        [SerializeField] private Button addButton;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button getLeaderboardButton;
        [SerializeField] private Button setRelativeButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private long score = 100;
        [SerializeField] private int resultCount = 100;
        [SerializeField] private TextMeshProUGUI yourScore;
        private List<FriendRow> _friendRows = new();
        private List<LeaderboardRow> _leaderboardRows = new();
        private bool _relativeMode;

        private void Start()
        {
            SetRelativeText();
            statusText.text = "Press something";

            _ = Socialization.Initialize();
            addButton.onClick.AddListener(AddFriend);
            removeButton.onClick.AddListener(RemoveFriend);
            friendsButton.onClick.AddListener(LoadFriends);
            setRelativeButton.onClick.AddListener(SetRelativeMode);
            getLeaderboardButton.onClick.AddListener(ReportScore);
        }

        private void OnDestroy()
        {
            addButton.onClick.RemoveListener(AddFriend);
            removeButton.onClick.RemoveListener(RemoveFriend);
            friendsButton.onClick.RemoveListener(LoadFriends);
            setRelativeButton.onClick.RemoveListener(SetRelativeMode);
            getLeaderboardButton.onClick.RemoveListener(ReportScore);
        }

        private void SetYourScore()
        {
            if (SoilServices.UserInfo == null)
            {
                return;
            }

            yourScore.text = "score" + ":" + score;
        }

        private async void ReportScore()
        {

            try
            {
                var userScore = await Leaderboard.Leaderboard.ReportScore(score, "demo_dec_manual");
                GetLeaderboard(userScore);
                SetYourScore();
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                return;
            }
        }

        private async void GetLeaderboard(UserScore userScore)
        {
            foreach (var row in _leaderboardRows)
                Destroy(row.gameObject);
            _leaderboardRows = new List<LeaderboardRow>();
            foreach (var row in _friendRows)
                Destroy(row.gameObject);
            _friendRows = new List<FriendRow>();
            
            try
            {
                var leaderboard = await Socialization.GetFriendsLeaderboard("demo_dec_manual", resultCount, _relativeMode);
                GetLeaderboardSuccess(leaderboard);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                return;
            }
        }

        private void GetLeaderboardSuccess(List<UserScore> rows)
        {
            _leaderboardRows = new List<LeaderboardRow>();

            foreach (var userScore in rows)
            {
                var row = Instantiate(leaderboardRowPrefab, rowsContainer.transform);
                row.SetData(userScore);
                _leaderboardRows.Add(row);
            }
            
            statusText.text = rows.Count == 0 ? "No Scores" : "";
        }

        private async void AddFriend()
        {
            try
            {
                await Socialization.AddFriendWithUUID(idInput.text);
                statusText.text = "";
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                return;
            }

            LoadFriends();
        }

        private async void RemoveFriend()
        {
            try
            {
                var value = await Socialization.RemoveFriendWithUUID(idInput.text);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                return;
            }

            LoadFriends();
        }

        private void SetRelativeText()
        {
            setRelativeButton.GetComponentInChildren<TextMeshProUGUI>().text =
                "Relative:" + (_relativeMode ? "Yes" : "No");
        }

        private void SetRelativeMode()
        {
            _relativeMode = !_relativeMode;
            SetRelativeText();
        }

        private async void LoadFriends()
        {
            foreach (var row in _leaderboardRows)
                Destroy(row.gameObject);
            _leaderboardRows = new List<LeaderboardRow>();
            foreach (var row in _friendRows)
                Destroy(row.gameObject);

            _friendRows = new List<FriendRow>();
            var friends = new List<UserInfo>();
            try
            {
                friends = (await Socialization.GetFriends()).friends;
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                return;
            }
            
            GetFriendsSuccess(friends);
        }

        private void GetFriendsSuccess(List<UserInfo> friends)
        {
            foreach (var friend in friends)
            {
                var row = Instantiate(friendRowPrefab, rowsContainer.transform);
                row.SetData(friend);
                _friendRows.Add(row);
            }

            statusText.text = friends.Count == 0 ? "No friends" : "";
        }
    }
}