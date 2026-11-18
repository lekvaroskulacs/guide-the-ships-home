using TMPro;
using UnityEngine;

public class BoatSpawner : MonoBehaviour
{

    [Tooltip("How many seconds between spawning a new boat")]
    [SerializeField] float rate;

    [SerializeField] float radius;

    [SerializeField] GameObject motorBoatPrefab;
    [SerializeField] GameObject largeCargoShipPrefab;
    [SerializeField] GameObject smallCargoShipPrefab;

    [SerializeField] bool enableSpawn;

    [SerializeField] TMP_Text timerText;

    [Tooltip("Seconds the player has to survive to win")]
    [SerializeField] float levelDuration = 300f;

    [Tooltip("Optional: shown when the level is won")]
    [SerializeField] GameObject winPanel;
    [Tooltip("Optional: shown when the level is lost")]
    [SerializeField] GameObject losePanel;

    private float cooldown = 0;
    private float timeLeft;
    private bool levelRunning;

    public bool LevelRunning => levelRunning;

    // Update is called once per frame
    void Update()
    {
        if (!levelRunning)
            return;

        TickTimer();

        if (enableSpawn)
        {
            TickSpawner();
        }
    }

    void TickTimer()
    {
        timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
        UpdateTimerText();

        if (timeLeft <= 0f)
            EndLevel(true);
    }

    void UpdateTimerText()
    {
        if (timerText == null)
            return;
        // Ceil so the display shows 0:00 only at the very end
        int seconds = Mathf.CeilToInt(timeLeft);
        timerText.text = $"Level 1    {seconds / 60}:{seconds % 60:00}";
    }

    void TickSpawner()
    {
        cooldown += Time.deltaTime;

        if (cooldown >= rate)
        {
            cooldown = 0;
            Spawn();
        }
    }

    void Spawn()
    {
        Vector3 position = Vector3.zero;
        float angle = Random.Range(0, 2 * Mathf.PI);
        position.x = Mathf.Cos(angle) * radius;
        position.z = Mathf.Sin(angle) * radius;

        int whichShip = Mathf.FloorToInt(Random.Range(0, 3));
        GameObject[] ships = {motorBoatPrefab, smallCargoShipPrefab, largeCargoShipPrefab};
        Instantiate(ships[whichShip], position, Quaternion.identity);
    }

    public void StartLevel()
    {
        enableSpawn = true;
        cooldown = 0;
        timeLeft = levelDuration;
        levelRunning = true;
        UpdateTimerText();
    }

    public void EndLevel(bool won)
    {
        if (!levelRunning)
            return;

        levelRunning = false;
        enableSpawn = false;

        if (won && winPanel != null)
            winPanel.SetActive(true);
        if (!won && losePanel != null)
            losePanel.SetActive(true);

        Debug.Log(won ? "Level won!" : "Level lost!");

        // Freeze ships, beam and waves in place
        Time.timeScale = 0f;
    }
}
