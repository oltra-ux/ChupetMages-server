using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
    [SerializeField] float speed = 31f;
    [SerializeField] float lifetime = 5f;
    [SerializeField] LayerMask hitMask;
    [SerializeField] float interpolationDelay = 0.1f; // Retraso de interpolación para suavizar movimiento

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool initialized = false;

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
        if (IsServer && ((1 << other.gameObject.layer) & hitMask) != 0)
        {
            OnHitServerRpc(other.transform.position, other.transform.forward);
        }
    }

    [ServerRpc]
    void OnHitServerRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Aquí podrías aplicar efectos o daño
        DestroyProjectileServerRpc();  // Destruir el proyectil solo en el servidor
    }
}
