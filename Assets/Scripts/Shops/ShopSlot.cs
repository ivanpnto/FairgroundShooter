using UnityEngine;

// Base de lo que se vende en una carpa (Fase 8): el arma de la armería o el frasco de la botica.
//
// Hace lo que es igual en las dos: al pasar el ratón el objeto se levanta y gira un poco en su soporte, y da el
// punto donde va la etiqueta flotante. Qué significa cada estado (bombilla, nivel del líquido…) lo pinta la hija.
public abstract class ShopSlot : MonoBehaviour
{
    [Tooltip("Pivote del objeto: es lo que se levanta y gira al pasar el ratón")]
    [SerializeField] protected Transform holder;

    [Header("Paso del ratón")]
    [SerializeField] private float liftHeight = 0.07f;
    [SerializeField] private float liftTurn = 10f;   // grados alrededor del eje vertical
    [Tooltip("Altura de la etiqueta flotante sobre el pivote")]
    [SerializeField] private float labelHeight = 0.4f;

    private Vector3 holderHome;
    private Quaternion holderRot;
    private bool hovered;
    private float lift;

    // Desplazamiento del pivote en reposo, además del de paso del ratón (el billete de la botica flota al comprarlo)
    protected Vector3 RestOffset { get; set; }

    // Punto encima del objeto para la etiqueta flotante
    public Vector3 LabelPoint => holder != null ? holder.position + Vector3.up * labelHeight : transform.position;

    protected virtual void Awake()
    {
        if (holder == null) return;
        holderHome = holder.localPosition;
        holderRot = holder.localRotation;
    }

    public void SetHovered(bool value) => hovered = value;

    protected virtual void Update()
    {
        if (holder == null) return;
        lift = Mathf.MoveTowards(lift, hovered ? 1f : 0f, Time.deltaTime * 6f);
        float e = lift * lift * (3f - 2f * lift);   // suavizado en los dos extremos
        holder.localPosition = holderHome + RestOffset + Vector3.up * (liftHeight * e);
        holder.localRotation = holderRot * Quaternion.Euler(0f, liftTurn * e, 0f);
    }
}
