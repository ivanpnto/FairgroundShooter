using UnityEngine;

// Giro continuo alrededor de un eje del mundo (noria, tiovivo). Animación procedural sencilla:
// grados por segundo multiplicados por Time.deltaTime, así no depende de los FPS.
public class ConstantRotation : MonoBehaviour
{
    [SerializeField] private Vector3 worldAxis = Vector3.forward;
    [SerializeField] private float degreesPerSecond = 8f;

    private void Update()
    {
        transform.Rotate(worldAxis, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
