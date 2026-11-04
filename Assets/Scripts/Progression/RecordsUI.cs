using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Récords: la mejor puntuación de cada perfil, de mayor a menor. Sustituye al ranking de
// LeaderboardUI/scores.json (Fase 5): ahora cada jugador tiene un perfil y su récord vive en él.
// Solo presentación. Se repinta al mostrarse y cuando cambia algún perfil (p. ej. al acabar una partida
// con el panel ya visible). Va en el panel de Récords y en el tablero del Game Over.
public class RecordsUI : MonoBehaviour
{
    // Mismos nombres que en LeaderboardUI: así el componente se cambió en el prefab del Game Over
    // sin perder las referencias a los textos
    [SerializeField] private TMP_Text rankingNamesText;
    [SerializeField] private TMP_Text rankingScoresText;
    [SerializeField, Min(1)] private int entriesToShow = 5;

    private ProfileManager profileManager;

    private void Awake()
    {
        profileManager = FindFirstObjectByType<ProfileManager>();
    }

    private void OnEnable()
    {
        if (profileManager != null) profileManager.ProfileChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (profileManager != null) profileManager.ProfileChanged -= Refresh;
    }

    public void Refresh()
    {
        if (rankingNamesText == null || rankingScoresText == null)
            return;

        List<PlayerProfile> records = profileManager != null ? profileManager.GetRecords() : new List<PlayerProfile>();

        StringBuilder names = new StringBuilder();
        StringBuilder points = new StringBuilder();

        if (records.Count == 0)
            names.Append("Aún no hay récords");

        string current = profileManager != null && profileManager.Current != null ? profileManager.Current.name : null;

        for (int i = 0; i < records.Count && i < entriesToShow; i++)
        {
            // El perfil activo se resalta para que el jugador se encuentre en la lista
            bool mine = records[i].name == current;
            string line = $"{i + 1}.  {records[i].name}";
            names.AppendLine(mine ? $"<color=#FFD24A>{line}</color>" : line);
            points.AppendLine(mine ? $"<color=#FFD24A>{records[i].bestScore}</color>" : records[i].bestScore.ToString());
        }

        rankingNamesText.text = names.ToString();
        rankingScoresText.text = points.ToString();
    }
}
