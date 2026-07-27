using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RealTimeCombatReactionPrompt : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Vector3 enemyOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField] private bool faceCamera = true;

    private Transform target;
    private RealTimeCombatManager manager;

    private void Awake()
    {
        if (promptText == null)
        {
            promptText = GetComponentInChildren<TMP_Text>(true);
        }

        if (visualRoot == null && promptText != null)
        {
            visualRoot = promptText.gameObject;
        }
    }

    private void OnEnable()
    {
        manager = RealTimeCombatManager.Instance;
        if (manager != null) manager.ReactionWindowChanged += OnReactionWindowChanged;
        SetVisible(false);
    }

    private void OnDisable()
    {
        if (manager != null) manager.ReactionWindowChanged -= OnReactionWindowChanged;
        manager = null;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.TransformPoint(enemyOffset);
        if (faceCamera && Camera.main != null)
        {
            Vector3 direction = transform.position - Camera.main.transform.position;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void OnReactionWindowChanged(RealTimeCombatReactionWindow window)
    {
        target = window.IsOpen ? window.Enemy : null;
        SetVisible(window.IsOpen && target != null);
        if (!window.IsOpen || promptText == null || window.Skill == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < window.Skill.AcceptedEnemyReactions.Count; i++)
        {
            if (i > 0) builder.Append(" + ");
            builder.Append(window.Skill.AcceptedEnemyReactions[i]);
        }

        promptText.text = builder.ToString();
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot != null)
        {
            if (visualRoot == gameObject && promptText != null)
            {
                promptText.enabled = visible;
                return;
            }

            visualRoot.SetActive(visible);
        }
    }
}
