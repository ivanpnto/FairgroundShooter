using UnityEngine;

// Mantiene la orientación con la que empezó aunque el padre gire: las cabinas de la noria
// cuelgan siempre verticales mientras la rueda da vueltas.
public class KeepWorldRotation : MonoBehaviour
{
    private Quaternion initialRotation;

    private void Awake()
    {
        initialRotation = transform.rotation;
    }

    // En LateUpdate: después de que el padre haya girado en su Update
    private void LateUpdate()
    {
        transform.rotation = initialRotation;
    }
}
