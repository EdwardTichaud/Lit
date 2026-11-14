using System.Collections;
using Lit.Story;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(StorySequenceFadeController), typeof(StorySequenceCameraDriver))]
public sealed class EnolaKnifeGame : MonoBehaviour, IInputModeHandler
{
    public const string Idle = "AS_Left_KnifeRaisedIdle";
    public const string RewardId = "enola.knife.rhythm";
    public Transform stoolPoint;
    public KnowledgeSO reward;
    [SerializeField] private CharacterData speakerCharacterData;
    public RuntimeAnimatorController activityAnimator;
    public GameObject knifePrefab;
    public Material knifeMaterial;
    public Vector3 knifeHandOffset;
    public Vector3 knifeHandAngles;
    [TextArea] public string introduction = "Enola — Tu auras assez de cran ? Attends que le repère entre dans la zone dorée, puis appuie sur A. Six bons temps sur huit. B pour arrêter.";
    [TextArea] public string victory = "Enola — Voilà. Tu as trouvé la cadence. Garde-la en mémoire.";
    [TextArea] public string defeat = "Enola — Tu précipites le geste. Écoute le rythme, puis réessaie.";
    private static readonly string[] Strikes = { "AS_Left_HitBetweenThumbIndex", "AS_Left_HitBetweenIndexMiddle",
        "AS_Left_HitBetweenMiddleRing", "AS_Left_HitBetweenRingPinky" };
    private enum Stage { Idle, Intro, Transition, Playing, Result }
    private Stage stage;
    private SquadCharacterController actor;
    private LitOpsiveLocomotionBridge bridge;
    private LitOpsiveLocomotionBridge.ExternalLockHandle movementLock;
    private StorySequenceFadeController fade;
    private StorySequenceCameraDriver driver;
    private StorySequenceCameraPoint point;
    private LitGameplayCameraModeController cameraMode;
    private Camera camera;
    private Pose origin, originalCamera;
    private float originalFov, originalNearClip;
    private Animator animator;
    private RuntimeAnimatorController normalAnimator;
    private bool normalRootMotion;
    private Transform head;
    private GameObject knife, hud;
    private RectTransform cursor, targetWindow;
    private TMP_Text hudText;
    private DialoguePanelUI dialogue;
    private EnolaKnifeRhythm rhythm;
    private Vector2? lastShownWindow;
    private int shownBeat;
    private bool advance, cancel, placed, ownsCamera, won;
    private double resumeIdleAt;
    private string feedback;
    private int stageFrame;
    public bool IsRunning => stage != Stage.Idle;
    public bool IsPlayingRhythm => stage == Stage.Playing;
    public int Score => rhythm?.Score ?? 0;
    public Vector3 CurrentCameraPosition => point != null ? point.transform.position : Vector3.zero;

