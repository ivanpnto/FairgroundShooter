using UnityEngine;

/// <summary>
/// Efectos del arma: sonidos (disparo, recarga e impacto en objetivo), fogonazo con luz en la
/// boca del cañón y partículas de impacto.
/// Solo presentación: LEE el estado de WeaponAmmo, WeaponVisual, PlayerScope y PlayerShoot, como
/// WeaponVisual lee el de WeaponAmmo. Va en Player. Clips, volúmenes y escala del fogonazo
/// salen del WeaponData del arma equipada.
/// </summary>
public class WeaponEffects : MonoBehaviour
{
    // Por debajo de esto el cargador se considera dentro. No es 0 exacto para no depender
    // de que la curva llegue justo a 0 (con otras tangentes podría quedarse en 0.0001)
    private const float MagazineInThreshold = 0.01f;
    // Puntos con los que se muestrea magazineCurve al empezar cada recarga
    private const int CurveSamples = 200;

    // 6 y no 4: el sonido de impacto en objetivo usa el mismo pool, así que al acertar en
    // ráfaga se gastan dos fuentes por disparo. Con 4, en la MP5 (cada 77 ms, clip de 0,35 s)
    // una fuente se reutilizaría a los ~154 ms con su disparo aún sonando
    [Tooltip("Fuentes compartidas por disparos e impactos en objetivo, usadas por turnos")]
    [Min(1)] [SerializeField] private int shotSourceCount = 6;

    [Header("Fogonazo")]
    [Tooltip("Prefab con el ParticleSystem del destello (Assets/Prefabs/VFX/MuzzleFlash)")]
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private Color flashLightColor = new Color(1f, 0.6f, 0.25f);
    // Baja a propósito: la escena no tiene otras luces (solo la de entorno), así que una
    // luz fuerte quemaría el puesto entero en cada disparo
    [SerializeField] private float flashLightIntensity = 2f;
    [Tooltip("Alcance de la luz (metros). Se multiplica por muzzleFlashScale del arma")]
    [SerializeField] private float flashLightRange = 3f;
    [Tooltip("Segundos que la luz está encendida. Con tan poco tiempo se ve 1-2 frames, como un destello real")]
    [SerializeField] private float flashLightDuration = 0.03f;

    [Header("Impacto")]
    [Tooltip("Chispas o polvo al dar en una superficie (Assets/Prefabs/VFX/ImpactSurface)")]
    [SerializeField] private ParticleSystem impactSurfacePrefab;
    [Tooltip("Confeti al dar en un objetivo (Assets/Prefabs/VFX/ImpactTarget)")]
    [SerializeField] private ParticleSystem impactTargetPrefab;
    [Tooltip("Partículas por impacto en superficie con impactScale = 1")]
    [Min(1)] [SerializeField] private int surfaceBaseCount = 10;
    [Tooltip("Partículas por impacto en objetivo con impactScale = 1")]
    [Min(1)] [SerializeField] private int targetBaseCount = 25;
    // Aquí y no en WeaponData: acertar suena igual con cualquier arma
    [Tooltip("Sonido al acertar a un objetivo (opcional: vacío = no suena)")]
    [SerializeField] private AudioClip targetHitSound;
    [Range(0f, 1f)] [SerializeField] private float targetHitVolume = 1f;

    private WeaponAmmo weaponAmmo;
    private WeaponController weaponController;
    private WeaponVisual weaponVisual;
    private PlayerScope playerScope;
    private PlayerShoot playerShoot;
    private GameManager gameManager;

    // Una sola instancia de cada efecto de impacto para toda la partida (ver HandleHit)
    private ParticleSystem impactSurface;
    private ParticleSystem impactTarget;

    // Fogonazo y luz del arma actual. Cuelgan de su muzzle: se mueven con el arma (retroceso,
    // recarga) y se ocultan solos cuando PlayerScope desactiva WeaponHolder
    private ParticleSystem muzzleFlash;
    private Light flashLight;
    private float flashLightOffTime;

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
        playerScope = GetComponent<PlayerScope>();
        playerShoot = GetComponent<PlayerShoot>();
        gameManager = FindFirstObjectByType<GameManager>();

        shotSources = new AudioSource[shotSourceCount];
        for (int i = 0; i < shotSources.Length; i++)
            shotSources[i] = CreateSource();

        reloadSource = CreateSource();

