using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// =========================================================
// CUBO DE COLOR
//
// Lo recoge el primer jugador que lo toque (si tiene la mano libre).
// El servidor decide quién lo recoge; los clientes solo ven el resultado.
// =========================================================

public class ColorCube : NetworkBehaviour
{
    private Color[] COLORS = { Color.red, Color.pink, Color.blue, Color.skyBlue };

    public Vector3 HandSlot = new Vector3(0.6f, 0f, 0.65f);

    // Distancia a la que el jugador recoge el cubo.
    public float pickupRadius = 0.8f;

    private Vector3 spawnPoint;
    private float nextPickupTime;

    // Un cubo en la mano tiene al jugador como padre.
    public bool IsHeld => transform.parent != null;

    public override void OnNetworkSpawn()
    {
        GetComponent<Renderer>().material.color = COLORS[NetworkObjectId % 4];
        spawnPoint = transform.position;

        // Un poco de margen para que no se recoja antes de caer del cofre.
        nextPickupTime = Time.time + 0.5f;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
        if (IsHeld || Time.time < nextPickupTime) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerAvatar player = hits[i].GetComponentInParent<PlayerAvatar>();

            if (player == null || !player.IsSpawned) continue;
            if (player.HasCube) continue; // ya lleva uno

            Grab(player.NetworkObject);
            return;
        }
    }

    public void Grab(NetworkObject user)
    {
        GetComponent<NetworkRigidbody>().SetIsKinematic(true);
        NetworkObject.TrySetParent(user, false);
        transform.localPosition = HandSlot;
        transform.localRotation = Quaternion.identity;
    }

    public void ThrowObj(Vector3 throwSpeed)
    {
        NetworkObject.TryRemoveParent(true);

        GetComponent<NetworkRigidbody>().SetIsKinematic(false);
        GetComponent<NetworkRigidbody>().SetLinearVelocity(throwSpeed);

        // Evita que se recoja otra vez de inmediato.
        nextPickupTime = Time.time + 1.5f;
    }
}