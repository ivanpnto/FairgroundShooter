using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    private List<LeaderboardEntry> scores = new List<LeaderboardEntry>();

    private string filePath;

    private void Awake()
    {
        filePath = Path.Combine(
            Application.persistentDataPath,
            "scores.json"
        );

        LoadScores();
    }

    public void AddScore(string playerName, int score)
    {
        LeaderboardEntry entry = new LeaderboardEntry(
            playerName,
            score
        );

        scores.Add(entry);

        scores = scores
            .OrderByDescending(x => x.score)
            .ToList();

        if (scores.Count > 10)
        {
            scores = scores
                .Take(10)
                .ToList();
        }

        SaveScores();
    }

    public List<LeaderboardEntry> GetTopScores(int count)
    {
        return scores
            .OrderByDescending(entry => entry.score)
            .Take(count)
            .ToList();
    }

    private void SaveScores()
    {
        LeaderboardData data = new LeaderboardData();

        data.scores = scores;

        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(filePath, json);
    }

    private void LoadScores()
    {
        if (!File.Exists(filePath))
            return;

        string json = File.ReadAllText(filePath);

        LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);

        if (data != null && data.scores != null)
        {
            scores = data.scores;
        }
    }
}