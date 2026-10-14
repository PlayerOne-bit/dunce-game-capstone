using TMPro;
using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    public TMP_Text attackText;
    public TMP_Text staminaText;
    public TMP_Text livesText;

    void Awake() => Instance = this;
}