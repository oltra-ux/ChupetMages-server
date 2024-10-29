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
    public bool isTestDummy = false;
    public NetworkVariable<int> teamId = new NetworkVariable<int>(0);
    public NetworkVariable<float> health = new NetworkVariable<float>(100f); // Valor inicial de vida
    public NetworkVariable<float> maxHealth = new NetworkVariable<float>(100f);

    // En el futuro se pueden agregar más variables sincronizadas como:
    public NetworkVariable<float> damage = new NetworkVariable<float>(10f);
    public NetworkVariable<float> defense = new NetworkVariable<float>(0f);
    public NetworkVariable<float> mana = new NetworkVariable<float>(50f);
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);  // Nueva variable para sincronizar la muerte
    public GameObject alivePl;
    public GameObject specPl;
    public Collider collider;
    public PlayerShooting shooting;
    public PlayerMovement movement;

    public override void OnNetworkSpawn()
    {
        // Solo el servidor asigna el teamId
        if (IsServer)
        {
            AssignTeamId();
            if(!isTestDummy)
            {
                StartCoroutine(WaitForGameManager());
            }
        }

        // Suscribir cambios de vida solo en el cliente
        if (IsClient)
        {
            health.OnValueChanged += OnHealthChanged;
            isDead.OnValueChanged += OnDeathStateChanged;
        }
    }
    private IEnumerator WaitForGameManager()
    {
        yield return null;  // Espera al siguiente frame
        // Llama a PlayerConnectedServerRpc después de asegurarte que GameManager existe
        GameManager.Instance.PlayerConnectedServerRpc(OwnerClientId);
    }

    // Solo el servidor asigna el teamId
    private void AssignTeamId()
    {
        if(isTestDummy)
        {
            teamId.Value = 99;
        }else
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

        }
        Debug.Log($"Assigned teamId: {teamId.Value} to {gameObject.name} (OwnerClientId: {OwnerClientId})");
    }

    void OnHealthChanged(float oldValue, float newValue)
    {
        // Aquí se podría actualizar la UI de vida en el cliente.
        // Solo se ejecuta en el cliente cuando la vida cambia.
        //Debug.Log($"Health updated from {oldValue} to {newValue}");
    }

    void OnDeathStateChanged(bool oldValue, bool newValue)
    {
        // Aquí se refleja el cambio de estado de vida/muerte en el cliente.
        if (newValue)
        {
            SpectatorModeClientRpc();
        }
    }

    public void ApplyDamage(float damage)
    {
    if (!IsServer) return;  // Solo el servidor aplica daño
    float effectiveDamage = Mathf.Max(damage - defense.Value, 0); // Aplica defensa
    //Debug.Log($"Applying {effectiveDamage} effective damage (base damage: {damage}, defense: {defense.Value})");
    health.Value -= effectiveDamage;
    if (health.Value <= 0 && !isDead.Value)
        {
            // Si la vida es 0 o menor y aún no está muerto, manejamos la muerte
            isDead.Value = true; // Sincroniza la muerte con todos los clientes
            HandleDeath();
        }
    }

    void HandleDeath()
    {
        // Aquí puedes implementar las mecánicas de muerte
        Debug.Log($"{gameObject.name} has died");
        if(IsServer)
        {
            DisableColliderClientRpc();
        }
        if(IsOwner)
        {
            SpectatorModeClientRpc();
        }
    }

    [ClientRpc]
    private void SpectatorModeClientRpc()
    {
        if(IsOwner)
        {
            collider.enabled = false;
            shooting.enabled = false;
            movement.IsSpec = true;
        }
        alivePl.SetActive(false);
    }

    private void SpectatorMode()
    {
        // Aquí llamamos el RPC para que todos los clientes entren en modo espectador
        SpectatorModeClientRpc();
    }
    [ClientRpc]
    void DisableColliderClientRpc()
    {
    // Esto desactivará el collider en todos los clientes
    collider.enabled = false;
    }
}