using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;  // Referencia al script de estadísticas del jugador
    [SerializeField] private Text healthText;  // Texto que mostrará la vida en la UI

    void Update()
    {
        if (playerStats != null)
        {
            healthText.text = $"hp {playerStats.health.Value} / {playerStats.maxHealth.Value}";
        }
    }
}