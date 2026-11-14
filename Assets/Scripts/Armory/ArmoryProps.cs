using UnityEngine;

// Atrezo animado de las tiendas en 3D (Fase 8): el cajón de la caja registradora y el dependiente de cartón
// (el payaso de la armería, el charlatán de la botica). Animación por código, como los objetivos: al comprar,
// el cajón sale de golpe y vuelve y suena el «cha-ching»; el dependiente se mece siempre un poco y, si
// reactOnSale, además se balancea sobre su poste.
public class ArmoryProps : MonoBehaviour
{
    [SerializeField] private Transform drawer;
    [Tooltip("Hacia dónde sale el cajón, en el espacio local de su padre (hacia el jugador)")]
    [SerializeField] private Vector3 drawerOut = new Vector3(0f, 0f, -0.28f);
    [SerializeField] private Transform clownPivot;   // en la base del poste
    [Tooltip("El dependiente celebra la venta (el payaso sí; el charlatán de la botica, no)")]
    [SerializeField] private bool reactOnSale = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip registerClip;

    private Vector3 drawerHome;
    private Quaternion clownHome;
    private float saleAge = -1f;

    private void Awake()
    {
        if (drawer != null) drawerHome = drawer.localPosition;
        if (clownPivot != null) clownHome = clownPivot.localRotation;
    }

    public void PlaySale()
    {
        saleAge = 0f;
        if (audioSource != null && registerClip != null) audioSource.PlayOneShot(registerClip);
    }

    private void Update()
    {
        float idle = Mathf.Sin(Time.time * 1.3f) * 1.2f;   // vaivén leve: el cartón no está del todo quieto
        float sway = 0f, open = 0f;
        if (saleAge >= 0f)
        {
            saleAge += Time.deltaTime;
            // El cajón sale en 0,12 s (al sonar el «ching»), espera y vuelve despacio
            open = saleAge < 0.12f ? saleAge / 0.12f : saleAge < 1.2f ? 1f : Mathf.Clamp01(1f - (saleAge - 1.2f) / 0.5f);
            // El dependiente «celebra»: oscilación amortiguada de hasta 12°
            if (reactOnSale) sway = 12f * Mathf.Sin(saleAge * 9f) * Mathf.Exp(-saleAge * 2.2f);
            if (saleAge > 2.5f) saleAge = -1f;
        }
        if (drawer != null) drawer.localPosition = drawerHome + drawerOut * open;
        if (clownPivot != null) clownPivot.localRotation = clownHome * Quaternion.Euler(0f, 0f, idle + sway);
    }
}
