using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lit.Story
{
    // Reuses the existing virtual keyboard's keys/visuals, independently of the main menu controller.
    [DisallowMultipleComponent]
    public sealed class StorySequenceNamePrompt : MonoBehaviour, IInputModeHandler
    {
        [SerializeField] private GameObject virtualKeyboardPrefab;
        private readonly List<Button> keys = new List<Button>();
        private GameObject view;
        private TMP_Text nameText;
        private string draft = string.Empty;
        private bool submitted, opened, previousNavigationEvents;
        private int selected, openedFrame;
        private float nextMove;
        private Keyboard keyboard;
        private EventSystem eventSystem;
        private GameObject previousSelection;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;

        public bool Confirmed { get; private set; }
        public bool HasKeyboard => virtualKeyboardPrefab != null;
        public bool IsOpen => opened;

        public IEnumerator ChooseName()
        {
            Confirmed = false;
            submitted = false;
            draft = string.Empty;
            var rules = FindAnyObjectByType<WorldRulesStateManager>();
            if (rules == null || virtualKeyboardPrefab == null)
            {
                Debug.LogError("[Story] Saisie du nom : etat du monde ou clavier manquant.", this);
                yield break;
            }
            if (!TryBuildView()) yield break;
            try
            {
                eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    previousNavigationEvents = eventSystem.sendNavigationEvents;
                    previousSelection = eventSystem.currentSelectedGameObject;
                    eventSystem.sendNavigationEvents = false;
                }
                previousCursorVisible = Cursor.visible;
                previousCursorLock = Cursor.lockState;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                opened = true;
                openedFrame = Time.frameCount;
                InputFocusStack.Push(this);
                InputModeCoordinator.Enter(this, InputMode.UserInterface);
                AttachKeyboard();
                view.SetActive(true);
                SelectKey(0);
                while (!submitted && opened) yield return null;
                if (!submitted) yield break;
                // Commit only after explicit validation. Abort/unload never commits a partial draft.
                StorySequenceNames.SetMuninName(rules, draft);
                Confirmed = true;
            }
            finally { Close(); }
            // Do not let the confirmation press dismiss the next dialogue in the same frame.
            yield return null;
        }

        private void Update()
        {
            if (!opened || !InputFocusStack.HasFocus(this) || Time.frameCount <= openedFrame) return;
            AttachKeyboard();
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            { submitted = true; return; }
            if (keyboard?.escapeKey.wasPressedThisFrame ?? false)
            { draft = string.Empty; submitted = true; return; }
            if (keyboard?.backspaceKey.wasPressedThisFrame ?? false) Erase();
            Gamepad pad = Gamepad.current;
            if (pad?.startButton.wasPressedThisFrame ?? false) { submitted = true; return; }
            if (pad?.buttonEast.wasPressedThisFrame ?? false) Erase();
            Vector2 direction = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
            if (direction.sqrMagnitude < .2f && pad != null) direction = pad.leftStick.ReadValue();
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) direction = Vector2.left;
                if (keyboard.rightArrowKey.isPressed) direction = Vector2.right;
                if (keyboard.upArrowKey.isPressed) direction = Vector2.up;
                if (keyboard.downArrowKey.isPressed) direction = Vector2.down;
            }
            if (direction.sqrMagnitude > .25f && Time.unscaledTime >= nextMove)
            {
                int delta = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? 1 : -1)
                    : (direction.y > 0 ? -13 : 13);
                SelectKey((selected + delta + keys.Count) % keys.Count);
                nextMove = Time.unscaledTime + .18f;
            }
            if (direction.sqrMagnitude <= .25f) nextMove = 0f;
            if (pad?.buttonSouth.wasPressedThisFrame ?? false) keys[selected].onClick.Invoke();
        }

        // This modal consumes coordinator events; polling below is the only keyboard/gamepad submit owner.
        public bool HandleInputModeAction(InputModeAction action, InputAction.CallbackContext context) => opened;

        private void AttachKeyboard()
        {
            if (keyboard == Keyboard.current) return;
            if (keyboard != null) keyboard.onTextInput -= OnText;
            keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onTextInput += OnText;
        }

        private void OnText(char c)
        {
            if (!opened || submitted || Time.frameCount <= openedFrame || !InputFocusStack.HasFocus(this)) return;
            Append(c.ToString());
        }

        private void Append(string value)
        {
            // Normalize for storage on confirmation; preserve an empty draft and trailing spaces while typing.
            foreach (char c in value)
                if (draft.Length < StorySequenceNames.MaximumNameLength &&
                    (char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark ||
                     c == ' ' || c == '-' || c == '\'' || c == '\u2019')) draft += c;
            UpdateText();
        }

        private void Erase()
        {
            int[] indices = StringInfo.ParseCombiningCharacters(draft);
            if (indices.Length > 0) draft = draft.Substring(0, indices[indices.Length - 1]);
            UpdateText();
        }

        private void PressKey(string label, int index)
        {
            if (!opened || submitted || Time.frameCount <= openedFrame) return;
            SelectKey(index);
            if (label == "OK") submitted = true;
            else if (label == "Erase" || label == "Backspace" || label == "<-" || label == "Delete") Erase();
            else if (label == "_" || label == "Space" || label == "Espace" || label == "Blank" || label == " ") Append(" ");
            else if (label.Length == 1) Append(label);
        }

        private void SelectKey(int index)
        {
            selected = index;
            for (int i = 0; i < keys.Count; i++)
                keys[i].image.color = i == selected ? new Color(.45f, .32f, .12f, 1f) : new Color(.13f, .15f, .2f, 1f);
            eventSystem?.SetSelectedGameObject(keys[index].gameObject);
        }

        private void UpdateText()
        {
            if (nameText == null) return;
            nameText.text = string.IsNullOrWhiteSpace(draft) ? "Munin (par defaut)" : draft;
            nameText.color = string.IsNullOrWhiteSpace(draft) ? new Color(.6f, .62f, .68f) : Color.white;
        }

        private bool TryBuildView()
        {
            try { BuildView(); return true; }
            catch (System.Exception error)
            {
                Debug.LogError("[Story] Impossible de construire le clavier : " + error.Message, this);
                Close();
                return false;
            }
        }

        private static void DeleteUi(Object obj)
        {
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }

        private void BuildView()
        {
            view = new GameObject("Munin Name", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            view.SetActive(false);
            view.transform.SetParent(transform, false);
            var canvas = view.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 5;
            var scaler = view.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var dim = Rect("Backdrop", view.transform, new Vector2(1920, 1080), Vector2.zero);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.sizeDelta = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            var panel = Rect("Name Panel", view.transform, new Vector2(1160, 600), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(.045f, .055f, .08f, .99f);
            Label(panel, "Comment t'appelles-tu ?", new Vector2(0, 210), 36);
            var nameBox = Rect("Name", panel, new Vector2(1050, 66), new Vector2(0, 115));
            nameBox.gameObject.AddComponent<Image>().color = new Color(.09f, .11f, .16f);
            nameText = Label(nameBox, "", Vector2.zero, 34);
            nameText.richText = false;
            Label(panel, "Entree / OK : valider  -  Vide : Munin  -  Echap : nom par defaut", new Vector2(0, -225), 22);
            Label(panel, "Manette : A pour une touche, B pour effacer, Start pour valider", new Vector2(0, -260), 20);
            var clone = Instantiate(virtualKeyboardPrefab, panel, false);
            Transform keyRoot = null;
            foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                if (t.name == "VirtualKeyboardButtons") { keyRoot = t; break; }
            if (keyRoot == null) throw new System.InvalidOperationException("VirtualKeyboardButtons absent du prefab de clavier.");
            foreach (var action in clone.GetComponentsInChildren<MenuCursorAction>(true)) action.enabled = false;
            foreach (var cursor in clone.GetComponentsInChildren<CursorController>(true)) cursor.enabled = false;
            foreach (var cursor in clone.GetComponentsInChildren<VirtualKeyboardCursorController>(true)) cursor.enabled = false;
            keyRoot.SetParent(panel, false);
            var rect = (RectTransform)keyRoot;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1050, 220); rect.anchoredPosition = new Vector2(0, -55); rect.localScale = Vector3.one;
            var grid = keyRoot.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(72, 62); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 13;
            keys.Clear();
            foreach (Transform key in keyRoot)
            {
                string label = key.name;
                int index = keys.Count;
                var button = key.GetComponent<Button>() ?? key.gameObject.AddComponent<Button>();
                button.targetGraphic = key.GetComponent<Image>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.transition = Selectable.Transition.None;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => PressKey(label, index));
                var text = key.GetComponentInChildren<TMP_Text>(true);
                if (text != null) { text.text = label == "Erase" ? "Effacer" : label == "Blank" ? "Espace" : label; text.fontSize = 27; text.raycastTarget = false; }
                keys.Add(button);
            }
            DeleteUi(clone);
            UpdateText();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static TMP_Text Label(Transform parent, string value, Vector2 position, float size)
        {
            var rect = Rect("Text", parent, new Vector2(1100, 52), position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = size; text.color = new Color(.94f, .9f, .8f);
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }

        public void Cancel() { submitted = false; Close(); }
        private void Close()
        {
            if (keyboard != null) keyboard.onTextInput -= OnText;
            keyboard = null;
            if (opened)
            {
                InputModeCoordinator.Exit(this);
                InputFocusStack.Pop(this);
                if (eventSystem != null)
                {
                    eventSystem.sendNavigationEvents = previousNavigationEvents;
                    eventSystem.SetSelectedGameObject(previousSelection);
                }
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
            }
            opened = false;
            if (view != null) { view.SetActive(false); DeleteUi(view); view = null; }
            keys.Clear();
        }
        private void OnDisable() => Cancel();
    }
}
