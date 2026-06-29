using UnityEngine;

public class SleepStatus : MonoBehaviour
{
    private bool isAsleep;
    public bool IsAsleep => isAsleep;
    private int remainingTurns = -1;

    [Header("Effets visuels")]
    [SerializeField] private GameObject sleepPrefab;
    [SerializeField] private GameObject wakeUpPrefab;
    [SerializeField] private Vector3 effectOffset = new Vector3(0, 2f, 0);

    [Header("Effets sonores")]
    [SerializeField] private AudioClipSO sleepClip;
    [SerializeField] private AudioClipSO wakeUpClip;

    private GameObject currentSleepEffect;

    public void Sleep(int turns = -1)
    {
        if (isAsleep && turns < 0)
            return;

        isAsleep = true;
        remainingTurns = turns;

        if (sleepClip != null)
            AudioManager.Instance?.PlaySfx(sleepClip);

        if (sleepPrefab != null && currentSleepEffect == null)
        {
            currentSleepEffect = Instantiate(sleepPrefab, transform);
            currentSleepEffect.transform.localPosition = effectOffset;
        }
    }

    public void WakeUp()
    {
        if (!isAsleep)
            return;

        isAsleep = false;
        remainingTurns = -1;

        if (wakeUpClip != null)
            AudioManager.Instance?.PlaySfx(wakeUpClip);

        if (currentSleepEffect != null)
        {
            Destroy(currentSleepEffect);
            currentSleepEffect = null;
        }

        if (wakeUpPrefab != null)
        {
            var effect = Instantiate(wakeUpPrefab, transform.position + effectOffset, Quaternion.identity);
            Destroy(effect, 3f);
        }
    }

    public void TickTurn()
    {
        if (remainingTurns < 0)
            return;

        remainingTurns--;
        if (remainingTurns <= 0)
            WakeUp();
    }

    public void OnDamageTaken()
    {
        if (isAsleep)
            WakeUp();
    }
}