    public void Begin(SquadCharacterController player)
    {
        if (IsRunning || player == null || stoolPoint == null || reward == null || StorySequenceRunner.IsAnySequencePlaying ||
            player.gameObject != LocalPlayerUtils.GetControlledCharacter() || InputFocusStack.HasAnyFocus() ||
            GameFlowService.Instance?.IsTransitioning == true ||
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return;
        var animation = player.GetComponent<CharacterAnimationController>();
        animator = animation != null ? animation.Animator : player.GetComponentInChildren<Animator>();
        bridge = player.GetComponent<LitOpsiveLocomotionBridge>();
        camera = Camera.main;
        cameraMode = camera != null ? camera.GetComponentInParent<LitGameplayCameraModeController>() : null;
        if (animator == null || !animator.isHuman || activityAnimator == null || camera == null ||
            cameraMode != null && cameraMode.ExternalControl || bridge == null || bridge.IsInputSuppressedByUcc ||
            !bridge.TryAcquireExternalLock(this, out movementLock, stopActiveAbilities: true))
        {
            InfoBoxUI.TryShowToast("Le jeu du couteau n'est pas disponible avec ce personnage.");
            return;
        }
        actor = player; actor.Stop(); head = animator.GetBoneTransform(HumanBodyBones.Head);
        origin = new Pose(actor.transform.position, actor.transform.rotation);
        fade = GetComponent<StorySequenceFadeController>(); driver = GetComponent<StorySequenceCameraDriver>();
        advance = cancel = won = false; rhythm = null; feedback = "";
        RuntimeOutlineSelectionManager.PushSuspension(this);
        SetStage(Stage.Intro);
        Present(introduction);
        StartCoroutine(PlayRound());
    }

    private IEnumerator PlayRound()
    {
        while (!advance && !cancel) yield return null;
        CloseDialogue();
        if (cancel) { Release(); yield break; }
        SetStage(Stage.Transition);
        yield return fade.FadeTo(1, .35f, true);
        if (!bridge.TryBeginScriptedTraversal(out _)) { Release(); yield break; }
        placed = true;
        bridge.ApplyScriptedTraversalPose(stoolPoint.position, stoolPoint.rotation);
        normalAnimator = animator.runtimeAnimatorController;
        normalRootMotion = animator.applyRootMotion;
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = activityAnimator;
        animator.Play(StateHash(Idle), 0, 0);
        yield return null;
        originalCamera = new Pose(camera.transform.position, camera.transform.rotation); originalFov = camera.fieldOfView;
        originalNearClip = camera.nearClipPlane;
        if (!driver.BeginCinematic()) { Release(); yield break; }
        ownsCamera = true;
        cameraMode?.BeginExternalControl();
        camera.nearClipPlane = .04f;
        var anchor = new GameObject("Enola_HeadCameraPoint"); anchor.transform.SetParent(transform, false);
        point = anchor.AddComponent<StorySequenceCameraPoint>();
        var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var otherHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        // Compose once from the settled idle pose, so strikes do not shake the view.
        Vector3 eyes = head != null ? head.position : actor.transform.position + Vector3.up * 1.55f;
        Vector3 workArea = hand != null && otherHand != null
            ? (hand.position + otherHand.position) * .5f
            : eyes + stoolPoint.forward * .65f - Vector3.up * .45f;
        Pose shot = ResolveKnifeCamera(eyes, stoolPoint.forward, workArea);
        point.transform.SetPositionAndRotation(shot.position, shot.rotation);
        point.SetFieldOfView(60f);
        driver.SetCameraPoint(point, 0);
        if (knifePrefab != null && hand != null)
        {
            knife = Instantiate(knifePrefab, hand);
            knife.transform.SetLocalPositionAndRotation(knifeHandOffset, Quaternion.Euler(knifeHandAngles));
            foreach (var collider in knife.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (knifeMaterial != null)
                foreach (var renderer in knife.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = knifeMaterial;
        }
        yield return fade.FadeTo(0, .35f, true);
        if (!cancel)
        {
            BuildHud();
            rhythm = new EnolaKnifeRhythm(Time.unscaledTimeAsDouble + .5, previousWindow: lastShownWindow);
            shownBeat = -1;
            SetStage(Stage.Playing);
            while (!cancel && !rhythm.Complete(Time.unscaledTimeAsDouble))
            {
                double now = Time.unscaledTimeAsDouble;
                RefreshTimingBar(now);
                hudText.text = $"Jeu du couteau  ·  Temps {shownBeat + 1}/{EnolaKnifeRhythm.BeatCount}  ·  {Score} réussis\n{feedback}\nA / Entrée : jouer   •   B / Échap : quitter";
                if (resumeIdleAt > 0 && now >= resumeIdleAt) { animator.CrossFadeInFixedTime(StateHash(Idle), .06f, 0); resumeIdleAt = 0; }
                yield return null;
            }
            hud.SetActive(false);
            if (!cancel)
            {
                won = Score >= EnolaKnifeRhythm.PassingScore;
                advance = false;
                SetStage(Stage.Result);
                string rewardText = won ? (KnowledgeManager.Instance?.HasKnowledge(reward) == true
                    ? "\nSavoir déjà obtenu." : "\nSavoir : " + reward.title) : "\nRéessaie en interagissant avec le tabouret.";
                Present($"{(won ? victory : defeat)}\n{Score}/{EnolaKnifeRhythm.BeatCount} temps réussis.{rewardText}\nA / B : terminer");
                while (!advance && !cancel) yield return null;
            }
        }
        CloseDialogue(); SetStage(Stage.Transition);
        yield return fade.FadeTo(1, .25f, true);
        RestorePlayerAndCamera();
        yield return fade.FadeTo(0, .3f, true);
        var revealer = actor;
        bool grant = won;
        Release();
        if (grant && revealer != null && KnowledgeReveal.Reveal(reward, revealer.gameObject, "enola.knife_game"))
            CharacterStateStore.Instance?.Save();
    }

    public bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context)
    {
        if (!IsRunning) return false;
        if (action == InputModeAction.Cancel) { cancel = true; return true; }
        if (Time.frameCount == stageFrame) return true;
        if (action != InputModeAction.Submit) return true;
        if (stage == Stage.Intro || stage == Stage.Result) advance = true;
        double now = Time.unscaledTimeAsDouble;
        if (stage == Stage.Playing && rhythm.TryPress(now, out bool hit))
        {
            feedback = hit ? "Bien joué !" : "Hors tempo";
            int strike = StateHash(Strikes[rhythm.Beat(now) % Strikes.Length]);
            if (hit && strike != 0) { animator.CrossFadeInFixedTime(strike, .05f, 0); resumeIdleAt = now + .36; }
        }
        return true;
    }

    private void SetStage(Stage next) { stage = next; stageFrame = Time.frameCount; }
    private int StateHash(string name)
    {
        int full = Animator.StringToHash("Base Layer." + name), shortName = Animator.StringToHash(name);
        return animator.HasState(0, full) ? full : animator.HasState(0, shortName) ? shortName : 0;
    }
    private void Present(string line)
    {
        dialogue = DialoguePanelUI.GetOrCreate();
        Sprite portrait = speakerCharacterData != null ? speakerCharacterData.portrait : null;
        if (dialogue != null && (dialogue.IsShowing || !dialogue.ShowControlledMessage(line, portrait))) dialogue = null;
        // The dialogue renders the text; this activity owns its Submit/Cancel.
        InputFocusStack.Push(this);
        if (dialogue == null) InfoBoxUI.TryShowToast(line);
    }
    private void CloseDialogue() { if (dialogue != null) dialogue.HideControlledImmediate(); dialogue = null; }
    private void Update()
    {
        if (IsRunning && (actor == null || actor.CurrentHp <= 0 || LocalPlayerUtils.GetControlledCharacter() != actor.gameObject ||
            GameFlowService.Instance?.IsTransitioning == true)) { StopAllCoroutines(); Release(); return; }
        // The shared UI map currently defines only gamepad Submit/Cancel.
        if (!IsRunning || !InputFocusStack.HasFocus(this) || Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) HandleInputModeAction(InputModeAction.Cancel, default);
        else if (Keyboard.current.enterKey.wasPressedThisFrame) HandleInputModeAction(InputModeAction.Submit, default);
    }
    public static Pose ResolveKnifeCamera(Vector3 eyes, Vector3 facing, Vector3 workArea)
    {
        Vector3 forward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        Vector3 position = eyes + forward * .17f + Vector3.up * .035f;
        Vector3 aim = workArea - position;
        // Keep the shot on the table side even if an avatar's idle puts the wrists too close.
        aim += forward * Mathf.Max(0f, .24f - Vector3.Dot(aim, forward));
        Vector3 horizontal = Vector3.ProjectOnPlane(aim, Vector3.up);
        float pitch = Mathf.Clamp(Mathf.Atan2(-aim.y, horizontal.magnitude) * Mathf.Rad2Deg, 30f, 68f);
        Quaternion rotation = Quaternion.LookRotation(horizontal, Vector3.up) * Quaternion.Euler(pitch, 0, 0);
        return new Pose(position, rotation);
    }
    private void RestorePlayerAndCamera()
    {
        if (normalAnimator != null && animator != null)
        {
            animator.runtimeAnimatorController = normalAnimator;
            animator.applyRootMotion = normalRootMotion;
        }
        normalAnimator = null;
        if (placed && bridge != null && bridge.isActiveAndEnabled)
        {
            if (actor != null && GameFlowService.Instance?.IsTransitioning != true)
            {
                bridge.ApplyScriptedTraversalPose(origin.position, origin.rotation);
                int locomotion = animator != null ? StateHash("Locomotion") : 0;
                if (actor.CurrentHp > 0 && locomotion != 0) animator.CrossFadeInFixedTime(locomotion, .08f, 0);
            }
            bridge.EndScriptedTraversal();
        }
        placed = false;
        if (ownsCamera)
        {
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(originalCamera.position, originalCamera.rotation);
                camera.fieldOfView = originalFov;
            }
            driver?.EndCinematic(); cameraMode?.EndExternalControl();
            if (camera != null) camera.nearClipPlane = originalNearClip;
        }
        ownsCamera = false;
        if (point != null) Destroy(point.gameObject); point = null;
        if (knife != null) { knife.SetActive(false); Destroy(knife); } knife = null;
    }
    private void Release()
    {
        RestorePlayerAndCamera(); CloseDialogue();
        if (hud != null) { hud.SetActive(false); Destroy(hud); } hud = null;
        fade?.SetImmediate(0);
        movementLock?.Dispose(); movementLock = null;
        InputFocusStack.Pop(this); RuntimeOutlineSelectionManager.PopSuspension(this);
        stage = Stage.Idle; actor = null; resumeIdleAt = 0;
    }
    private void OnDisable() { StopAllCoroutines(); if (IsRunning) Release(); }

    private void BuildHud()
    {
        hud = new GameObject("EnolaKnifeHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        hud.transform.SetParent(transform, false);
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = ImageRect(hud.transform, "Panel", new Color(.04f, .045f, .06f, .94f));
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0); panel.sizeDelta = new Vector2(620, 172); panel.anchoredPosition = new Vector2(0, 132);
        var textObject = new GameObject("Instructions", typeof(RectTransform), typeof(TextMeshProUGUI)); textObject.transform.SetParent(panel, false);
        hudText = textObject.GetComponent<TMP_Text>(); hudText.fontSize = 24; hudText.alignment = TextAlignmentOptions.Center; hudText.raycastTarget = false;
        hudText.rectTransform.sizeDelta = new Vector2(610, 108); hudText.rectTransform.anchoredPosition = new Vector2(0, 25);
        var bar = ImageRect(panel, "Timing", new Color(.19f, .22f, .28f)); bar.sizeDelta = new Vector2(550, 23); bar.anchoredPosition = new Vector2(0, -52);
        targetWindow = ImageRect(bar, "TargetWindow", new Color(1, .73f, .27f));
        targetWindow.sizeDelta = Vector2.zero;
        cursor = ImageRect(bar, "Cursor", Color.white); cursor.sizeDelta = new Vector2(6, 31);
    }

    private void RefreshTimingBar(double now)
    {
        int beat = rhythm.Beat(now);
        if (shownBeat != beat)
        {
            shownBeat = beat;
            Vector2 window = rhythm.Window(beat);
            lastShownWindow = window;
            targetWindow.anchorMin = new Vector2(window.x - window.y * .5f, 0f);
            targetWindow.anchorMax = new Vector2(window.x + window.y * .5f, 1f);
        }
        cursor.anchorMin = cursor.anchorMax = new Vector2(rhythm.Phase(now), .5f);
    }
    private static RectTransform ImageRect(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image.rectTransform;
    }
}
