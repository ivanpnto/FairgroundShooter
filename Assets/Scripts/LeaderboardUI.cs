using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankingNamesText;
    [SerializeField] private TMP_Text rankingScoresText;
    [SerializeField] private int entriesToShow = 5;

    private LeaderboardManager leaderboardManager;

    private void Awake()
    {
        leaderboardManager =
            FindFirstObjectByType<LeaderboardManager>();

        if (leaderboardManager == null)
        {
            Debug.LogError(
                "LeaderboardUI: no se ha encontrado LeaderboardManager."
            );
        }
    }

    public void UpdateRanking()
    {
        if (leaderboardManager == null)
        {
            rankingNamesText.text = "Ranking no disponible.";
            rankingScoresText.text = "";
            return;
        }

        List<LeaderboardEntry> scores =
            leaderboardManager.GetTopScores(entriesToShow);

        StringBuilder names = new StringBuilder();
        StringBuilder points = new StringBuilder();

        if (scores.Count == 0)
        {
            names.Append("Sin puntuaciones");
        }
        else
        {
            for (int i = 0; i < scores.Count; i++)
            {
                LeaderboardEntry entry = scores[i];

                names.AppendLine(
                    $"{i + 1}.  {entry.playerName}"
                );

                points.AppendLine(entry.score.ToString());
            }
        }

        rankingNamesText.text = names.ToString();
        rankingScoresText.text = points.ToString();
    }
}