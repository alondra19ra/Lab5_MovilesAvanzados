using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// =========================================================
// CUBO DE COLOR
//
// El primer jugador que lo toque (y tenga espacio en su inventario)
// lo recoge: el cubo desaparece y su color pasa al inventario.
// El servidor decide quién lo recoge.
// =========================================================

public class ColorCube : NetworkBehaviour
{
    // Colores posibles. El inventario guarda solo el índice.
    public static readonly Color[] Colors = { Color.red, Color.pink, Color.blue, Color.skyBlue };

    public Vector3 HandSlot = new Vector3(0.6f, 0f, 0.65f);

    // Distancia a la que el jugador recoge el cubo.
    public float pickupRadius = 0.8f;

    private float nextPickupTime;

    public int ColorIndex => (int)(NetworkObjectId % (ulong)Colors.Length);

    public bool IsHeld => transform.parent != null;

    public override void OnNetworkSpawn()
    {
        GetComponent<Renderer>().material.color = Colors[ColorIndex];

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

            // Si el inventario está lleno, el cubo se queda en el suelo.
            if (!player.TryAddToInventory(ColorIndex)) continue;

            // Recogido: el cubo desaparece para todos.
            NetworkObject.Despawn();
            return;
        }
    }

    // Se conservan por si luego quieres agarrar en la mano.
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

        nextPickupTime = Time.time + 1.5f;
    }
}