using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
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
    [SerializeField] private InputActionReference interactAction;
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

    [Header("Display")]
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TextMeshProUGUI staminaText;
    [SerializeField] private TMP_Text livesText;

    private CharacterController controller;
    private float verticalRotation = 0f;
    private float verticalVelocity = 0f;
    private bool isTypingMode = false;
    private string typedText = "";
    private int lastShownStamina = -1;
    private int lives;
    public bool IsAlive => lives > 0;

    void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        typeMode.action.Enable();
        submitAttack.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
        typeMode.action.Disable();
        submitAttack.action.Disable();

        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        lives = maxLives;
        UpdateLivesText();
        RefreshAttackText();
        SetTypingMode(false);
    }

    void Update()
    {
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
        {
            verticalVelocity = -2f;
        }

        bool jumpPressed = !isTypingMode && jumpAction.action.WasPressedThisFrame();
        if (controller.isGrounded && jumpPressed)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void UpdateStaminaText()
    {
        int shown = Mathf.RoundToInt(stamina);
        if (shown == lastShownStamina) return;

        lastShownStamina = shown;
        staminaText.SetText($"Stamina {shown}");
    }

    void HandleTypeMode()
    {
        if (typeMode.action.WasPressedThisFrame())
        {
            SetTypingMode(!isTypingMode);
        }

        if (isTypingMode && submitAttack.action.WasPressedThisFrame())
        {
            SubmitAttack();
        }
    }
    void SubmitAttack()
    {
        string attempt = typedText.Trim();
        if (attempt.Length == 0) return;

        foreach (Enemy enemy in Enemy.Active)
        {
            if (string.Equals(enemy.Word, attempt, System.StringComparison.OrdinalIgnoreCase))
            {
                Destroy(enemy.gameObject);
                break;
            }
        }

        typedText = "";
        RefreshAttackText();
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

    public string Attack()
    {
        return typedText;
    }
    public void TakeDamage(int amount)
    {
        if (!IsAlive) return;

        lives = Mathf.Clamp(lives - amount, 0, maxLives);
        UpdateLivesText();

        if (lives == 0)
        {
            Debug.Log("Game Over"); 
        }
    }

    void UpdateLivesText()
    {
        livesText.SetText($"Lives {lives}");
    }
}