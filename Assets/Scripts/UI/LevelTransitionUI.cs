using TMPro;
using UnityEngine;

// Cartel y cuenta atrás de la transición entre niveles (Fase 8). Solo pinta: el orden y los tiempos los decide
// LevelTransitionSequence.
//
// El cartel entra igual que el de victoria o derrota del boss (ResultBannerUI): rebote de 0,3 a 1 de escala y un
// balanceo leve, como un cartel colgado. Sale subiendo fuera de pantalla, como si lo izaran. Los números de la
// cuenta atrás entran grandes, encogen con rebote y se desvanecen.
public class LevelTransitionUI : MonoBehaviour
{
    [Header("Cartel")]
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text titleText;    // ¡BOSQUE SUPERADO!
    [SerializeField] private TMP_Text nextText;     // SIGUIENTE: FONDO MARINO
    [SerializeField] private TMP_Text bonusText;    // +87 por precisión (87 %)

    [Header("Cuenta atrás")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Color countColor = new Color(0.96f, 0.91f, 0.79f);
    [SerializeField] private Color goColor = new Color(1f, 0.82f, 0.29f);
    // Los números, grandes; «¡FUEGO!» tiene siete letras y a ese tamaño se saldría de la pantalla
    [SerializeField] private float numberSize = 400f;
    [SerializeField] private float goSize = 230f;

    [Header("Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popClip;
    [SerializeField] private AudioClip tickClip;
    [SerializeField] private AudioClip goClip;

    private const float CardIn = 0.45f, CardOut = 0.35f, CountLife = 0.6f, GoLife = 0.65f;   // ¡FUEGO! se va pronto: tapa el centro del escenario ya jugando
    private const float FlyHeight = 1100f;   // unidades del Canvas: más que media pantalla, sale del todo

    private float cardAge = -1f, cardOutAge = -1f, countAge = -1f, countLife;
    private Vector2 cardHome;

    private void Awake()
    {
        if (card != null) cardHome = card.anchoredPosition;
        HideAll();
    }

    public void ShowCard(string title, string next, string bonus)
    {
        titleText.text = title;
        nextText.text = next;
        bonusText.text = bonus;
        card.anchoredPosition = cardHome;
        cardAge = 0f;
        cardOutAge = -1f;
        cardGroup.gameObject.SetActive(true);
        Play(popClip);
    }

    public void HideCard()
    {
        if (cardAge >= 0f) cardOutAge = 0f;
    }

    public void ShowCount(string text, bool final)
    {
        countText.text = text;
        countText.color = final ? goColor : countColor;
        countText.fontSize = final ? goSize : numberSize;
        countLife = final ? GoLife : CountLife;
        countAge = 0f;
        countText.gameObject.SetActive(true);
        Play(final ? goClip : tickClip);
    }

    public void HideAll()
    {
        cardAge = cardOutAge = countAge = -1f;
        if (cardGroup != null) { cardGroup.alpha = 0f; cardGroup.gameObject.SetActive(false); }
        if (countText != null) countText.gameObject.SetActive(false);
    }

    private void Update()
    {
        AnimateCard();
        AnimateCount();
    }

    private void AnimateCard()
    {
        if (cardAge < 0f) return;
        cardAge += Time.deltaTime;

        float tin = Mathf.Clamp01(cardAge / CardIn);
        card.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, EaseOutBack(tin));
        card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(cardAge * 2.2f) * 2f);
        cardGroup.alpha = Mathf.Clamp01(tin * 2f);

        if (cardOutAge < 0f) return;
        cardOutAge += Time.deltaTime;
        float tout = Mathf.Clamp01(cardOutAge / CardOut);
        // Ease in: arranca despacio y acelera hacia arriba, como un cartel al que tiran de la cuerda
        card.anchoredPosition = cardHome + Vector2.up * (FlyHeight * tout * tout);
        if (tout >= 1f)
        {
            cardAge = cardOutAge = -1f;
            cardGroup.gameObject.SetActive(false);
        }
    }

    private void AnimateCount()
    {
        if (countAge < 0f) return;
        countAge += Time.deltaTime;

        float t = Mathf.Clamp01(countAge / 0.3f);
        countText.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, EaseOutBack(t));
        float fade = Mathf.Clamp01((countLife - countAge) / 0.25f);
        countText.alpha = fade;
        if (countAge >= countLife)
        {
            countAge = -1f;
            countText.gameObject.SetActive(false);
        }
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // Se pasa un poco de 1 y vuelve: el «rebote» de los carteles del juego
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
