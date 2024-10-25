using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Utilities;
using System;
using Unity.Netcode.Components;
using Kart;

public class PlayerStats : NetworkBehaviour
{
    public NetworkVariable<int> teamId = new NetworkVariable<int>(0);
    public NetworkVariable<float> health = new NetworkVariable<float>(100f); // Valor inicial de vida
    public NetworkVariable<float> maxHealth = new NetworkVariable<float>(100f);

    // En el futuro se pueden agregar más variables sincronizadas como:
    public NetworkVariable<float> damage = new NetworkVariable<float>(10f);
    public NetworkVariable<float> defense = new NetworkVariable<float>(5f);
    public NetworkVariable<float> mana = new NetworkVariable<float>(50f);

    public override void OnNetworkSpawn()
    {
        // Solo el servidor asigna el teamId
        if (IsServer)
        {
            AssignTeamId();
        }

        // Suscribir cambios de vida solo en el cliente
        if (IsClient)
        {
            health.OnValueChanged += OnHealthChanged;
        }
    }

    // Solo el servidor asigna el teamId
    private void AssignTeamId()
    {
        // Usa el OwnerClientId para diferenciar entre el host y los clientes
        if (OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            teamId.Value = 0;  // Host tiene el teamId 0
        }
        else
        {
            teamId.Value = 1;  // Clientes tienen el teamId 1
        }

        Debug.Log($"Assigned teamId: {teamId.Value} to {gameObject.name} (OwnerClientId: {OwnerClientId})");
    }

    void OnHealthChanged(float oldValue, float newValue)
    {
        // Aquí se podría actualizar la UI de vida en el cliente.
        // Solo se ejecuta en el cliente cuando la vida cambia.
        Debug.Log($"Health updated from {oldValue} to {newValue}");
    }

    public void ApplyDamage(float damage)
    {
    if (!IsServer) return;  // Solo el servidor aplica daño
    float effectiveDamage = Mathf.Max(damage - defense.Value, 0); // Aplica defensa
    Debug.Log($"Applying {effectiveDamage} effective damage (base damage: {damage}, defense: {defense.Value})");
    health.Value -= effectiveDamage;
    if (health.Value <= 0)
    {
        HandleDeath();
    }
    }

    void HandleDeath()
    {
        // Aquí puedes implementar las mecánicas de muerte
        Debug.Log($"{gameObject.name} has died");
    }
}