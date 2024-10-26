using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;
using Utilities;
using System;
using Unity.Netcode.Components;
using Kart;

public class Projectile : NetworkBehaviour
{
    [SerializeField]private PlayerShooting playerSht;
    [SerializeField]private float damageAmount;  // Variable para almacenar el daño del proyectil
    [SerializeField]private int teamId;  // Team ID del jugador que disparó el proyectil
    [SerializeField] float speed = 31f;
    [SerializeField] float lifetime = 5f;
    [SerializeField] LayerMask hitMask;
    [SerializeField] float interpolationDelay = 0.1f; // Retraso de interpolación para suavizar movimiento

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialized = false;
    
    public void Initialize(PlayerShooting player, float damage, int shooterTeamId)
    {
        playerSht = player;
        damageAmount = damage;
        teamId = shooterTeamId;
    }

    void Start()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifetime);  // Destruye el proyectil después de un tiempo si es servidor
        }
    }

    void Update()
    {
        if (IsOwner || IsServer)
        {
            MoveProjectile();
        }
        else
        {
            InterpolatePosition();
        }
    }

    void MoveProjectile()
    {
        Vector3 newPosition = transform.position + transform.forward * speed * Time.deltaTime;

        // Enviar la nueva posición y rotación al cliente
        if (IsServer)
        {
            UpdateClientPositionAndRotationServerRpc(newPosition, transform.rotation);
        }

        transform.position = newPosition;
    }

    // Interpolar la posición y la rotación recibida del servidor
    void InterpolatePosition()
    {
        if (initialized)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, interpolationDelay);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, interpolationDelay);
        }
    }

    // Solo el servidor puede destruir el proyectil
    [ServerRpc]
    void DestroyProjectileServerRpc()
    {
        Destroy(gameObject);
    }

    // Enviar la nueva posición y rotación a los clientes desde el servidor
    [ServerRpc]
    void UpdateClientPositionAndRotationServerRpc(Vector3 newPosition, Quaternion newRotation)
    {
        UpdateClientPositionAndRotationClientRpc(newPosition, newRotation);
    }

    // Actualizar la posición y rotación en los clientes
    [ClientRpc]
    void UpdateClientPositionAndRotationClientRpc(Vector3 newPosition, Quaternion newRotation)
    {
        targetPosition = newPosition;
        targetRotation = newRotation;
        initialized = true;
    }

    // Manejar colisiones (solo en el servidor)
    void OnTriggerEnter(Collider other)
    {
        if (IsServer && ((1 << other.gameObject.layer) & hitMask) != 0 && IsEnemy(other))
        {
            PlayerStats targetStats = other.GetComponent<PlayerStats>();
            if (targetStats != null)
            {
                playerSht.hitsound.Play();
                ShowDamageTextClientRpc(other.transform.position, damageAmount);
                ApplyDamageServerRpc(targetStats.NetworkObjectId, damageAmount);
            }

            DestroyProjectileServerRpc();  // Destruir el proyectil solo en el servidor
        }
    }
    [ClientRpc]
    void ShowDamageTextClientRpc(Vector3 position, float damage)
    {
        // Mostrar el texto flotante sobre el objetivo
        ShowFloatingDamageText(position, damage);
    }

    void ShowFloatingDamageText(Vector3 position, float damage)
    {
        // Crear el texto flotante en la posición del impacto
        GameObject floatingText = Instantiate(playerSht.floatingTextPrefab, position, Quaternion.identity);
        floatingText.GetComponent<dmgText>().StartText(damage);
    }

    [ServerRpc(RequireOwnership = false)]
    void ApplyDamageServerRpc(ulong targetId, float damage)
    {
        var target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId];
        if (target != null)
        {
            PlayerStats targetStats = target.GetComponent<PlayerStats>();
            if (targetStats != null)
            {
                Debug.Log($"{damage} damage");
                targetStats.ApplyDamage(damage);
            }else{
                Debug.LogError($"targetStats is null. No damage applied.");
            }
        }
    }

    bool IsEnemy(Collider targetCollider)
    {
        PlayerStats targetStats = targetCollider.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            return targetStats.teamId.Value != teamId;
        }
        return false;
    }

    [ServerRpc]
    void OnHitServerRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Aquí podrías aplicar efectos o daño
        DestroyProjectileServerRpc();  // Destruir el proyectil solo en el servidor
    }
}
