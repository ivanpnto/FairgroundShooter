using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Corazones del jugador (J7.1, bloque 2), abajo a la izquierda junto a la munición.
//
// Solo se ven cuando PlayerHealth está activa, es decir, desde que empieza el combate contra el boss: antes
// destriparían la sorpresa del nivel 4. Se crean desde una plantilla tantos como MaxHearts.
// Al perder uno: tiembla y se vacía. Al recuperarlo (especial rosa): «pop» de escala.
public class HeartsUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image heartTemplate;
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private float fadeSpeed = 4f;

    private PlayerHealth health;
    private readonly List<Image> hearts = new List<Image>();
    private readonly List<float> anim = new List<float>();   // >0: pop (ganado), <0: temblor (perdido)
    private int lastHearts = -1;

    private void Awake()
    {
        health = SceneCache<PlayerHealth>.Get();
        if (heartTemplate != null) heartTemplate.gameObject.SetActive(false);
        if (group != null) group.alpha = 0f;
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.HeartsChanged += HandleHearts;
        lastHearts = -1;
        HandleHearts(health.Hearts, health.MaxHearts);
    }

    private void OnDisable()
    {
        if (health != null) health.HeartsChanged -= HandleHearts;
    }

    private void HandleHearts(int current, int max)
    {
        while (hearts.Count < max)
        {
            Image heart = Instantiate(heartTemplate, heartTemplate.transform.parent);
            heart.gameObject.SetActive(true);
            hearts.Add(heart);
            anim.Add(0f);
        }

        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < current;
            hearts[i].sprite = full ? fullSprite : emptySprite;
            if (lastHearts >= 0 && i < max)
            {
                if (full && i >= lastHearts) anim[i] = 1f;          // recuperado
                else if (!full && i < lastHearts) anim[i] = -1f;    // perdido
            }
        }
        lastHearts = current;
    }

    private void Update()
    {
        if (health == null) return;

        if (group != null)
            group.alpha = Mathf.MoveTowards(group.alpha, health.IsActive ? 1f : 0f, Time.deltaTime * fadeSpeed);

        for (int i = 0; i < hearts.Count; i++)
        {
            float a = anim[i];
            Transform t = hearts[i].transform;
            if (a > 0f)
            {
                t.localScale = Vector3.one * (1f + 0.45f * Mathf.Sin(a * Mathf.PI));
                t.localRotation = Quaternion.identity;
                anim[i] = Mathf.Max(0f, a - Time.deltaTime * 2.5f);
            }
            else if (a < 0f)
            {
                float k = -a;
                t.localScale = Vector3.one * (1f + 0.25f * k);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 60f) * 18f * k);
                anim[i] = Mathf.Min(0f, a + Time.deltaTime * 2f);
            }
            else
            {
                t.localScale = Vector3.one;
                t.localRotation = Quaternion.identity;
            }
        }
    }
}
