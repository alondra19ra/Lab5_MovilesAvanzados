using TMPro;
using Unity.Netcode;
using UnityEngine;

// =========================================================
// COFRE
//
// - El jugador lo TOCA (se acerca) y empieza la cuenta atrás.
// - El tiempo restante aparece encima del cofre (lo ven todos).
// - Si el jugador se aleja, la cuenta se cancela.
// - Al llegar a 0 el cofre se abre, cambia de color y suelta cubos.
//
// El servidor decide todo. Los clientes solo leen Progress/Opened.
// =========================================================

public class Chest : NetworkBehaviour
{
    public NetworkObject cubePrefab;
    public float TimeToOpenChest = 4f;

    // Distancia (en horizontal) a la que se considera que el jugador toca el cofre.
    public float touchDistance = 0.9f;

    public int cubesToSpawn = 5;

    public NetworkVariable<ulong> UserOpening = new NetworkVariable<ulong>(ulong.MaxValue);
    public NetworkVariable<float> Progress = new(0f);
    public NetworkVariable<bool> Opened = new(false);

    private static readonly Color OpenedColor = new Color(0.35f, 0.25f, 0.1f, 1f);

    private Renderer chestRenderer;
    private Collider chestCollider;
    private Color closedColor = Color.white;
    private TextMeshPro timerText;
    private float showOpenedUntil;


    private void Awake()
    {
        chestRenderer = GetComponentInChildren<Renderer>();
        chestCollider = GetComponentInChildren<Collider>();

        if (chestRenderer != null)
        {
            closedColor = chestRenderer.material.color;
        }
    }

    public override void OnNetworkSpawn()
    {
        BuildTimerText();

        Opened.OnValueChanged += OnOpenedChanged;
        ApplyOpenedVisual();
    }

    public override void OnNetworkDespawn()
    {
        Opened.OnValueChanged -= OnOpenedChanged;
        DestroyTimerText();
    }

    public override void OnDestroy()
    {
        DestroyTimerText();
        base.OnDestroy();
    }


    // =========================================================
    // LÓGICA (solo servidor)
    // =========================================================

    private void Update()
    {
        if (IsServer) ServerUpdate();
    }

    private void ServerUpdate()
    {
        if (Opened.Value) return;

        // Cofre libre: el primer jugador que lo toque empieza a abrirlo.
        if (UserOpening.Value == ulong.MaxValue)
        {
            PlayerAvatar toucher = FindTouchingPlayer(ulong.MaxValue);

            if (toucher != null)
            {
                UserOpening.Value = toucher.OwnerClientId;
                Progress.Value = 0f;
            }

            return;
        }

        // Si quien lo abría se aleja, se cancela.
        if (FindTouchingPlayer(UserOpening.Value) == null)
        {
            UserOpening.Value = ulong.MaxValue;
            Progress.Value = 0f;
            return;
        }

        Progress.Value += Time.deltaTime / Mathf.Max(0.01f, TimeToOpenChest);

        if (Progress.Value >= 1f) OpenChest();
    }

    // Devuelve un jugador que toca el cofre. Si clientId es ulong.MaxValue
    // vale cualquiera; si no, solo el jugador de ese cliente.
    private PlayerAvatar FindTouchingPlayer(ulong clientId)
    {
        PlayerAvatar[] players = FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            PlayerAvatar p = players[i];

            if (!p.IsSpawned) continue;
            if (clientId != ulong.MaxValue && p.OwnerClientId != clientId) continue;
            if (IsTouching(p.transform.position)) return p;
        }

        return null;
    }

    private bool IsTouching(Vector3 playerPosition)
    {
        if (chestCollider == null) return false;

        // Ignoramos la altura: solo importa la distancia en el suelo.
        playerPosition.y = chestCollider.bounds.center.y;

        Vector3 closest = chestCollider.ClosestPoint(playerPosition);

        return Vector3.Distance(playerPosition, closest) <= touchDistance;
    }

    private void OpenChest()
    {
        Opened.Value = true;
        UserOpening.Value = ulong.MaxValue;
        Progress.Value = 1f;

        for (int i = 0; i < cubesToSpawn; i++)
        {
            Vector3 spawnPoint = transform.position
                + Quaternion.Euler(0f, i * (360f / cubesToSpawn), 0f) * new Vector3(2f, 1f, 0f);

            NetworkObject colorCube = Instantiate(cubePrefab, spawnPoint, Random.rotation);
            colorCube.Spawn();
        }
    }


    // =========================================================
    // VISUAL (todas las máquinas)
    // =========================================================

    private void OnOpenedChanged(bool previous, bool current)
    {
        if (current) showOpenedUntil = Time.time + 2f;

        ApplyOpenedVisual();
    }

    private void ApplyOpenedVisual()
    {
        if (chestRenderer != null)
        {
            chestRenderer.material.color = Opened.Value ? OpenedColor : closedColor;
        }
    }

    private void BuildTimerText()
    {
        if (timerText != null) return;

        GameObject textObject = new GameObject("ChestTimerText");

        timerText = textObject.AddComponent<TextMeshPro>();
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.fontSize = 8f;
        timerText.fontStyle = FontStyles.Bold;
        timerText.color = Color.white;
        timerText.outlineColor = new Color32(0, 0, 0, 255);
        timerText.outlineWidth = 0.2f;
        timerText.rectTransform.sizeDelta = new Vector2(6f, 2f);
        timerText.text = "";
    }

    private void DestroyTimerText()
    {
        if (timerText != null)
        {
            Destroy(timerText.gameObject);
            timerText = null;
        }
    }

    private void LateUpdate()
    {
        if (timerText == null) return;

        string newText = "";

        if (!Opened.Value && UserOpening.Value != ulong.MaxValue)
        {
            float remaining = Mathf.Max(0f, (1f - Progress.Value) * TimeToOpenChest);
            newText = remaining.ToString("0.0") + "s";
        }
        else if (Opened.Value && Time.time < showOpenedUntil)
        {
            newText = "¡Abierto!";
        }

        if (timerText.text != newText) timerText.text = newText;

        // Sobre el cofre, mirando a la cámara.
        Vector3 top = chestCollider != null
            ? new Vector3(chestCollider.bounds.center.x, chestCollider.bounds.max.y + 0.7f, chestCollider.bounds.center.z)
            : transform.position + Vector3.up * 1.5f;

        timerText.transform.position = top;

        if (Camera.main != null)
        {
            timerText.transform.rotation = Camera.main.transform.rotation;
        }
    }
}