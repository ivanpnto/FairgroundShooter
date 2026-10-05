using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private int points = 10;
    [SerializeField] private ScoreManager scoreManager;

    public void Hit()
    {
        Debug.Log("¡Objetivo alcanzado!");

        scoreManager.AddScore(points);

        gameObject.SetActive(false);
    }
}