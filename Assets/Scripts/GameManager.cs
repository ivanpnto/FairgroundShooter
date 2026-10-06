using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Waiting,
        Playing,
        GameOver
    }

    public GameState CurrentState { get; private set; } = GameState.Waiting;

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        if (CurrentState != GameState.Waiting)
            return;

        CurrentState = GameState.Playing;

        Debug.Log($"Game state: {CurrentState}");
    }

    public void EndGame()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.GameOver;

        Debug.Log($"Game state: {CurrentState}");
    }
}