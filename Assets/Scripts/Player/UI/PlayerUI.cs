using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerUI : NetworkBehaviour
{
    [SerializeField] private PlayerStats playerStats;  // Referencia al script de estadísticas del jugador
    [SerializeField] private Text healthText;  // Texto que mostrará la vida en la UI
    [SerializeField] private GameObject canvas;
    [SerializeField] private Text stateText;
    [SerializeField] private Text timerText;

    public override void OnNetworkSpawn()
    {
        if(!IsOwner)
        {
            canvas.SetActive(false);
        }
    }

    void Update()
    {
        if (playerStats != null)
        {
            healthText.text = $"hp {playerStats.health.Value} / {playerStats.maxHealth.Value}";
        }
        stateText.text = GameManager.Instance.currentState.Value.ToString();
        timerText.text = Mathf.Ceil(GameManager.Instance.stateTimer.Value).ToString();
    }
}