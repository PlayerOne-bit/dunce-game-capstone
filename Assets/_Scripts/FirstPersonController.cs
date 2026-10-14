using System.Collections.Generic;
using FishNet.Component.Animating;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    // Server-side list of all players (enemies use this to pick targets)
    public static readonly List<FirstPersonController> AllPlayers = new List<FirstPersonController>();
    // The player controlled by this client (enemies use its camera for billboarding)
    public static FirstPersonController Local { get; private set; }

    [Header("Camera")]
    public Camera playerCamera;
    public float mouseSensitivity = 0.1f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference typeMode;
    [SerializeField] private InputActionReference submitAttack;

    [Header("Stats")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float stamina = 100f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainRate = 5f;
    [SerializeField] private float staminaRegenRate = 2f;

    [Header("Typing Mode")]
    [SerializeField] private int maxCharacters = 10;
    [SerializeField] private string placeholderText = "<Enter Input>";
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int InteractHash = Animator.StringToHash("Interact");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    // Synced from server to everyone
    private readonly SyncVar<int> _lives = new SyncVar<int>();

    // Local-only UI (assigned from PlayerHUD for the owner)
    private TMP_Text attackText;
    private TMP_Text staminaText;
    private TMP_Text livesText;

    private CharacterController controller;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f;
    private bool isTypingMode = false;
    private string typedText = "";
    private int lastShownStamina = -1;

    public bool IsAlive => _lives.Value > 0;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        _lives.OnChange += OnLivesChanged;
    }

    // ---------- Network lifecycle ----------

    public override void OnStartServer()
    {
        AllPlayers.Add(this);
        _lives.Value = maxLives;
    }

    public override void OnStopServer()
    {
        AllPlayers.Remove(this);
    }

    public override void OnStartClient()
    {
        if (!IsOwner)
        {
            // Remote players: no camera, no CharacterController (NetworkTransform moves them)
            controller.enabled = false;
            playerCamera.gameObject.SetActive(false);
            return;
        }

        Local = this;

        PlayerHUD hud = PlayerHUD.Instance;
        attackText = hud.attackText;
        staminaText = hud.staminaText;
        livesText = hud.livesText;

        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        typeMode.action.Enable();
        submitAttack.action.Enable();

        UpdateLivesText();
        RefreshAttackText();
        SetTypingMode(false);
    }

    public override void OnStopClient()
    {
        if (!IsOwner) return;

        if (Local == this) Local = null;

        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
        typeMode.action.Disable();
        submitAttack.action.Disable();

        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
    }

    // ---------- Update (owner only) ----------

    void Update()
    {
        if (NetworkObject == null) return; // not a networked object
        if (!IsOwner || !IsAlive) return;

        HandleTypeMode();
        HandleLook();
        HandleMovement();
    }

    void HandleLook()
    {
        if (isTypingMode) return;

        Vector2 look = lookAction.action.ReadValue<Vector2>();
        float mouseX = look.x * mouseSensitivity;
        float mouseY = look.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        Vector2 input = isTypingMode ? Vector2.zero : moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f);

        bool sprinting = !isTypingMode
                         && sprintAction.action.IsPressed()
                         && input.sqrMagnitude > 0.01f
                         && stamina > 0f;

        if (sprinting)
        {
            stamina -= staminaDrainRate * Time.deltaTime;
            move *= sprintMultiplier;
        }
        else
        {
            stamina += staminaRegenRate * Time.deltaTime;
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        UpdateStaminaText();

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        bool jumpPressed = !isTypingMode && jumpAction.action.WasPressedThisFrame();
        if (controller.isGrounded && jumpPressed)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        animator.SetBool(GroundedHash, controller.isGrounded);
        Vector3 horizontal = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        animator.SetFloat(SpeedHash, horizontal.magnitude, 0.1f, Time.deltaTime);
    }

    void UpdateStaminaText()
    {
        int shown = Mathf.RoundToInt(stamina);
        if (shown == lastShownStamina) return;

        lastShownStamina = shown;
        staminaText.SetText($"Stamina {shown}");
    }

    // ---------- Typing / attacking ----------

    void HandleTypeMode()
    {
        bool togglePressed = typeMode.action.WasPressedThisFrame();
        bool submitPressed = submitAttack.action.WasPressedThisFrame();

        if (isTypingMode && (submitPressed || togglePressed))
            SubmitAttack();

        if (togglePressed)
        {
            SetTypingMode(!isTypingMode);
            networkAnimator.SetTrigger(InteractHash);
        }
    }

    void SubmitAttack()
    {
        string attempt = typedText.Trim();
        if (attempt.Length == 0) return;

        SubmitAttackServer(attempt); // server decides if it hits

        typedText = "";
        RefreshAttackText();
    }

    [ServerRpc] // owner-only by default
    void SubmitAttackServer(string attempt)
    {
        if (!IsAlive || string.IsNullOrEmpty(attempt) || attempt.Length > maxCharacters) return;

        Enemy nearest = null;
        float nearestDistSqr = float.MaxValue;

        foreach (Enemy enemy in Enemy.Active)
        {
            if (!string.Equals(enemy.Word, attempt, System.StringComparison.OrdinalIgnoreCase))
                continue;

            float distSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = enemy;
            }
        }

        if (nearest != null)
            nearest.NetworkObject.Despawn(); // replaces Destroy()
    }

    void SetTypingMode(bool on)
    {
        isTypingMode = on;
        Cursor.visible = on;
        Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked;
        attackText.gameObject.SetActive(on);

        if (Keyboard.current == null) return;

        if (on)
        {
            typedText = "";
            RefreshAttackText();
            Keyboard.current.onTextInput += OnTextInput;
        }
        else
        {
            Keyboard.current.onTextInput -= OnTextInput;
        }
    }

    void OnTextInput(char c)
    {
        if (c == '\b')
        {
            if (typedText.Length > 0)
                typedText = typedText.Substring(0, typedText.Length - 1);
        }
        else if (!char.IsControl(c) && typedText.Length < maxCharacters)
        {
            typedText += c;
        }

        RefreshAttackText();
    }

    void RefreshAttackText()
    {
        attackText.SetText(typedText.Length == 0 ? placeholderText : typedText.ToUpper());
    }

    // ---------- Lives ----------

    [Server]
    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;

        _lives.Value = Mathf.Clamp(_lives.Value - amount, 0, maxLives);

        if (_lives.Value == 0)
            Debug.Log($"Player {OwnerId} is out");
    }

    void OnLivesChanged(int prev, int next, bool asServer)
    {
        if (IsOwner && livesText != null)
            UpdateLivesText();
    }

    void UpdateLivesText()
    {
        livesText.SetText($"Lives {_lives.Value}");
    }
}