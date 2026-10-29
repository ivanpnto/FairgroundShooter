using UnityEngine;

/// <summary>
/// Sonidos del arma: disparo y recarga (cargador fuera / dentro). Solo presentación:
/// LEE el estado de WeaponAmmo y WeaponVisual, como WeaponVisual lee el de WeaponAmmo.
/// Va en Player. Los clips y volúmenes salen del WeaponData del arma equipada.
/// </summary>
public class WeaponEffects : MonoBehaviour
{
    // Por debajo de esto el cargador se considera dentro. No es 0 exacto para no depender
    // de que la curva llegue justo a 0 (con otras tangentes podría quedarse en 0.0001)
    private const float MagazineInThreshold = 0.01f;
    // Puntos con los que se muestrea magazineCurve al empezar cada recarga
    private const int CurveSamples = 200;

    [Tooltip("Fuentes para los disparos. 4 bastan: el clip más largo de un arma automática dura 0,4 s y la más rápida dispara cada 77 ms")]
    [Min(1)] [SerializeField] private int shotSourceCount = 4;

    private WeaponAmmo weaponAmmo;
    private WeaponController weaponController;
    private WeaponVisual weaponVisual;
    private GameManager gameManager;

    // Un pool de fuentes usadas por turnos, en lugar de una sola con PlayOneShot: el pitch
    // es una propiedad del AudioSource, no de cada PlayOneShot. Si todos los disparos
    // salieran de la misma fuente, cambiar el pitch para el disparo nuevo cambiaría también
    // el de los anteriores que aún suenan (sus colas se oirían desafinarse de golpe). Con
    // turnos, cuando una fuente se reutiliza su último disparo ya ha terminado o casi
    private AudioSource[] shotSources;
    private int nextShotSource;

    // La recarga tiene su propia fuente: al cambiar de arma hay que cortarla con Stop(),
    // y Stop() corta todo lo que suena en la fuente. En el pool cortaría también disparos
    private AudioSource reloadSource;

    // Estado de la recarga en curso, para que cada sonido suene una sola vez por recarga
    private bool wasReloading;
    private bool magOutPlayed;
    private bool magInPlayed;
    // Momentos (progreso 0..1) en los que el cargador empieza a salir y vuelve a entrar
    private float magOutAt;
    private float magInAt;

    private void Awake()
    {
        // Todos van en Player (WeaponVisual en su hijo WeaponHolder): GetComponent evita
        // campos que asignar en la escena. includeInactive por si la mira ha ocultado WeaponHolder
        weaponAmmo = GetComponent<WeaponAmmo>();
        weaponController = GetComponent<WeaponController>();
        weaponVisual = GetComponentInChildren<WeaponVisual>(true);
        gameManager = FindFirstObjectByType<GameManager>();

        shotSources = new AudioSource[shotSourceCount];
        for (int i = 0; i < shotSources.Length; i++)
            shotSources[i] = CreateSource();

        reloadSource = CreateSource();
    }

    // Creadas por código y no en la escena: son un detalle interno de este componente, y
    // así cambiar cuántas hay no obliga a tocar Test.unity
    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        // 2D: el arma está pegada a la cámara, no tiene sentido espacializarla. Así además
        // suena igual en los dos oídos (los clips ya son mono)
        source.spatialBlend = 0f;
        return source;
    }

    // Suscribirse en OnEnable y desuscribirse en OnDisable evita llamadas
    // a un objeto desactivado o destruido
    private void OnEnable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot += HandleShot;
        if (weaponController != null) weaponController.OnWeaponEquipped += HandleWeaponEquipped;
    }

    private void OnDisable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot -= HandleShot;
        if (weaponController != null) weaponController.OnWeaponEquipped -= HandleWeaponEquipped;
    }

    private void HandleShot()
    {
        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;
        if (weapon == null || weapon.shootSound == null)
            return;

        AudioSource source = shotSources[nextShotSource];
        nextShotSource = (nextShotSource + 1) % shotSources.Length;

        source.pitch = Random.Range(1f - weapon.pitchVariation, 1f + weapon.pitchVariation);
        source.PlayOneShot(weapon.shootSound, weapon.shootVolume);
    }

    // Equip cancela la recarga (WeaponAmmo.Configure): se corta lo que sonaba del arma
    // anterior y se marcan sus sonidos como ya reproducidos para que no suenen los que faltaban
    private void HandleWeaponEquipped(WeaponData data)
    {
        reloadSource.Stop();
        magOutPlayed = true;
        magInPlayed = true;
    }

    private void Update()
    {
        if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            return;

        if (weaponAmmo == null)
            return;

        bool reloading = weaponAmmo.IsReloading;

        if (reloading && !wasReloading)
            BeginReload();

        wasReloading = reloading;

        if (!reloading)
            return;

        // Se compara el progreso con los momentos calculados, no el valor de la curva con un
        // umbral en el frame actual: así, aunque un frame lento se salte el tramo en el que
        // el cargador sale, el sonido suena igual (una vez, gracias al flag). Y como todo va
        // con ReloadProgress, sigue coincidiendo con la animación aunque cambie reloadTime
        float progress = weaponAmmo.ReloadProgress;
        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;

        if (!magOutPlayed && progress >= magOutAt)
        {
            magOutPlayed = true;
            PlayReloadSound(weapon != null ? weapon.magOutSound : null);
        }

        if (!magInPlayed && progress >= magInAt)
        {
            magInPlayed = true;
            PlayReloadSound(weapon != null ? weapon.magInSound : null);
        }
    }

    private void BeginReload()
    {
        // Sin WeaponVisual no hay curva con la que sincronizar: mejor no sonar que sonar a destiempo
        if (weaponVisual == null)
        {
            magOutPlayed = true;
            magInPlayed = true;
            return;
        }

        magOutPlayed = false;
        magInPlayed = false;
        FindMagazineMoments();
    }

    /// <summary>
    /// Muestrea la curva del cargador para saber cuándo empieza a salir (primer punto por
    /// encima del umbral) y cuándo vuelve a entrar (primer punto por debajo del umbral
    /// después del máximo). Se hace al empezar cada recarga, no en Awake: así recoge los
    /// cambios que se hagan a la curva en el Inspector durante el Play.
    /// </summary>
    private void FindMagazineMoments()
    {
        magOutAt = 2f; // > 1: si la curva nunca sale, ese sonido no se reproduce
        magInAt = 2f;

        // Primero el máximo: "vuelve a entrar" tiene que ser después de haber salido del todo
        float peak = 0f;
        float peakAt = 0f;
        for (int i = 0; i <= CurveSamples; i++)
        {
            float t = (float)i / CurveSamples;
            float m = weaponVisual.MagazineOutAmount(t);

            if (magOutAt > 1f && m > MagazineInThreshold)
                magOutAt = t;

            if (m > peak)
            {
                peak = m;
                peakAt = t;
            }
        }

        if (peak <= MagazineInThreshold)
            return;

        for (int i = Mathf.CeilToInt(peakAt * CurveSamples); i <= CurveSamples; i++)
        {
            float t = (float)i / CurveSamples;
            if (weaponVisual.MagazineOutAmount(t) <= MagazineInThreshold)
            {
                magInAt = t;
                break;
            }
        }
    }

    // Un clip sin asignar (p. ej. un arma sin sonido de cargador) simplemente no suena
    private void PlayReloadSound(AudioClip clip)
    {
        if (clip != null)
            reloadSource.PlayOneShot(clip);
    }
}
