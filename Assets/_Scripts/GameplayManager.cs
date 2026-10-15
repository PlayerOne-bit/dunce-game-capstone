using FishNet;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemyPrefab;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxEnemies = 5;
    [SerializeField] private float minSpawnDistance = 10f;
    [SerializeField] private float maxSpawnDistance = 25f;

    [Header("Words")]
    [SerializeField] private string[] words = { "fire", "water", "earth", "wind" };

    private float nextSpawnTime = 0f;

    void Update()
    {
        // Clients must never spawn enemies; only the server/host does
        if (!InstanceFinder.IsServerStarted) return;

        if (Enemy.Active.Count >= maxEnemies)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime) return;

        if (Spawn())
            nextSpawnTime = Time.time + spawnInterval;
    }

    bool Spawn()
    {
        FirstPersonController target = PickRandomAlivePlayer();
        if (target == null) return false;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 position = target.transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

        Enemy enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        enemy.SetWord(words[Random.Range(0, words.Length)]);

        // This is what makes it exist on every client
        InstanceFinder.ServerManager.Spawn(enemy.gameObject);
        return true;
    }

    FirstPersonController PickRandomAlivePlayer()
    {
        var all = FirstPersonController.AllPlayers;
        if (all.Count == 0) return null;

        int start = Random.Range(0, all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            FirstPersonController p = all[(start + i) % all.Count];
            if (p.IsAlive) return p;
        }
        return null;
    }
}