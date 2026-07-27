using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallingGrapplePoint : MonoBehaviour
{
    private static readonly List<FallingGrapplePoint> ActivePoints = new List<FallingGrapplePoint>();

    [SerializeField] private Transform anchor;
    [SerializeField] private Light glowLight;
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField, Min(0f)] private float reuseCooldownSeconds = 1.5f;
    [SerializeField, Min(0f)] private float availableGlowIntensity = 5f;

    private float nextAvailableAt;
    private bool isAvailable;

    public static IReadOnlyList<FallingGrapplePoint> Points => ActivePoints;
    public Transform Anchor => anchor != null ? anchor : transform;
    public bool IsReady => Time.time >= nextAvailableAt;

    private void Awake()
    {
        if (anchor == null)
        {
            anchor = transform;
        }

        if (glowLight == null)
        {
            glowLight = GetComponentInChildren<Light>(true);
        }

        if (promptRoot == null)
        {
            CreateRuntimePrompt();
        }

        SetAvailable(false, string.Empty);
    }

    private void OnEnable()
    {
        if (!ActivePoints.Contains(this))
        {
            ActivePoints.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePoints.Remove(this);
    }

    private void LateUpdate()
    {
        if (!isAvailable || promptRoot == null || Camera.main == null)
        {
            return;
        }

        promptRoot.transform.rotation = Quaternion.LookRotation(
            Camera.main.transform.position - promptRoot.transform.position,
            Camera.main.transform.up);
    }

    public void SetAvailable(bool available, string bindingDisplay)
    {
        isAvailable = available && IsReady;
        if (promptRoot != null)
        {
            promptRoot.SetActive(isAvailable);
        }

        if (promptText != null && isAvailable)
        {
            promptText.text = bindingDisplay;
        }

        if (glowLight != null)
        {
            glowLight.enabled = isAvailable;
            glowLight.intensity = isAvailable ? availableGlowIntensity : 0f;
        }
    }

    public bool TryConsume()
    {
        if (!IsReady)
        {
            return false;
        }

        nextAvailableAt = Time.time + reuseCooldownSeconds;
        SetAvailable(false, string.Empty);
        return true;
    }

    private void CreateRuntimePrompt()
    {
        GameObject promptObject = new GameObject("GrapplePrompt");
        promptObject.transform.SetParent(transform, false);
        promptObject.transform.localPosition = Vector3.up * 0.9f;
        TextMeshPro text = promptObject.AddComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.5f;
        text.color = new Color(0.72f, 0.95f, 1f);
        promptRoot = promptObject;
        promptText = text;
    }
}
