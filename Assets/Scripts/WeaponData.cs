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

    // Mismos valores por defecto que WeaponVisual (pensados para la pistola). Un arma larga
    // necesita menos giro: la rotación es alrededor de WeaponHolder y mueve mucho el cañón
    [Header("Recarga (animación)")]
    [Tooltip("Desplazamiento del arma durante la recarga (metros, ejes del WeaponHolder)")]
    public Vector3 reloadOffset = new Vector3(0f, -0.12f, 0f);
    [Tooltip("Inclinación del arma durante la recarga (grados)")]
    public Vector3 reloadRotation = new Vector3(-15f, 0f, 30f);
    [Tooltip("Hacia dónde sale el cargador (metros, ejes del WeaponHolder: Y negativo = abajo). Debe superar la altura del cargador")]
    public Vector3 magazineOutOffset = new Vector3(0f, -0.25f, 0f);

    // Mismos valores por defecto que WeaponVisual: así un arma nueva se siente
    // igual que la pistola actual hasta que se ajuste a mano
    [Header("Retroceso del modelo")]
    [Tooltip("Metros que el modelo retrocede hacia atrás con cada disparo")]
    public float recoilKick = 0.06f;
    [Tooltip("Grados que el cañón sube con cada disparo")]
    public float recoilAngle = 10f;
    [Tooltip("Velocidad de vuelta a la pose de reposo (mayor = vuelve antes)")]
    public float recoverySpeed = 12f;

    // Proporcional al calibre, pero comprimido: con la energía real el Barrett tendría
    // ~40 veces el retroceso de la pistola. La vista no se recupera mientras se dispara a
    // la cadencia máxima, así que vaciar el cargador sube aprox. cameraKick × magazineSize
    // grados; cameraRecoverySpeed solo marca lo rápido que vuelve al dejar de disparar
    [Header("Retroceso de cámara")]
    [Tooltip("Grados que sube la vista con cada disparo")]
    public float cameraKick = 1f;
    [Tooltip("Desviación lateral máxima por disparo (grados, al azar entre -valor y +valor)")]
    [Min(0f)] public float cameraKickSide = 0.3f;
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