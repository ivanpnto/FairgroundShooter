using UnityEngine;

public abstract class TargetMovement : MonoBehaviour
{
    protected Vector3 startPosition;

    protected virtual void Start()
    {
        startPosition = transform.position;
    }
}