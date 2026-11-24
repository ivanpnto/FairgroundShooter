using TMPro;
using UnityEngine;

// Un hueco de la botica (Fase 8): un frasco de «tónico milagroso» por mejora, o el billete dorado del nivel 4.
//
// El nivel se lee sin texto, como las bombillas de la armería:
//  · frasco alto (mejoras de 3 niveles): el líquido sube por tercios hasta las marcas doradas;
//  · matraz redondo (mejoras de 1 nivel): vacío o lleno;
//  · al máximo: lacre dorado en el corcho y la etiqueta de la balda en dorado;
//  · billete: apagado dentro de la vitrina; comprado, se enciende y sale flotando por encima.
//
// El líquido es una malla aparte con el pivote en su base: llenar es escalarla en Y. No hace falta un shader:
// el líquido es opaco y el cristal transparente, y URP pinta siempre los opacos antes que los transparentes, así
// que el líquido se ve dentro del frasco sin pelearse con él por el orden.
//
// Solo pinta. El nivel comprado lo guarda ProfileManager; BoticaController llama a SetLevel cuando cambia.
public class BoticaSlot : ShopSlot
{
    public enum Kind { Bottle, Ticket }

    [SerializeField] private UpgradeData upgrade;
    [SerializeField] private Kind kind = Kind.Bottle;

    [Header("Frasco")]
    [Tooltip("Pivote del líquido, en su base: se escala en Y de 0 (vacío) a 1 (lleno)")]
    [SerializeField] private Transform liquid;
    [Tooltip("Lacre dorado del corcho: solo con la mejora al máximo")]
    [SerializeField] private GameObject seal;
    [Tooltip("Texto de la etiqueta de papel de la balda")]
    [SerializeField] private TMP_Text plate;
    [SerializeField] private Color plateColor = new Color(0.25f, 0.12f, 0.05f);
    [SerializeField] private Color plateMaxColor = new Color(0.62f, 0.4f, 0.04f);
    [SerializeField] private ParticleSystem bubbles;   // dentro del líquido mientras sube
    [SerializeField] private ParticleSystem puff;      // nubecilla en la boca al terminar de llenarse
    [SerializeField] private float fillTime = 0.7f;

    [Header("Billete")]
    [SerializeField] private Renderer ticket;
    [SerializeField] private Material ticketOff;
    [SerializeField] private Material ticketOn;
    [SerializeField] private Light ticketLight;
    [SerializeField] private float ticketRise = 0.24f;   // más alto se metía delante de la balda de los frascos
    [SerializeField] private float ticketLightIntensity = 1.2f;

    private Renderer liquidRenderer;
    private float shownFill, fromFill, toFill;
    private float fillAge = -1f;
    private float rise, riseTarget;

    public UpgradeData Upgrade => upgrade;
    public bool IsTicket => kind == Kind.Ticket;
    public int Level { get; private set; }
    public bool IsMaxed => upgrade != null && Level >= upgrade.maxLevel;
    public float FillTime => fillTime;

    protected override void Awake()
    {
        base.Awake();
        if (liquid != null) liquidRenderer = liquid.GetComponentInChildren<Renderer>(true);
    }

    // animate: al comprar el líquido sube con burbujas (o el billete sale de la vitrina). Al abrir, sin animación
    public void SetLevel(int level, bool animate)
    {
        Level = level;
        int max = upgrade != null ? Mathf.Max(1, upgrade.maxLevel) : 1;
        float fill = Mathf.Clamp01(level / (float)max);

        if (animate && fill > shownFill)
        {
            fromFill = shownFill;
            toFill = fill;
            fillAge = 0f;
            if (bubbles != null && !IsTicket) bubbles.Play();
        }
        else
        {
            shownFill = toFill = fill;
            fillAge = -1f;
            ApplyFill(fill);
        }

        // El lacre se pone al terminar de llenarse, no antes
        if (seal != null) seal.SetActive(IsMaxed && fillAge < 0f);
        if (plate != null && upgrade != null)
        {
            plate.text = PlateName(upgrade.displayName);
            plate.color = IsMaxed ? plateMaxColor : plateColor;
            plate.fontStyle = IsMaxed ? FontStyles.Bold : FontStyles.Normal;
        }

        if (IsTicket)
        {
            bool owned = level > 0;
            if (ticket != null) ticket.sharedMaterial = owned ? ticketOn : ticketOff;
            if (ticketLight != null) ticketLight.enabled = owned;
            riseTarget = owned ? 1f : 0f;
            if (!animate) rise = riseTarget;
        }
    }

    private void ApplyFill(float fill)
    {
        if (liquid == null) return;
        liquid.localScale = new Vector3(1f, Mathf.Max(0.001f, fill), 1f);
        if (liquidRenderer != null) liquidRenderer.enabled = fill > 0.001f;
    }

    protected override void Update()
    {
        if (fillAge >= 0f)
        {
            fillAge += Time.deltaTime;
            float t = Mathf.Clamp01(fillAge / fillTime);
            // Sube rápido al principio y se asienta al final, como un chorro que se corta
            shownFill = Mathf.Lerp(fromFill, toFill, 1f - (1f - t) * (1f - t));
            ApplyFill(shownFill);
            if (t >= 1f)
            {
                fillAge = -1f;
                if (bubbles != null) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (puff != null) puff.Play();
                if (seal != null) seal.SetActive(IsMaxed);
            }
        }

        if (IsTicket)
        {
            rise = Mathf.MoveTowards(rise, riseTarget, Time.deltaTime / 0.9f);
            float e = rise * rise * (3f - 2f * rise);
            float bob = Mathf.Sin(Time.time * 2.1f) * 0.012f * e;   // flotando, sube y baja un poco
            RestOffset = Vector3.up * (ticketRise * e + bob);
            if (ticketLight != null && ticketLight.enabled) ticketLight.intensity = ticketLightIntensity * (0.85f + 0.15f * Mathf.Sin(Time.time * 5f));
        }

        base.Update();
    }

    // Nombre corto para la etiqueta de papel (24/11): en una sola línea, los nombres largos se cortan antes de « de »
    // («Multiplicador de puntos» → MULTIPLICADOR). En dos líneas, la segunda quedaba bajo el canto de la balda.
    // El cartel al pasar el ratón y la ficha siguen diciendo el nombre entero
    public static string PlateName(string displayName)
    {
        string name = displayName.ToUpper();
        int cut = name.IndexOf(" DE ", System.StringComparison.Ordinal);
        return name.Length > 16 && cut > 0 ? name.Substring(0, cut) : name;
    }
}
