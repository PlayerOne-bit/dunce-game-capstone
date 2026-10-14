using System.Collections.Generic;
using FishNet;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemyPrefab;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxEnemiesPerPlayer = 5;
    [SerializeField] private float minSpawnDistance = 10f;
    [SerializeField] private float maxSpawnDistance = 25f;

    [Header("Words")]
    [SerializeField] private string[] words = { "Sample" };

    private float nextSpawnTime = 0f;
    private readonly List<FirstPersonController> alivePlayers = new List<FirstPersonController>();

    void Update()
    {
        // Only the server spawns enemies
        if (!InstanceFinder.IsServerStarted) return;

        alivePlayers.Clear();
        foreach (FirstPersonController p in FirstPersonController.AllPlayers)
        {
            if (p.IsAlive) alivePlayers.Add(p);
        }
        if (alivePlayers.Count == 0) return;

        int maxEnemies = maxEnemiesPerPlayer * alivePlayers.Count;
        if (Enemy.Active.Count >= maxEnemies)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime) return;

        Spawn(alivePlayers[Random.Range(0, alivePlayers.Count)]);
        nextSpawnTime = Time.time + spawnInterval;
    }

    void Spawn(FirstPersonController target)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 position = target.transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

        Enemy enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        enemy.SetWord(words[Random.Range(0, words.Length)].ToUpper());
        InstanceFinder.ServerManager.Spawn(enemy.NetworkObject);
    }
}