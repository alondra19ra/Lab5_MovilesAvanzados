using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// JUGADOR
//
// - WASD para moverse.
// - El cofre se abre al TOCARLO (Chest).
// - Los cubos se recogen al tocarlos (ColorCube) y van al inventario.
// - E: saca el último cubo del inventario, el player lo agarra
//   y lo tira hacia donde se movió por última vez.
//
// El inventario es una NetworkList que SOLO escribe el servidor.
// =========================================================

public class PlayerAvatar : NetworkBehaviour
{
    public PlayerInputs2 inputs;
    public float Speed;

    // Prefab del cubo (el mismo que usa el cofre: YY).
    public NetworkObject cubePrefab;

    public int inventorySize = 5;
    public float throwForce = 6f;
    public float holdTime = 0.3f; // tiempo que lo tiene en la mano antes de tirarlo

    public NetworkList<int> inventory = new NetworkList<int>();

    private Vector2 moveInput;
    private Vector3 lastDirection = Vector3.forward;
    private bool isThrowing;

    private GameObject inventoryCanvas;
    private Image[] slotImages;


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

        // El cliente solo PIDE tirar. El servidor decide.
        ThrowFromInventoryRpc(lastDirection);
    }


    // =========================================================
    // SACAR DEL INVENTARIO Y TIRAR (servidor)
    // =========================================================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ThrowFromInventoryRpc(Vector3 direction)
    {
        if (isThrowing) return;
        if (inventory.Count == 0) return;
        if (cubePrefab == null)
        {
            Debug.LogWarning("PlayerAvatar: falta asignar Cube Prefab.");
            return;
        }

        int last = inventory.Count - 1;
        int colorIdx = inventory[last];

        // Sale del inventario (la casilla vuelve a ponerse blanca).
        inventory.RemoveAt(last);

        StartCoroutine(GrabAndThrow(colorIdx, direction));
    }

    private IEnumerator GrabAndThrow(int colorIdx, Vector3 direction)
    {
        isThrowing = true;

        // 1. Aparece en la mano con su color.
        ColorCube prefabCube = cubePrefab.GetComponent<ColorCube>();
        Vector3 handPosition = transform.position + (prefabCube != null ? prefabCube.HandSlot : Vector3.zero);

        NetworkObject cubeObject = Instantiate(cubePrefab, handPosition, Quaternion.identity);
        ColorCube cube = cubeObject.GetComponent<ColorCube>();
        cube.pendingColorIndex = colorIdx;

        cubeObject.Spawn();

        // 2. El player lo agarra.
        cube.Grab(NetworkObject);

        yield return new WaitForSeconds(holdTime);

        // 3. Lo tira.
        if (cubeObject != null && cubeObject.IsSpawned)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;

            cube.ThrowObj(flat.normalized * throwForce + Vector3.up * 2f);
        }

        isThrowing = false;
    }


    // =========================================================
    // INVENTARIO
    // =========================================================

    public bool TryAddToInventory(int colorIndex)
    {
        if (!IsServer) return false;
        if (inventory.Count >= inventorySize) return false;

        inventory.Add(colorIndex);
        return true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        BuildInventoryUI();

        inventory.OnListChanged += OnInventoryChanged;
        RefreshInventoryUI();
    }

    public override void OnNetworkDespawn()
    {
        inventory.OnListChanged -= OnInventoryChanged;

        if (inventoryCanvas != null) Destroy(inventoryCanvas);
    }

    private void OnInventoryChanged(NetworkListEvent<int> changeEvent)
    {
        RefreshInventoryUI();
    }

    private void RefreshInventoryUI()
    {
        if (slotImages == null) return;

        for (int i = 0; i < slotImages.Length; i++)
        {
            slotImages[i].color = i < inventory.Count
                ? ColorCube.Colors[inventory[i] % ColorCube.Colors.Length]
                : Color.white;
        }
    }

    private void BuildInventoryUI()
    {
        const float slotSize = 70f;
        const float gap = 12f;
        const float margin = 30f;

        inventoryCanvas = new GameObject("InventoryCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        Canvas canvas = inventoryCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = inventoryCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        slotImages = new Image[inventorySize];

        for (int i = 0; i < inventorySize; i++)
        {
            GameObject slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Outline));

            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.SetParent(inventoryCanvas.transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(
                margin + slotSize * 0.5f,
                -(margin + slotSize * 0.5f + i * (slotSize + gap))
            );

            Image image = slot.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;

            Outline outline = slot.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            slotImages[i] = image;
        }
    }


    private void Update()
    {
        if (!IsOwner) return;

        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y);

        if (dir.sqrMagnitude > 0.01f) lastDirection = dir;

        transform.position += dir * Speed * Time.deltaTime;
    }
}