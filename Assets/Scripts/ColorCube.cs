using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

// =========================================================
// CUBO DE COLOR
//
// - Lo recoge el primer jugador que lo toque (si tiene espacio):
//   desaparece y su color pasa al inventario.
// - Su color se replica (colorIndex) para que, al sacarlo del
//   inventario, el cubo salga con el MISMO color.
// =========================================================

public class ColorCube : NetworkBehaviour
{
    public static readonly Color[] Colors = { Color.red, Color.pink, Color.blue, Color.skyBlue };

    public Vector3 HandSlot = new Vector3(0.6f, 0f, 0.65f);

    // Distancia a la que el jugador recoge el cubo.
    public float pickupRadius = 0.8f;

    // Índice de color replicado. -1 = todavía sin asignar.
    public NetworkVariable<int> colorIndex = new NetworkVariable<int>(-1);

    // Lo rellena el servidor ANTES de Spawn() para forzar un color.
    [HideInInspector] public int pendingColorIndex = -1;

    private float nextPickupTime;

    public int ColorIndex => colorIndex.Value >= 0
        ? colorIndex.Value
        : (int)(NetworkObjectId % (ulong)Colors.Length);

    public bool IsHeld => transform.parent != null;


    public override void OnNetworkSpawn()
    {
        if (IsServer && colorIndex.Value < 0)
        {
            colorIndex.Value = pendingColorIndex >= 0
                ? pendingColorIndex
                : (int)(NetworkObjectId % (ulong)Colors.Length);
        }

        colorIndex.OnValueChanged += OnColorChanged;
        ApplyColor();

        // Margen para que no se recoja antes de caer.
        nextPickupTime = Time.time + 0.5f;
    }

    public override void OnNetworkDespawn()
    {
        colorIndex.OnValueChanged -= OnColorChanged;
    }

    private void OnColorChanged(int previous, int current)
    {
        ApplyColor();
    }

    private void ApplyColor()
    {
        Renderer r = GetComponent<Renderer>();

        if (r != null)
        {
            r.material.color = Colors[ColorIndex % Colors.Length];
        }
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

            // Inventario lleno: el cubo se queda en el suelo.
            if (!player.TryAddToInventory(ColorIndex)) continue;

            NetworkObject.Despawn();
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