using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel de OPCIONES (Fase 8): cuatro deslizadores de volumen. Se abre desde el menú de inicio y desde la pausa.
// Solo pinta y reenvía: el volumen lo guarda y aplica VolumeSettings.
//
// Los deslizadores y textos van en el orden de VolumeChannel (General, Música, Efectos, Ambiente).
// Lo monta Tools > Fairground > Build Pause And Options.
public class OptionsPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Slider[] sliders = new Slider[4];
    [SerializeField] private TMP_Text[] valueTexts = new TMP_Text[4];
    [SerializeField] private Button closeButton;
    [SerializeField] private Button openButton;   // OPCIONES del menú de inicio (opcional)

    private VolumeSettings volume;
    private bool syncing;

    public bool IsOpen => panel != null && panel.activeSelf;
    // PauseMenuUI vuelve a enseñar su tablero al cerrar
    public event System.Action Closed;

    private void Awake()
    {
        volume = SceneCache<VolumeSettings>.Get();
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (openButton != null) openButton.onClick.AddListener(Open);

        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            VolumeChannel channel = (VolumeChannel)i;   // copia local: cada lambda guarda su canal
            sliders[i].minValue = 0f;
            sliders[i].maxValue = 1f;
            sliders[i].onValueChanged.AddListener(v => HandleSlider(channel, v));
        }
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel == null) return;
        Sync();
        panel.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        if (volume != null) volume.Save();
        Closed?.Invoke();
    }

    // Pone los deslizadores en los valores guardados sin disparar onValueChanged como si los moviera el jugador
    private void Sync()
    {
        if (volume == null) return;
        syncing = true;
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] == null) continue;
            float level = volume.Get((VolumeChannel)i);
            sliders[i].SetValueWithoutNotify(level);
            SetText(i, level);
        }
        syncing = false;
    }

    private void HandleSlider(VolumeChannel channel, float level)
    {
        if (syncing) return;
        if (volume != null) volume.Set(channel, level);
        SetText((int)channel, level);
    }

    private void SetText(int i, float level)
    {
        if (i < valueTexts.Length && valueTexts[i] != null)
            valueTexts[i].text = $"{Mathf.RoundToInt(level * 100f)} %";
    }
}
