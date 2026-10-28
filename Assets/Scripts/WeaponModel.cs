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

    public Transform Magazine => magazine;
    public Transform Slide => slide;
}
