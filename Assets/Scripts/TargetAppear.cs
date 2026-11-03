using System.Collections;
using UnityEngine;

// Aparición y desaparición de los objetivos (I4.3), como en una caseta real: el recorte sube desde
// detrás del decorado sobre su raíl y, si nadie lo derriba a tiempo, vuelve a bajar.
//
// Solo anima el MODELO HIJO (a través de TargetMovement.SetModelOffset): la raíz la mueve el script
// de movimiento cada frame y lo que escribiéramos en ella se perdería o se acumularía.
//
// Mientras sube, sus colliders están apagados. Si no, el BoxCollider (que está en la raíz, ya en su
// sitio final) se podría acertar en el aire, por encima del arbusto, antes de que se vea el objetivo.
[RequireComponent(typeof(Target))]
public class TargetAppear : MonoBehaviour
{
    [Tooltip("Desde dónde llega: (0,-1,0) = sube desde abajo; (0,1,0) = baja desde arriba (neumático)")]
    [SerializeField] private Vector3 fromDirection = Vector3.down;
    [SerializeField, Min(0f)] private float distance = 1.2f;

    [Header("Aparición")]
    [Tooltip("Desactivar en objetivos que ya se asoman por sí mismos y gestionan su collider (búho)")]
    [SerializeField] private bool animateAppear = true;
    [SerializeField, Min(0.01f)] private float appearDuration = 0.35f;
    [Tooltip("Rebote al llegar arriba (0 = sin rebote). Da el golpe seco del raíl de feria")]
    [SerializeField, Min(0f)] private float overshoot = 1.6f;

    [Header("Desaparición")]
    [SerializeField, Min(0.01f)] private float disappearDuration = 0.3f;

    private Target target;
    private TargetMovement movement;
    private Collider[] colliders;

    private void Awake()
    {
        target = GetComponent<Target>();
        movement = GetComponent<TargetMovement>();
        colliders = GetComponents<Collider>();

        target.ExpireDuration = disappearDuration;   // Target espera esto antes de destruirlo
        target.Expired += OnExpired;
    }

    private void OnDestroy()
    {
        if (target != null)
            target.Expired -= OnExpired;
    }

    private void Start()
    {
        if (animateAppear && movement != null)
            StartCoroutine(Appear());
    }

    private Vector3 Offset(float hidden) => fromDirection.normalized * (distance * hidden);

    private IEnumerator Appear()
    {
        SetColliders(false);
        movement.SetModelOffset(Offset(1f));

        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / appearDuration);
            movement.SetModelOffset(Offset(1f - EaseOutBack(k, overshoot)));
            yield return null;
        }

        movement.SetModelOffset(Vector3.zero);

        // Si en este rato ha expirado o lo han matado (no debería: no tenía collider), no se reactiva
        if (!target.IsDead && !target.IsExpiring)
            SetColliders(true);
    }

    private void OnExpired(Target t)
    {
        StopAllCoroutines();   // por si expira mientras aún estaba subiendo
        if (movement != null)
            StartCoroutine(Disappear());
    }

    private IEnumerator Disappear()
    {
        // Parte de donde esté (puede que no haya terminado de subir)
        float t = 0f;
        while (t < disappearDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / disappearDuration);
            movement.SetModelOffset(Offset(k * k));   // acelera al bajar, como si cayera por el raíl
            yield return null;
        }
    }

    private void SetColliders(bool enabled)
    {
        foreach (Collider col in colliders)
            if (col != null) col.enabled = enabled;
    }

    // Sube pasándose un poco y vuelve: k=0 → 0, k=1 → 1, con un pico de ~1.1 hacia el final
    private static float EaseOutBack(float k, float s)
    {
        float x = k - 1f;
        return 1f + (s + 1f) * x * x * x + s * x * x;
    }
}
