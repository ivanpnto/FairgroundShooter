using UnityEngine;
using UnityEngine.UI;

// Respuesta audiovisual al combate contra el boss (J7.2, bloque 2): viñeta roja, tartazo, sacudida y sonidos.
//
// No decide nada del juego: solo escucha los eventos que ya publicaban PlayerHealth, PlayerDamageZone,
// BossController y BossReveal (§27.8) y reacciona. Por eso se puede quitar o cambiar sin tocar la lógica.
//
// Reglas de lectura (§14, §27.6): el rojo es «peligro»; cuanto más daño, más fuerte el aviso (la viñeta y la
// sacudida del golpe de 2 corazones son mayores que las del de 1). La toma de aire del bocinazo suena muy
// distinta de todo lo demás para que se aprenda «cabeza roja + este sonido → recarga ya».
public class BossFeedback : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private Image vignette;
    [SerializeField] private Image pieSplat;
    [SerializeField] private AudioSource audioSource;

    [Header("Sonidos")]
    [SerializeField] private AudioClip inhale;          // toma de aire del bocinazo
    [SerializeField] private AudioClip honk;            // el bocinazo te alcanza
    [SerializeField] private AudioClip stun;            // bocinazo contrarrestado: aturdido
    [SerializeField] private AudioClip minionWarning;
    [SerializeField] private AudioClip playerHit;
    [SerializeField] private AudioClip pieSplatSound;
    [SerializeField] private AudioClip heartGain;
    [SerializeField] private AudioClip phaseCrash;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    [Header("Viñeta y tartazo")]
    [SerializeField] private Color vignetteColor = new Color(0.85f, 0.05f, 0.05f);
    [SerializeField] private float vignetteFade = 1.6f;      // alfa que pierde por segundo
    [SerializeField] private float splatSeconds = 1.3f;

    private PlayerHealth health;
    private PlayerDamageZone zone;
    private BossController boss;
    private BossReveal reveal;
    private float vignetteAlpha, splatAge = -1f;
    private int lastHearts;
    private Vector2 splatBasePos;

    private void Awake()
    {
        health = SceneCache<PlayerHealth>.Get();
        zone = SceneCache<PlayerDamageZone>.Get();
        boss = SceneCache<BossController>.Get();
        reveal = SceneCache<BossReveal>.Get();
        if (vignette != null) vignette.color = Clear(vignetteColor);
        if (pieSplat != null) { splatBasePos = pieSplat.rectTransform.anchoredPosition; pieSplat.gameObject.SetActive(false); }
    }

    private void OnEnable()
    {
        if (health != null) { health.Damaged += HandleDamaged; health.HeartsChanged += HandleHearts; lastHearts = health.Hearts; }
        if (zone != null) zone.HazardReached += HandleHazard;
        if (boss != null)
        {
            boss.StateChanged += HandleBossState;
            boss.HonkResolved += HandleHonkResolved;
            boss.PhaseStarted += HandlePhase;
            boss.MinionWarning += HandleMinionWarning;
        }
        if (reveal != null)
        {
            reveal.WallHit += HandleWallHit;
            reveal.BoothBurst += HandleBurst;
            reveal.BossRoared += HandleRoar;
        }
    }

    private void OnDisable()
    {
        if (health != null) { health.Damaged -= HandleDamaged; health.HeartsChanged -= HandleHearts; }
        if (zone != null) zone.HazardReached -= HandleHazard;
        if (boss != null)
        {
            boss.StateChanged -= HandleBossState;
            boss.HonkResolved -= HandleHonkResolved;
            boss.PhaseStarted -= HandlePhase;
            boss.MinionWarning -= HandleMinionWarning;
        }
        if (reveal != null)
        {
            reveal.WallHit -= HandleWallHit;
            reveal.BoothBurst -= HandleBurst;
            reveal.BossRoared -= HandleRoar;
        }
    }

    // ---------------------------------------------------------------- golpes al jugador

    private void HandleDamaged(int hearts)
    {
        bool heavy = hearts >= 2;
        vignetteAlpha = Mathf.Max(vignetteAlpha, heavy ? 0.95f : 0.6f);
        Shake(heavy ? 0.75f : 0.45f);
        Play(playerHit);
    }

    private void HandleHearts(int current, int max)
    {
        if (current > lastHearts && lastHearts > 0) Play(heartGain);   // especial rosa
        lastHearts = current;
    }

    // Una tarta que llega y QUITA vida te mancha la pantalla (si eras invulnerable, no: no te ha «dado»)
    private void HandleHazard(IPlayerHazard hazard, bool damaged)
    {
        if (!damaged || pieSplat == null) return;
        if (hazard is BossProjectile projectile && projectile.Kind == ProjectileKind.Pie)
        {
            // En un borde al azar, nunca en el centro: se ve y hace gracia, pero no tapa la mira
            Vector2 side = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.Range(-0.6f, 0.6f));
            pieSplat.rectTransform.anchoredPosition = splatBasePos + new Vector2(side.x * Random.Range(430f, 560f), side.y * 260f);
            pieSplat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));
            pieSplat.gameObject.SetActive(true);
            splatAge = 0f;
            Play(pieSplatSound);
        }
    }

    // ---------------------------------------------------------------- avisos del boss

    private void HandleBossState(BossController.BossState state)
    {
        if (state == BossController.BossState.Telegraph && boss.CurrentAttack == BossAttack.ChargedHonk)
            Play(inhale, 1.15f);   // un poco más fuerte: es EL aviso que hay que aprender
    }

    private void HandleHonkResolved(bool countered)
    {
        if (countered) { Play(stun); Shake(0.25f); }
        else Play(honk, 1.1f);
    }

    private void HandlePhase(int index)
    {
        if (index > 0) { Play(phaseCrash); Shake(0.35f); }
    }

    private void HandleMinionWarning(MinionLunge minion) => Play(minionWarning);

    // ---------------------------------------------------------------- revelación (§27.1)

    private void HandleWallHit(int hit) => Shake(hit >= 2 ? 0.45f : 0.3f);
    private void HandleBurst() => Shake(0.95f);
    private void HandleRoar() => Shake(0.4f);

    // ---------------------------------------------------------------- utilidades

    private void Shake(float trauma)
    {
        if (cameraShake != null) cameraShake.AddTrauma(trauma);
    }

    private void Play(AudioClip clip, float gain = 1f)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume * gain);
    }

    private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

    private void Update()
    {
        if (vignette != null)
        {
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, vignetteFade * Time.deltaTime);
            vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteAlpha);
        }

        if (splatAge >= 0f && pieSplat != null)
        {
            splatAge += Time.deltaTime;
            float k = splatAge / splatSeconds;
            // Plaf (escala que rebota) y luego resbala hacia abajo mientras se desvanece
            float pop = splatAge < 0.12f ? Mathf.Lerp(0.4f, 1.15f, splatAge / 0.12f) : Mathf.Lerp(1.15f, 1f, (splatAge - 0.12f) / 0.2f);
            pieSplat.rectTransform.localScale = Vector3.one * pop;
            pieSplat.rectTransform.anchoredPosition += Vector2.down * (40f * Time.deltaTime * Mathf.Clamp01(k * 2f));
            pieSplat.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.6f) / 0.4f));
            if (k >= 1f) { splatAge = -1f; pieSplat.gameObject.SetActive(false); }
        }
    }
}
