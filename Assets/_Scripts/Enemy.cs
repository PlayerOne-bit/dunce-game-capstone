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
    [SerializeField] private TMP_Text wordText;

    [Header("Chase")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;

    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldown = 3f;

    private readonly SyncVar<string> _word = new SyncVar<string>();
    private CharacterController controller;
    private float nextAttackTime = 0f;

    public string Word => _word.Value;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        _word.OnChange += OnWordChanged;
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public override void OnStartClient()
    {
        // Pure clients only display; the server moves the enemy and NetworkTransform copies it
        if (!IsServerStarted) controller.enabled = false;

        wordText.SetText(_word.Value);
    }

    // Call this before spawning so the word is part of the initial state
    public void SetWord(string newWord)
    {
        _word.Value = newWord;
    }

    void OnWordChanged(string prev, string next, bool asServer)
    {
        if (wordText != null) wordText.SetText(next);
    }

    void Update()
    {
        if (!IsServerStarted) return; // only the server runs the AI

        FirstPersonController target = FindNearestPlayer();
        if (target == null) return;

        Vector3 toPlayer = target.transform.position - transform.position;
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
            target.TakeDamage(damage);
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    FirstPersonController FindNearestPlayer()
    {
        FirstPersonController nearest = null;
        float best = float.MaxValue;

        foreach (FirstPersonController p in FirstPersonController.AllPlayers)
        {
            if (!p.IsAlive) continue;

            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = p;
            }
        }
        return nearest;
    }

    void LateUpdate()
    {
        // Every screen turns the word toward its own local camera
        FirstPersonController local = FirstPersonController.Local;
        if (wordText == null || local == null) return;

        wordText.transform.rotation = local.playerCamera.transform.rotation;
    }
}