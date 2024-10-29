using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Utilities;
using System;
using Unity.Netcode.Components;
using Kart;

public enum GameState
{
    GameWait,       // Esperando a los jugadores
    GameStarting,   // Contador antes de que comience la ronda
    GamePreRound,   // Prueba de hechizos antes de la ronda
    RoundStarting,  // Contador antes de la pelea
    GameRound,      // Batalla entre equipos
    RoundEnding,
    GameEnded       // Fin del juego
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    
    public NetworkVariable<GameState> currentState = new NetworkVariable<GameState>(GameState.GameWait);
    public NetworkVariable<float> stateTimer = new NetworkVariable<float>();
    public NetworkVariable<int> connectedPlayers = new NetworkVariable<int>(0); // Número de jugadores conectados

    public int requiredPlayers = 2;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Para asegurarse de que no haya más de un GameManager
        }
    }
    private void Start()
    {
        if (IsServer)
        {
            currentState.Value = GameState.GameWait;
            stateTimer.Value = 30f; // 1 minuto de espera por jugadores
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            // Actualizar temporizador en el servidor
            if (stateTimer.Value > 0)
            {
                stateTimer.Value -= Time.deltaTime;
            }
            else
            {
                HandleStateTransition();
            }

            switch(currentState.Value)
            {
                case GameState.GameWait:
                UpdateGameWait();
                    break;
                case GameState.GameStarting:
                    break;
                case GameState.GamePreRound:
                UpdateGamePreRound();
                    break;
                case GameState.RoundStarting:
                    break;
                case GameState.GameRound:
                UpdateGameRound();
                    break;
                case GameState.GameEnded:
                    break;
            }
        }
    }

    private void UpdateGameWait()
    {
        if (connectedPlayers.Value >= requiredPlayers)
        {
            Debug.Log("jugadores conectados");
            currentState.Value = GameState.GameStarting;
            stateTimer.Value = 5f;
        }
    }

    private void UpdateGamePreRound()
    {

    }

    private void UpdateGameRound()
    {

    }

    private void HandleStateTransition()
    {
        switch (currentState.Value)
        {
            case GameState.GameWait:
                currentState.Value = GameState.GameStarting;
                break;
            case GameState.GameStarting:
                currentState.Value = GameState.GamePreRound;
                stateTimer.Value = 40f;
                break;
            case GameState.GamePreRound:
                currentState.Value = GameState.RoundStarting;
                stateTimer.Value = 5f;
                break;
            case GameState.RoundStarting:
                currentState.Value = GameState.GameRound;
                stateTimer.Value = 120f; // 2 minutos de ronda
                break;
            case GameState.GameRound:
                HandleEndRound();
                break;
            case GameState.GameEnded:
                // Fin del juego, volver a la lobby o reiniciar
                break;
        }
    }

    private void HandleEndRound()
    {
        // Lógica para manejar el fin de la ronda, evaluar el daño a las torres
        // y actualizar la vida de las torres de las parejas.
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerConnectedServerRpc(ulong playerId)
    {
        connectedPlayers.Value++;
        Debug.Log($"Jugador conectado: {playerId}. Total: {connectedPlayers.Value}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerDisconnectedServerRpc(ulong playerId)
    {
        connectedPlayers.Value--;
        Debug.Log($"Jugador desconectado: {playerId}. Total: {connectedPlayers.Value}");
    }
}