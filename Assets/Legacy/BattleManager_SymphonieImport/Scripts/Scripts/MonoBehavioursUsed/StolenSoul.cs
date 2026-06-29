using UnityEngine;
using System.Collections;

public class StolenSoul : MonoBehaviour
{
    public Transform target;

    void Start()
    {
        NewBattleManager battleManager = NewBattleManager.Instance;
        CharacterUnit caster = battleManager != null ? battleManager.currentCharacterUnit : null;
        if (caster == null)
            caster = GetComponentInParent<CharacterUnit>();
        target = caster != null ? caster.transform : null;
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        // 1. Apparition (le GO est déjà actif)
        yield return new WaitForSeconds(2f);

        // 2. Monter de 10m en 2s
        Vector3 startPos = transform.position;
        Vector3 upPos = startPos + Vector3.up * 10f;

        yield return MoveOverTime(startPos, upPos, 2f);

        // 3. Pause 1s
        yield return new WaitForSeconds(1f);

        // 4. Aller vers la cible en 2s
        if (target != null)
        {
            yield return MoveOverTime(transform.position, target.position, 2f);
        }
    }

    IEnumerator MoveOverTime(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float normalizedTime = t / duration;
            transform.position = Vector3.Lerp(from, to, normalizedTime);
            yield return null;
        }

        transform.position = to;
    }
}
