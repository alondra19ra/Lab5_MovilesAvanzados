using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// JUGADOR
//
// - WASD para moverse.
// - El cofre se abre al TOCARLO (lo gestiona Chest).
// - Los cubos se recogen al tocarlos (lo gestiona ColorCube):
//   desaparecen y su color aparece en el inventario de la pantalla.
//
// El inventario es una NetworkList que SOLO escribe el servidor.
// La pantalla solo lee y dibuja.
// =========================================================

public class PlayerAvatar : NetworkBehaviour
{
    public PlayerInputs2 inputs;
    public float Speed;

    // Cantidad de casillas del inventario.
    public int inventorySize = 5;

    // Guarda el índice de color (ColorCube.Colors) de cada cubo recogido.
    public NetworkList<int> inventory = new NetworkList<int>();

    private Vector2 moveInput;

    // UI (solo existe en el dueño).
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
    }

    private void OnDisable()
    {
        inputs.Player.Move.performed -= OnMove;
        inputs.Player.Move.canceled -= OnMoveCanceled;
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


    // =========================================================
    // INVENTARIO
    // =========================================================

    // Solo el servidor. Devuelve false si no hay espacio.
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

        // Si entras con cosas ya en el inventario, no se dispara el evento.
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
            // Casilla vacía = blanca. Con cubo = color del cubo.
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

            // Columna arriba a la izquierda.
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

        transform.position += dir * Speed * Time.deltaTime;
    }
}