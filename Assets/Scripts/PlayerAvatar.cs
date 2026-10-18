using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// JUGADOR
//
// - WASD para moverse.
// - El cofre se abre solo al TOCARLO (lo gestiona Chest).
// - Los cubos se recogen al tocarlos (lo gestiona ColorCube).
// - E suelta el cubo que lleves en la mano.
// =========================================================

public class PlayerAvatar : NetworkBehaviour
{
    public PlayerInputs2 inputs;
    public float Speed;
    public float dropForce = 3f;

    private Vector2 moveInput;
    private Vector3 lastDirection = Vector3.forward;

    // Solo es fiable en el servidor (donde se decide la recogida).
    public bool HasCube => GetComponentInChildren<ColorCube>() != null;


    private void Awake()
    {
        inputs = new PlayerInputs2();
    }

    private void OnEnable()
    {
        inputs.Enable();
        inputs.Player.Move.performed += OnMove;
        inputs.Player.Move.canceled += OnMoveCanceled;
        inputs.Player.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        inputs.Player.Move.performed -= OnMove;
        inputs.Player.Move.canceled -= OnMoveCanceled;
        inputs.Player.Interact.performed -= OnInteract;
        inputs.Disable();
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !IsSpawned) return;

        DropCubeRpc(lastDirection);
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void DropCubeRpc(Vector3 direction)
    {
        ColorCube cube = GetComponentInChildren<ColorCube>();

        if (cube == null) return;

        // Se suelta hacia donde miraba el jugador, con un pequeño salto.
        cube.ThrowObj(direction.normalized * dropForce + Vector3.up * 2f);
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y);

        if (dir.sqrMagnitude > 0.01f) lastDirection = dir;

        transform.position += dir * Speed * Time.deltaTime;
    }
}