using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Utilities;
using System;
using Unity.Netcode.Components;
using Kart;
using UnityEngine.SceneManagement;

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
    public NetworkVariable<bool> canEditWands = new NetworkVariable<bool>(false); // Para editar varitas

    public int requiredPlayers = 2;
    
    // Array de posiciones de spawn para los equipos
    public Transform[] teamSpawns; // Asignar desde el editor

    private void Awake()
    {
    if (Instance == null)
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("GameManager creado y marcado para no destruirse.");
    }
    else
    {
        Debug.LogWarning("Intento de crear un segundo GameManager. Destruyendo el duplicado.");
        Destroy(gameObject);
    }
    }

    private void Start()
    {
        if (IsServer)
        {
            currentState.Value = GameState.GameWait;
            stateTimer.Value = 30f; // 30 segundos de espera por jugadores
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
                    UpdateRoundStarting();
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
            Debug.Log("Jugadores conectados. Iniciando juego.");
            currentState.Value = GameState.GameStarting;
            stateTimer.Value = 5f; // Cuenta regresiva para iniciar
        }
    }

    private void UpdateGamePreRound()
    {
        // Aquí podría agregarse la lógica de la prueba de hechizos si fuera necesario.
        // Por ahora no hay interacción en el PreRound más allá del movimiento a las posiciones de spawn.
    }

    private void UpdateRoundStarting()
    {
        // Desactivar la edición de varitas
        canEditWands.Value = false;
    }

    private void UpdateGameRound()
    {
        // Lógica del estado de la batalla
    }

    private void HandleStateTransition()
    {
    switch (currentState.Value)
    {
        case GameState.GameWait:
            currentState.Value = GameState.GameStarting;
            stateTimer.Value = 5f;
            break;
        case GameState.GameStarting:
            currentState.Value = GameState.GamePreRound;
            stateTimer.Value = 40f;
            StartPreRound();
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
        default:
            // Este caso es el problema: si no hay un estado válido, vuelve a GameWait
            Debug.LogWarning("Estado desconocido, volviendo a GameWait.");
            currentState.Value = GameState.GameWait;
            stateTimer.Value = 30f;
            break;
    }
    }

    private void StartPreRound()
    {
        Debug.Log("Iniciando PreRound. Moviendo jugadores a sus casas.");
        canEditWands.Value = true;

        // Suscribirse al evento de carga de escena
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneLoaded;

        // Cambia a la escena "hall"
        NetworkManager.Singleton.SceneManager.LoadScene("hall", LoadSceneMode.Single);
    }

    private void OnSceneLoaded(SceneEvent sceneEvent)
    {
    if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == "hall")
    {
        // Desuscribirse del evento para evitar duplicaciones
        NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneLoaded;

        Debug.Log("Escena 'hall' cargada. Moviendo jugadores a las casas.");
        
        // Procede con la lógica de movimiento de jugadores a sus posiciones
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObject = player.PlayerObject;
            if (playerObject != null)
            {
                var playerStats = playerObject.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    SpawnPlayerInHouse();
                }
            }
        }
    }
    else
    {
        Debug.LogWarning("Evento de escena no es LoadComplete o no es la escena correcta.");
    }
    }

    // Spawnea a un jugador en la casa correspondiente a su equipo
    private void SpawnPlayerInHouse()
    {
        // Llamar al método para buscar y asignar los spawnpoints
        FindSpawnPoints();

        // Mover a los jugadores a sus posiciones
        MovePlayersToSpawn();
    }

    

    private void FindSpawnPoints()
    {
        // Buscar todos los spawnpoints por nombre o tag en la escena actual
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint"); // Asegúrate de que los spawn points tengan la tag "SpawnPoint"
        
        teamSpawns = new Transform[spawnPoints.Length];

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            teamSpawns[i] = spawnPoints[i].transform;
        }
    }


    private void MovePlayersToSpawn()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerStats playerStats = client.PlayerObject.GetComponent<PlayerStats>();

            // Asigna una posición de spawn según el teamId
            int teamId = playerStats.teamId.Value;

            // Mover al jugador a su posición de spawn (si el número de equipos es menor o igual al tamaño de teamSpawns)
            if (teamId < teamSpawns.Length)
            {
                Transform spawnPoint = teamSpawns[teamId];
                playerStats.transform.position = spawnPoint.position;
                playerStats.transform.rotation = spawnPoint.rotation;
            }
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