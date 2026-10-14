using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Enemy : NetworkBehaviour
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    [Header("Word")]
    [SerializeField] private string word;
    [SerializeField] private TMP_Text wordText;

    [Header("Chase")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldown = 3f;

    private CharacterController controller;
    private FirstPersonController player;
    private float nextAttackTime = 0f;

    public string Word => word;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Start()
    {
        player = FindFirstObjectByType<FirstPersonController>();
        if (wordText != null) wordText.SetText(word);
    }

    public void SetWord(string newWord)
    {
        word = newWord;
        if (wordText != null) wordText.SetText(word);
    }

    void Update()
    {
        if (player == null || !player.IsAlive) return;

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (toPlayer.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toPlayer);

        if (distance > attackRange)
        {
            controller.SimpleMove(toPlayer.normalized * moveSpeed);
        }
        else if (Time.time >= nextAttackTime)
        {
            player.TakeDamage(damage);
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void LateUpdate()
    {
        if (wordText == null || player == null) return;
        wordText.transform.rotation = player.playerCamera.transform.rotation;
    }
}