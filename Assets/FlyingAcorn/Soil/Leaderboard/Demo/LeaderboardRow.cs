using System;
using FlyingAcorn.Soil.Leaderboard.Models;
using UnityEngine;

namespace FlyingAcorn.Soil.Leaderboard.Demo
{
    public class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI nameText;
        [SerializeField] private TMPro.TextMeshProUGUI rankText;
        [SerializeField] private TMPro.TextMeshProUGUI scoreText;

        public void SetData(UserScore score)
        {
            nameText.text = score.name;
            rankText.text = score.rank.ToString();
            scoreText.text = score.score_scientific.ToString();
        }

        internal void SetPlayer(bool isPlayer)
        {
            var imageComponent = GetComponentInChildren<UnityEngine.UI.Image>();
            if (imageComponent != null && isPlayer)
            {
                imageComponent.color = Color.yellow;
            }
        }
    }
}