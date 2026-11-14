using System;
using System.Collections;
using UnityEngine;

// Transición escenificada entre niveles (Fase 8): el puesto «cierra» para cambiar el decorado.
//
//  t (s)   qué pasa
//  0       el telón se cierra (CurtainController lo hace solo fuera de Playing) y las luces bajan a penumbra
//  0,8     telón cerrado: fuera las dianas y aparece el cartel
//  1,0     detrás del telón cambia el nivel: decorado, iluminación y música (swapLevel)
//  1–2,6   dos o tres bultos en el telón, con golpes: alguien cambia el decorado
//  2,8     el cartel sube fuera de pantalla
//  3,15    3 · 2 · 1 (cada 0,6 s); en el 1 se abre el telón y vuelven las luces
//  4,95    ¡FUEGO!: empieza el nivel (beginPlay)
//
// Solo escenifica. El estado de la partida sigue siendo de GameManager, que pasa las dos acciones que cambian
// el juego (swapLevel y beginPlay) para no exponer nada más. Va en el objeto GameManager.
public class LevelTransitionSequence : MonoBehaviour
{
    [SerializeField] private LevelTransitionUI ui;
    [SerializeField] private StageDimmer dimmer;
    [SerializeField] private CurtainController curtain;

    [Header("Tiempos (s)")]
    [SerializeField] private float curtainClose = 0.8f;   // lo que tarda el clip Curtains_Closing
    [SerializeField] private float swapDelay = 0.2f;      // del cartel al cambio de nivel
    [SerializeField] private float cardTime = 1.8f;       // desde el cambio hasta que el cartel se va
    [SerializeField] private float cardExit = 0.35f;
    [SerializeField] private float countStep = 0.6f;
    [SerializeField, Range(0f, 1f)] private float closedLight = 0.3f;

    public bool IsReady => ui != null;

    public IEnumerator Play(string title, string next, string bonus, Action swapLevel, Action beginPlay)
    {
        if (dimmer != null) dimmer.Target = closedLight;
        yield return new WaitForSeconds(curtainClose);

        ui.ShowCard(title, next, bonus);
        yield return new WaitForSeconds(swapDelay);

        swapLevel();
        Coroutine bumps = StartCoroutine(Bumps(cardTime - 0.2f));
        yield return new WaitForSeconds(cardTime);
        StopCoroutine(bumps);

        ui.HideCard();
        yield return new WaitForSeconds(cardExit);

        for (int n = 3; n >= 1; n--)
        {
            ui.ShowCount(n.ToString(), false);
            if (n == 1)
            {
                // El telón tarda 0,9 s en abrirse: empieza con el 1 para estar casi abierto en el ¡FUEGO!
                if (curtain != null) curtain.OpenOverride = true;
                if (dimmer != null) dimmer.Target = 1f;
            }
            yield return new WaitForSeconds(countStep);
        }

        ui.ShowCount("¡FUEGO!", true);
        if (curtain != null) curtain.OpenOverride = null;   // en Playing ya está abierto por sí solo
        beginPlay();
    }

    // Partida nueva o fin a mitad de la transición: escenario y cartel como al principio
    public void ResetStage()
    {
        StopAllCoroutines();   // los bultos corren en este componente, no en GameManager
        if (ui != null) ui.HideAll();
        if (dimmer != null) dimmer.ResetToFull();
        if (curtain != null) curtain.OpenOverride = null;
    }

    // Empujones sueltos en una u otra mitad, nunca dos seguidos en la misma: así parece que alguien va y viene
    private IEnumerator Bumps(float window)
    {
        if (curtain == null) yield break;
        int count = UnityEngine.Random.Range(2, 4);
        int side = UnityEngine.Random.Range(0, 2);
        float slot = window / count;
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(slot * 0.4f, slot * 0.9f));
            curtain.Bump(side, UnityEngine.Random.Range(0.7f, 1f));
            side = 1 - side;
            yield return new WaitForSeconds(slot * 0.1f);
        }
    }
}