        // Sin padre (en la raíz de la escena): si colgaran de Player, se moverían con la
        // cámara, y con la mira se ocultarían junto al arma
        if (impactSurfacePrefab != null) impactSurface = Instantiate(impactSurfacePrefab);
        if (impactTargetPrefab != null) impactTarget = Instantiate(impactTargetPrefab);
    }

    // Al no tener padre no se destruyen con Player: se limpian a mano
    private void OnDestroy()
    {
        if (impactSurface != null) Destroy(impactSurface.gameObject);
        if (impactTarget != null) Destroy(impactTarget.gameObject);
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
        if (playerShoot != null) playerShoot.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        if (weaponAmmo != null) weaponAmmo.OnShot -= HandleShot;
        if (weaponController != null) weaponController.OnWeaponEquipped -= HandleWeaponEquipped;
        if (playerShoot != null) playerShoot.OnHit -= HandleHit;
    }

    private void HandleShot()
    {
        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;
        if (weapon == null)
            return;

        PlayShotSound(weapon);
        PlayMuzzleFlash();
    }

    private void PlayShotSound(WeaponData weapon)
    {
        if (weapon.shootSound == null)
            return;

        PlayPooled(weapon.shootSound, weapon.shootVolume,
            Random.Range(1f - weapon.pitchVariation, 1f + weapon.pitchVariation));
    }

    // La siguiente fuente del pool, por turnos (ver el comentario de shotSources)
    private void PlayPooled(AudioClip clip, float volume, float pitch)
    {
        AudioSource source = shotSources[nextShotSource];
        nextShotSource = (nextShotSource + 1) % shotSources.Length;

        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Partículas (y sonido, si se acierta) en el punto de impacto. Siempre se reutiliza la
    /// misma instancia de cada efecto: nada de Instantiate/Destroy por disparo.
    /// Funciona porque los dos prefabs simulan en espacio World: cada partícula guarda su
    /// posición en coordenadas del mundo al nacer, así que mover el sistema al siguiente
    /// impacto solo cambia dónde nacen las nuevas; las del impacto anterior siguen donde
    /// estaban. Y Emit() añade partículas sin reiniciar el sistema, así que varios impactos
    /// seguidos conviven hasta que se acaba su vida (Max Particles pone el tope).
    /// </summary>
    private void HandleHit(RaycastHit hit, bool hitTarget)
    {
        WeaponData weapon = weaponController != null ? weaponController.CurrentWeapon : null;
        float scale = weapon != null ? weapon.impactScale : 1f;

        ParticleSystem system = hitTarget ? impactTarget : impactSurface;
        int baseCount = hitTarget ? targetBaseCount : surfaceBaseCount;

        if (system != null)
        {
            // Un centímetro fuera de la superficie para que las partículas no nazcan dentro;
            // el +Z del prefab (eje de su cono) queda alineado con la normal
            system.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f,
                Quaternion.LookRotation(hit.normal));
            system.Emit(Mathf.Max(1, Mathf.RoundToInt(baseCount * scale)));
        }

        // Tono fijo: el sonido de acertar es el mismo con cualquier arma
        if (hitTarget && targetHitSound != null)
            PlayPooled(targetHitSound, targetHitVolume, 1f);
    }

    private void PlayMuzzleFlash()
    {
        // Con la mira puesta el modelo está oculto: el fogonazo y la luz quedarían flotando
        // delante del visor. Se usa el mismo estado con el que PlayerScope oculta el arma
        if (playerScope != null && playerScope.IsOverlayVisible)
            return;

        if (muzzleFlash != null)
        {
            // Play() no hace nada si el sistema ya está reproduciéndose, así que primero se
            // para: en la MP5 cada disparo tiene su destello aunque el anterior no haya
            // terminado. StopEmitting y no StopEmittingAndClear: el humo del disparo anterior
            // se deja terminar en lugar de desaparecer de golpe en plena ráfaga
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            muzzleFlash.Play(true);
        }

        if (flashLight != null)
        {
            flashLight.enabled = true;
            flashLightOffTime = Time.time + flashLightDuration;
        }
    }

    private void HandleWeaponEquipped(WeaponData data)
    {
        // Equip cancela la recarga (WeaponAmmo.Configure): se corta lo que sonaba del arma
        // anterior y se marcan sus sonidos como ya reproducidos para que no suenen los que faltaban
        reloadSource.Stop();
        magOutPlayed = true;
        magInPlayed = true;

        SetupMuzzleFlash(data);
    }

    /// <summary>
    /// Crea el fogonazo (y la luz, si el arma la usa) en el muzzle del arma recién equipada.
    /// Se crean una vez por arma y no en cada disparo: instanciar en ráfaga generaría basura.
    /// </summary>
    private void SetupMuzzleFlash(WeaponData data)
    {
        // Los anteriores ya se destruirían con el modelo viejo (son hijos suyos), pero así no
        // depende de eso y las referencias quedan a null al momento
        if (muzzleFlash != null) Destroy(muzzleFlash.gameObject);
        if (flashLight != null) Destroy(flashLight.gameObject);
        muzzleFlash = null;
        flashLight = null;

        WeaponModel model = weaponController != null ? weaponController.CurrentModel : null;
        Transform muzzle = model != null ? model.Muzzle : null;

        // Sin muzzle (o sin prefab asignado) el arma simplemente no tiene fogonazo
        if (muzzle == null)
            return;

        if (muzzleFlashPrefab != null)
        {
            muzzleFlash = Instantiate(muzzleFlashPrefab, muzzle);
            muzzleFlash.transform.localPosition = Vector3.zero;
            muzzleFlash.transform.localRotation = Quaternion.identity;
            // El prefab usa Scaling Mode = Hierarchy en sus dos sistemas: así esta escala
            // agranda también el humo hijo, no solo el destello
            muzzleFlash.transform.localScale = Vector3.one * data.muzzleFlashScale;
        }

        if (data.muzzleFlashLight)
        {
            GameObject lightObject = new GameObject("MuzzleFlashLight");
            lightObject.transform.SetParent(muzzle, false);

            flashLight = lightObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = flashLightColor;
            flashLight.intensity = flashLightIntensity;
            flashLight.range = flashLightRange * data.muzzleFlashScale;
            // Sin sombras: es lo que haría caro un destello que se enciende en cada disparo
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;
        }
    }

    private void Update()
    {
        // Antes de comprobar el estado: si la partida acaba justo al disparar, la luz no
        // debe quedarse encendida en el Game Over
        if (flashLight != null && flashLight.enabled && Time.time >= flashLightOffTime)
            flashLight.enabled = false;

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
