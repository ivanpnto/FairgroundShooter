using UnityEngine;

/// <summary>
/// Va en la raíz de cada prefab de arma (Assets/Prefabs/Weapons/).
/// No tiene lógica: solo señala qué piezas del modelo se animan.
/// Por qué existe: cada modelo de PolyOne nombra y anida sus piezas de forma distinta,
/// así que en lugar de buscarlas por nombre desde código, se asignan una vez en el prefab.
/// </summary>
public class WeaponModel : MonoBehaviour
{
    [Tooltip("Pieza del cargador (opcional)")]
    [SerializeField] private Transform magazine;
    [Tooltip("Corredera (opcional)")]
    [SerializeField] private Transform slide;
    // Hijo vacío añadido en la variante (no existe en PolyOne): la posición sale de la
    // cara delantera de la malla, y su +Z apunta en la dirección del disparo
    [Tooltip("Punta del cañón, con su eje Z local hacia delante (opcional: sin él no hay fogonazo)")]
    [SerializeField] private Transform muzzle;

    public Transform Magazine => magazine;
    public Transform Slide => slide;
    public Transform Muzzle => muzzle;
}
