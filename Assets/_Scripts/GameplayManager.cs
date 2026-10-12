using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FirstPersonController player;
    [SerializeField] private Enemy enemyPrefab;

    [Header("Spawning")]
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int maxEnemies = 5;
    [SerializeField] private float minSpawnDistance = 10f;
    [SerializeField] private float maxSpawnDistance = 25f;

    [Header("Words")]
    [SerializeField] private string[] words = { "Sample" };

    private float nextSpawnTime = 0f;

    void Update()
    {
        if (player == null || !player.IsAlive) return;

        if (Enemy.Active.Count >= maxEnemies)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime) return;

        Spawn();
        nextSpawnTime = Time.time + spawnInterval;
    }

    void Spawn()
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 position = player.transform.position + new Vector3(dir.x, 0f, dir.y) * distance;

        Enemy enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        enemy.SetWord(words[Random.Range(0, words.Length)].ToUpper());
    }
}