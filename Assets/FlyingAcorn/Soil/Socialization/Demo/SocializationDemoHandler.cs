using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Leaderboard.Demo;
using FlyingAcorn.Soil.Leaderboard.Models;
using FlyingAcorn.Soil.Socialization.Models;
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
        [SerializeField] private TextMeshProUGUI headerText;
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
            headerText.text = "Initializing Soil SDK...";
            statusText.text = "Initializing...";
            Reset();

            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            addButton.onClick.AddListener(AddFriend);
            removeButton.onClick.AddListener(RemoveFriend);
            friendsButton.onClick.AddListener(LoadFriends);
            setRelativeButton.onClick.AddListener(SetRelativeMode);
            getLeaderboardButton.onClick.AddListener(ReportScore);

            if (SoilServices.Ready)
            {
                OnSoilServicesReady();
            }
            else
            {
                SoilServices.InitializeAsync();
            }
        }

        private void OnDestroy()
        {
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;

            if (addButton != null)
                addButton.onClick.RemoveListener(AddFriend);
            if (removeButton != null)
                removeButton.onClick.RemoveListener(RemoveFriend);
            if (friendsButton != null)
                friendsButton.onClick.RemoveListener(LoadFriends);
            if (setRelativeButton != null)
                setRelativeButton.onClick.RemoveListener(SetRelativeMode);
            if (getLeaderboardButton != null)
                getLeaderboardButton.onClick.RemoveListener(ReportScore);
        }

        private void OnSoilServicesReady()
        {
            headerText.text = "Soil SDK ready. Press something";
            statusText.text = "Ready";
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            statusText.text = $"SDK initialization failed: {exception.Message}";
        }

        private void ReportScore()
        {
            _ = ReportScoreAsync();
        }

        private async System.Threading.Tasks.Task ReportScoreAsync()
        {
            statusText.text = "Reporting score...";
            try
            {
                var userScore = await Leaderboard.Leaderboard.ReportScore(score, "demo_dec_manual");
                GetLeaderboard(userScore);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                Reset();
            }
        }

        private void Reset()
        {
            headerText.text = "";
            foreach (var row in _leaderboardRows)
                Destroy(row.gameObject);
            _leaderboardRows = new List<LeaderboardRow>();
            foreach (var row in _friendRows)
                Destroy(row.gameObject);
            _friendRows = new List<FriendRow>();
        }

        private void GetLeaderboard(UserScore userScore)
        {
            _ = GetLeaderboardAsync(userScore);
        }

        private async System.Threading.Tasks.Task GetLeaderboardAsync(UserScore userScore)
        {
            Reset();
            statusText.text = "Working...";
            try
            {
                var leaderboard =
                    await Socialization.GetFriendsLeaderboard("demo_dec_manual", resultCount, _relativeMode);
                GetLeaderboardSuccess(leaderboard);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
            }
        }

        private void GetLeaderboardSuccess(List<UserScore> rows)
        {
            headerText.text = "Leaderboard";

            foreach (var userScore in rows)
            {
                var row = Instantiate(leaderboardRowPrefab, rowsContainer.transform);
                row.SetData(userScore);
                row.SetPlayer(userScore.uuid == SoilServices.UserInfo.uuid);
                _leaderboardRows.Add(row);
            }

            statusText.text = rows.Count == 0 ? "No Scores" : "";
        }

        private void AddFriend()
        {
            _ = AddFriendAsync();
        }

        private async System.Threading.Tasks.Task AddFriendAsync()
        {
            statusText.text = "Working...";
            FriendsResponse response;
            try
            {
                response = await Socialization.AddFriendWithUUID(idInput.text);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                Reset();
                return;
            }

            if (response.detail.code != Constants.FriendshipStatus.FriendshipCreated)
            {
                statusText.text = response.detail.message;
                return;
            }

            statusText.text = "";
            LoadFriends();
        }

        private void RemoveFriend()
        {
            _ = RemoveFriendAsync();
        }

        private async System.Threading.Tasks.Task RemoveFriendAsync()
        {
            statusText.text = "Working...";
            FriendsResponse response;
            try
            {
                response = await Socialization.RemoveFriendWithUUID(idInput.text);
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                Reset();
                return;
            }

            if (response.detail.code != Constants.FriendshipStatus.FriendshipDeleted)
            {
                statusText.text = response.detail.message;
                return;
            }

            statusText.text = "";
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

        private void LoadFriends()
        {
            _ = LoadFriendsAsync();
        }

        private async System.Threading.Tasks.Task LoadFriendsAsync()
        {
            // Clear existing rows so repeated requests don't append duplicates
            Reset();

            // Show a refreshing indicator and disable buttons to prevent concurrent actions
            TextMeshProUGUI friendsBtnTextComp = null;
            string originalFriendsBtnText = null;
            try
            {
                if (friendsButton != null)
                {
                    friendsBtnTextComp = friendsButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (friendsBtnTextComp != null)
                    {
                        originalFriendsBtnText = friendsBtnTextComp.text;
                        friendsBtnTextComp.text = "Refreshing...";
                    }
                    friendsButton.interactable = false;
                }

                if (addButton != null) addButton.interactable = false;
                if (removeButton != null) removeButton.interactable = false;
                if (setRelativeButton != null) setRelativeButton.interactable = false;
                if (getLeaderboardButton != null) getLeaderboardButton.interactable = false;

                statusText.text = "Refreshing...";
            }
            catch
            {
                // Ignore UI update failures; continue to attempt load
                statusText.text = "Refreshing...";
            }
            FriendsResponse response = null;
            try
            {
                response = await Socialization.GetFriends();
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
                Reset();
                return;
            }
            finally
            {
                // restore UI state regardless of success/failure
                try
                {
                    if (friendsBtnTextComp != null)
                        friendsBtnTextComp.text = originalFriendsBtnText ?? friendsBtnTextComp.text;
                    if (friendsButton != null) friendsButton.interactable = true;
                    if (addButton != null) addButton.interactable = true;
                    if (removeButton != null) removeButton.interactable = true;
                    if (setRelativeButton != null) setRelativeButton.interactable = true;
                    if (getLeaderboardButton != null) getLeaderboardButton.interactable = true;
                }
                catch
                {
                    // ignore UI restore failures
                }
            }

            if (response == null) return;

            if (response.detail.code != Constants.FriendshipStatus.FriendshipExists)
            {
                statusText.text = response.detail.message;
                return;
            }

            GetFriendsSuccess(response.friends);
        }

        private void GetFriendsSuccess(List<FriendInfo> friends)
        {
            headerText.text = "Friends";
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