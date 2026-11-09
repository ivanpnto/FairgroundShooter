using UnityEngine;

// Relé de Animation Events (I7.4): Unity solo llama a los eventos de un clip en componentes del MISMO objeto
// que tiene el Animator (Boss_Model), y BossController está en el padre (Boss). Esto los reenvía.
public class BossAnimationEvents : MonoBehaviour
{
    private BossController boss;

    private void Awake() => boss = GetComponentInParent<BossController>();

    // Evento del clip Boss_Throw, en el fotograma del latigazo
    public void OnThrow()
    {
        if (boss != null) boss.OnThrowEvent();
    }
}
