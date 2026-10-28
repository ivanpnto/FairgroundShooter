using UnityEngine;

/// <summary>
/// Datos que definen un arma. No tiene lógica: solo almacena valores.
/// Cada arma del juego es un asset de este tipo (Create > FairgroundShooter > Weapon Data).
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "FairgroundShooter/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string displayName = "Arma";
    [Tooltip("Prefab del modelo (carpeta PolyOne/Free Gun/Prefabs)")]
    public GameObject modelPrefab;

    [Header("Colocación en la mano (relativa a WeaponHolder)")]
    // Cada modelo tiene un tamaño y un pivote distintos, así que cada arma
    // necesita su propio ajuste para verse bien en pantalla
    public Vector3 holdPosition = Vector3.zero;
    public Vector3 holdRotation = Vector3.zero;
    public Vector3 holdScale = Vector3.one;

    [Header("Munición")]
    [Min(1)] public int magazineSize = 12;
    [Min(0f)] public float reloadTime = 2f;

    [Header("Disparo")]
    [Tooltip("Disparos por segundo como máximo")]
    [Min(0.1f)] public float fireRate = 5f;
    [Tooltip("true = dispara mientras se mantiene el clic; false = un disparo por clic")]
    public bool isAutomatic = false;
    [Min(1f)] public float maxDistance = 100f;

    // Mismos valores por defecto que WeaponVisual: así un arma nueva se siente
    // igual que la pistola actual hasta que se ajuste a mano
    [Header("Retroceso del modelo")]
    [Tooltip("Metros que el modelo retrocede hacia atrás con cada disparo")]
    public float recoilKick = 0.06f;
    [Tooltip("Grados que el cañón sube con cada disparo")]
    public float recoilAngle = 10f;
    [Tooltip("Velocidad de vuelta a la pose de reposo (mayor = vuelve antes)")]
    public float recoverySpeed = 12f;

    [Header("Retroceso de cámara")]
    [Tooltip("Grados que sube la vista con cada disparo")]
    public float cameraKick = 1f;
    [Tooltip("Velocidad con la que la vista vuelve a su sitio (mayor = vuelve antes)")]
    public float cameraRecoverySpeed = 8f;

    [Header("Mira")]
    public bool hasScope = false;
    [Tooltip("Campo de visión al apuntar. La cámara normal tiene 60")]
    [Range(5f, 60f)] public float scopeFov = 20f;

    // Propiedad calculada: el controlador trabajará con segundos entre disparos.
    // Se guarda la cadencia en disparos/segundo porque es más intuitiva al editarla
    public float TimeBetweenShots => 1f / fireRate;
}