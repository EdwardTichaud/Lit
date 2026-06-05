/// ---------------------------------------------
/// Opsive Shared
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.Shared.Editor.Managers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// An editor window which allows for a single location to update all of the conflicted references on the character.
    /// </summary>
    public class ReferenceResolverWindow : EditorWindow
    {
        /// <summary>
        /// Maps a template bone to the source animator that owns it.
        /// </summary>
        private struct TemplateBoneMapping
        {
            public int AnimatorIndex;
            public HumanBodyBones Bone;
            public bool FirstPersonPerspective;
        }

        /// <summary>
        /// Maps a template animator transform to its index.
        /// </summary>
        private struct TemplateAnimatorMapping
        {
            public int AnimatorIndex;
            public bool FirstPersonPerspective;
        }

        /// <summary>
        /// Identifies a child transform relative to a root.
        /// </summary>
        private struct RelativeHierarchySegment
        {
            public string Name;
            public int SiblingIndex;
        }

        /// <summary>
        /// Represents an object that needs to be updated.
        /// </summary>
        public class ConflictingObjects
        {
            [Tooltip("The owning object that is in a conflicted state.")]
            public object Object;
            [Tooltip("The field that is in a conflicted state.")]
            public FieldInfo Field;
            [Tooltip("The class path to the field.")]
            public string Path;
            [Tooltip("Event that is invoked when the value is set.")]
            public System.Action<object> OnChangeEvent;

            /// <summary>
            /// Returns the value of the object.
            /// </summary>
            /// <returns>The value of the object.</returns>
            public object GetValue() { return Field.GetValue(Object); }

            /// <summary>
            /// Sets a new value.
            /// </summary>
            /// <param name="value">The new object value.</param>
            public void SetValue(object value)
            { 
                Field.SetValue(Object, value);
                OnChangeEvent(Object);
            }
        }

        private List<ConflictingObjects> m_ConflictingObjects;
        private Transform m_TemplateTransform;
        private Action m_OnClose;
        private static Dictionary<Transform, TemplateBoneMapping> s_TemplateBoneByTransform;
        private static Dictionary<Transform, TemplateAnimatorMapping> s_TemplateAnimatorByTransform;

        private bool m_Close;

        /// <summary>
        /// The window should be closed so it displays the accurate fields.
        /// </summary>
        public void OnDisable()
        {
            m_Close = true;
        }

        /// <summary>
        /// Closes the window after the project has been compiled.
        /// </summary>
        public void OnEnable()
        {
            if (m_Close) {
                EditorApplication.update += CloseWindow;
            }
        }

        /// <summary>
        /// Closes the window.
        /// </summary>
        private void CloseWindow()
        {
            EditorApplication.update -= CloseWindow;

            if (m_OnClose != null) {
                m_OnClose();
            }

            Close();
        }

        /// <summary>
        /// Initializes the window.
        /// </summary>
        public void Initialize(List<ConflictingObjects> conflictingObjects, GameObject templateObject, string objectType, Action onClose = null)
        {
            m_ConflictingObjects = conflictingObjects;
            m_TemplateTransform = templateObject.transform;
            m_OnClose = onClose;

            rootVisualElement.styleSheets.Add(Shared.Editor.Utility.EditorUtility.LoadAsset<StyleSheet>("e70f56fae2d84394b861a2013cb384d0"));//shared uss
            rootVisualElement.styleSheets.Add(ManagerStyles.StyleSheet);

            BuildVisualElements(objectType);
        }

        /// <summary>
        /// Builds the Visual Elements for the window.
        /// </summary>
        private void BuildVisualElements(string objectType)
        {
            rootVisualElement.Clear();

            if (m_ConflictingObjects == null || m_ConflictingObjects.Count == 0) {
                rootVisualElement.Add(new HelpBox("No fields need resolving.", HelpBoxMessageType.Info));
                return;
            }


            var scrollView = new ScrollView();
            rootVisualElement.Add(scrollView);
            
            var instructionsLabel = new Label($"The Reference Resolver tried to update all of the field references to the new {objectType}. " +
                                            "The fields below are not able to be automatically updated. Please manually update these fields.");
            instructionsLabel.style.whiteSpace = WhiteSpace.Normal;
            scrollView.Add(instructionsLabel);

            for (int i = 0; i < m_ConflictingObjects.Count; ++i) {
                // Apply a special formatting to display the class path.
                var pathSplit = m_ConflictingObjects[i].Path.Split('.');
                var headerText = m_ConflictingObjects[i].Path.Substring(0, m_ConflictingObjects[i].Path.Length - pathSplit[pathSplit.Length - 1].Length - 1);
                headerText = headerText.Replace("m_", "").Replace(" ", "").Replace("[]", "").Replace("+", ".");

                var headerLabel = new Label(headerText);
                headerLabel.tooltip = headerText;
                headerLabel.AddToClassList("header-text");
                headerLabel.style.marginTop = 4;
                scrollView.Add(headerLabel);

                var helpBox = new HelpBox($"The destination is a child of the template {objectType}.", HelpBoxMessageType.Warning);
                if (typeof(IList).IsAssignableFrom(m_ConflictingObjects[i].Field.FieldType)) {
                    var listValue = (IList)m_ConflictingObjects[i].GetValue();
                    var index = i;
                    for (int j = 0; j < listValue.Count; ++j) {
                        var elementValue = (UnityEngine.Object)listValue[j];
                        if (!IsValueTemplateChild(elementValue)) {
                            continue;
                        }

                        var elementIndex = j;
                        var destinationObjectField = new ObjectField(ObjectNames.NicifyVariableName(pathSplit[pathSplit.Length - 1]) + $" (Element {elementIndex})");
                        destinationObjectField.objectType = m_ConflictingObjects[i].Field.FieldType.GetElementType();
                        destinationObjectField.value = elementValue;
                        destinationObjectField.RegisterValueChangedCallback(c =>
                        {
                            listValue[elementIndex] = c.newValue;
                            m_ConflictingObjects[index].SetValue(listValue);
                            helpBox.style.display = IsValueTemplateChild(destinationObjectField.value) ? DisplayStyle.Flex : DisplayStyle.None;
                        });
                        destinationObjectField.AddToClassList("flex-grow");
                        scrollView.Add(destinationObjectField);

                        helpBox.style.display = IsValueTemplateChild(destinationObjectField.value) ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                } else {
                    var destinationObjectField = new ObjectField(ObjectNames.NicifyVariableName(pathSplit[pathSplit.Length - 1]));
                    destinationObjectField.objectType = m_ConflictingObjects[i].Field.FieldType;
                    destinationObjectField.value = (UnityEngine.Object)m_ConflictingObjects[i].GetValue();
                    var index = i;
                    destinationObjectField.RegisterValueChangedCallback(c =>
                    {
                        m_ConflictingObjects[index].SetValue(c.newValue);
                        helpBox.style.display = IsValueTemplateChild(destinationObjectField.value) ? DisplayStyle.Flex : DisplayStyle.None;
                    });
                    destinationObjectField.AddToClassList("flex-grow");
                    scrollView.Add(destinationObjectField);

                    helpBox.style.display = IsValueTemplateChild(destinationObjectField.value) ? DisplayStyle.Flex : DisplayStyle.None;
                }

                scrollView.Add(helpBox);
            }

            var closeButton = new Button();
            closeButton.style.marginTop = 10;
            closeButton.text = "Close";
            closeButton.clicked += () =>
            {
                CloseWindow();
            };
            rootVisualElement.Add(closeButton);
        }

        /// <summary>
        /// Is the value a child of the template object?
        /// </summary>
        /// <param name="value">The value of the object.</param>
        /// <returns>True if the value is a child of the template character.</returns>
        private bool IsValueTemplateChild(object value)
        {
            if (value == null || value.Equals(null)) {
                return false;
            }
            if (value is Component) {
                value = (value as Component).transform;
            } else if (value is GameObject) {
                value = (value as GameObject).transform;
            } else {
                return false;
            }

            return (value as Transform).IsChildOf(m_TemplateTransform) || (value as Transform) == m_TemplateTransform;
        }

        /// <summary>
        /// Traverses through all of the fields and resolves the field value the new character.
        /// </summary>
        public static void ResolveFields<T>(GameObject templateParent, GameObject targetParent, Type animatorIdentifier, object templateObject, T targetObject, List<ReferenceResolverWindow.ConflictingObjects> conflictingObjects)
        {
            if (s_TemplateBoneByTransform == null) {
                s_TemplateBoneByTransform = new Dictionary<Transform, TemplateBoneMapping>();
            } else {
                s_TemplateBoneByTransform.Clear();
            }

            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fields = templateObject.GetType().GetFields(bindingFlags);
            for (int i = 0; i < fields.Length; ++i) {
                if (!fields[i].IsPublic && fields[i].GetCustomAttribute<SerializeField>() == null && fields[i].GetCustomAttribute<SerializeReference>() == null) {
                    continue;
                }
                if (fields[i].GetCustomAttribute<UltimateCharacterController.Utility.IgnoreReferences>() != null) {
                    continue;
                }

                var index = i;
                Action<object> onChangeEvent = (object changedValue) =>
                {
                    if (!fields[index].FieldType.IsAssignableFrom(changedValue.GetType())) {
                        return;
                    }
                    fields[index].SetValue(targetObject, changedValue);
                    if (targetObject is UnityEngine.Object) {
                        Utility.EditorUtility.SetDirty(targetObject as UnityEngine.Object);
                    }
                };

                bool resolved;
                var value = ResolveFields(templateParent, targetParent, animatorIdentifier, fields[i].GetValue(templateObject), fields[i].GetValue(targetObject), fields[i].FieldType, targetObject.GetType().Name + "." + fields[i].Name, conflictingObjects, onChangeEvent, out resolved);
                if (!resolved) {
                    conflictingObjects.Add(new ReferenceResolverWindow.ConflictingObjects {
                        Object = targetObject,
                        Field = fields[i],
                        Path = targetObject.GetType().Name + "." + fields[i].Name,
                        OnChangeEvent = onChangeEvent
                    });
                }

                fields[i].SetValue(targetObject, value);
            }
        }

        /// <summary>
        /// Returns the resolved value on the new object.
        /// </summary>
        private static object ResolveFields(GameObject templateParent, GameObject targetParent, Type animatorIdentifier, object templateValue, object targetValue, Type fieldType, string path, List<ReferenceResolverWindow.ConflictingObjects> conflictingObjects, Action<object> onChangeEvent, out bool resolved)
        {
            // Value is being retrieved from reflection. Due to the unmanaged/managed Unity Object conversion != and the Equals method must be used to truly check for null.
            if (templateValue == null || templateValue.Equals(null) || typeof(ScriptableObject).IsAssignableFrom(fieldType) || 
                                typeof(Sprite).IsAssignableFrom(fieldType) || typeof(Material).IsAssignableFrom(fieldType) || typeof(AudioClip).IsAssignableFrom(fieldType)) {
                resolved = true;
                return templateValue;
            }

            // If the object is a prefab that is not part of the template then the object is resolved.
            if (typeof(GameObject).IsAssignableFrom(fieldType) && EditorUtility.IsPersistent((UnityEngine.Object)templateValue)) {
                if (!((GameObject)templateValue).transform.IsChildOf(templateParent.transform)) {
                    resolved = true;
                    return templateValue;
                }
            }

            // The default parameter assignment type may be different.
            if (templateValue.GetType().IsClass && targetValue != null && templateValue.GetType() != targetValue.GetType()) {
                targetValue = Activator.CreateInstance(templateValue.GetType());
            }

            // If the transform is a child of the parent then that value should persist.
            Transform targetTransform = null;
            if (targetValue != null && !targetValue.Equals(null)) {
                if (targetValue is Component) {
                    targetTransform = (targetValue as Component).transform;
                } else if (targetValue is GameObject) {
                    targetTransform = (targetValue as GameObject).transform;
                }
            }
            if (targetTransform != null) {
                if (targetTransform.IsChildOf(targetParent.transform)) {
                    resolved = true;
                    return targetValue;
                }
            }

            // Ensure each array element is resolved.
            if (typeof(IList).IsAssignableFrom(fieldType)) {
                Type elementType;
                if (fieldType.IsArray) {
                    elementType = fieldType.GetElementType();
                } else {
                    var baseFieldType = fieldType;
                    while (!baseFieldType.IsGenericType) {
                        baseFieldType = baseFieldType.BaseType;
                    }
                    elementType = baseFieldType.GetGenericArguments()[0];
                }
                var templateList = (IList)templateValue;
                var targetList = (IList)targetValue;
                if (templateList != null && (targetList == null || templateList.Count != targetList.Count)) {
                    // Deep copy the list so the values will not reference the same object.
                    if (fieldType.IsArray) {
                        targetList = Array.CreateInstance(elementType, templateList.Count);
                    } else if (fieldType.IsGenericType) {
                        targetList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), new object[] { templateList }) as IList;
                    } else {
                        targetList = Activator.CreateInstance(fieldType, new object[] { templateList }) as IList;
                    }
                    var originalTargetList = (IList)targetValue;
                    if (originalTargetList != null) {
                        for (int i = 0; i < originalTargetList.Count; ++i) {
                            if (i >= targetList.Count) {
                                break;
                            }

                            targetList[i] = originalTargetList[i];
                        }
                    }
                }

                // Ensure the list types match.
                if (templateList != null && targetList != null && templateList.Count == targetList.Count) {
                    for (int i = 0; i < targetList.Count; ++i) {
                        if (templateList[i] == null || templateList[i].Equals(null)) {
                            continue;
                        }
                        if (templateList[i] is ScriptableObject || templateList[i] is Sprite || templateList[i] is Material || templateList[i] is AudioClip) {
                            continue;
                        }
                        if (targetList[i] == null || templateList[i].GetType() != targetList[i].GetType()) {
                            if (templateList[i].GetType().IsPrimitive || templateList[i].GetType().IsValueType || templateList[i] is string) {
                                targetList[i] = templateList[i];
                            } else {
                                targetList[i] = Activator.CreateInstance(templateList[i].GetType(), true);
                            }
                        }
                    }
                }

                // Resolve the element.
                resolved = true;
                if (targetList != null) {
                    for (int i = 0; i < targetList.Count; ++i) {
                        var index = i;
                        var elementResolved = true;
                        targetList[i] = ResolveFields(templateParent, targetParent, animatorIdentifier, templateList[i], targetList[i], templateList[i] != null ? templateList[i].GetType() : elementType, $"{path}.Element{i}", conflictingObjects, (object changedValue) =>
                        {
                            targetList[index] = changedValue;
                            onChangeEvent(targetList);
                        }, out elementResolved);
                        resolved &= elementResolved;
                    }
                }
                return targetList;
            }

            // The strings should always be the template value.
            if (fieldType == typeof(string)) {
                resolved = true;
                return new string((string)templateValue);
            }

            // Nested objects must also be resolved.
            if (fieldType.IsClass || (fieldType.IsValueType && !fieldType.IsPrimitive)) { // Classes and structs.
                var targetObj = fieldType.IsPrimitive ? templateValue : targetValue;
                if (targetObj == null && !typeof(UnityEngine.Object).IsAssignableFrom(fieldType) && fieldType != typeof(string) && !(fieldType.IsValueType && !fieldType.IsPrimitive) && !fieldType.IsAbstract) {
                    targetObj = Activator.CreateInstance(fieldType, true);
                }

                var fields = fieldType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < fields.Length; ++i) {
                    if (!fields[i].IsPublic && fields[i].GetCustomAttribute<SerializeField>() == null && fields[i].GetCustomAttribute<SerializeReference>() == null) {
                        continue;
                    }
                    if (fields[i].GetCustomAttribute<UltimateCharacterController.Utility.IgnoreReferences>() != null) {
                        continue;
                    }

                    bool objResolved;
                    var objValue = fields[i].GetValue(templateValue);
                    var index = i;
                    var resolvedValue = ResolveFields(templateParent, targetParent, animatorIdentifier, objValue, fields[i].GetValue(targetObj), objValue != null ? objValue.GetType() : fields[i].FieldType, $"{path}.{fields[i].Name}", conflictingObjects,
                            (object changedValue) =>
                            {
                                fields[index].SetValue(targetObj, changedValue);
                                onChangeEvent(targetObj);
                            }, out objResolved);
                    fields[i].SetValue(targetObj, resolvedValue);
                    if (!objResolved) {
                        conflictingObjects.Add(new ReferenceResolverWindow.ConflictingObjects {
                            Object = targetObj,
                            Field = fields[i],
                            Path = $"{path}.{fields[i].Name}",
                            OnChangeEvent = onChangeEvent
                        });
                    }
                }
                if (targetObj != null && !targetObj.Equals(null)) {
                    templateValue = targetObj;
                }
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(fieldType)) {
                resolved = true;
                return templateValue;
            }

            // If the value is a humanoid transform referencing an animator bone then replace that bone with the new character bone.
            resolved = false;
            Transform templateTransform = null;
            if (templateValue != null) {
                if (templateValue is Component) {
                    templateTransform = (templateValue as Component).transform;
                } else if (templateValue is GameObject) {
                    templateTransform = (templateValue as GameObject).transform;
                }
            }
            if (templateTransform != null) {
                if (templateTransform.IsChildOf(templateParent.transform)) {
                    var humanoidBone = GetHumanoidBoneMapping(templateParent, targetParent, animatorIdentifier, templateTransform);
                    if (humanoidBone != null) {
                        if (templateValue is Component) {
                            targetValue = humanoidBone.GetComponent(templateValue.GetType());
                            // Add the new component if the object is a Collider.
                            if ((targetValue == null || targetValue.Equals(null)) && templateValue is Collider) {
                                targetValue = humanoidBone.gameObject.AddComponent(templateValue.GetType());
                                EditorUtility.CopySerialized(templateValue as Component, targetValue as Component);
                            }
                        } else { // GameObject.
                            targetValue = humanoidBone.gameObject;
                        }
                        resolved = true;
                        return targetValue;
                    }

                    // If the child belongs to an animator hierarchy then try to map it by relative path within the matching target hierarchy.
                    var hierarchyMapping = GetAnimatorHierarchyMapping(templateParent, targetParent, animatorIdentifier, templateTransform);
                    if (hierarchyMapping == null) {
                        hierarchyMapping = GetRelativeTransform(templateParent.transform, targetParent.transform, templateTransform);
                    }
                    if (hierarchyMapping != null) {
                        if (templateValue is Component) {
                            targetValue = hierarchyMapping.GetComponent(templateValue.GetType());
                            // Add the new component if the object is a Collider.
                            if ((targetValue == null || targetValue.Equals(null)) && templateValue is Collider) {
                                targetValue = hierarchyMapping.gameObject.AddComponent(templateValue.GetType());
                                EditorUtility.CopySerialized(templateValue as Component, targetValue as Component);
                            }
                        } else { // GameObject.
                            targetValue = hierarchyMapping.gameObject;
                        }
                        resolved = targetValue != null && !targetValue.Equals(null);
                        if (resolved) {
                            return targetValue;
                        }
                    }
                }
            }

            // Special case for the player input proxy.
            if (templateValue is Shared.Input.PlayerInput) {
                templateValue = targetParent.GetComponentInChildren(templateValue.GetType());
                resolved = templateValue != null;
            }

            return templateValue;
        }

        /// <summary>
        /// Returns the target transform using the matching animator hierarchy.
        /// </summary>
        private static Transform GetAnimatorHierarchyMapping(GameObject templateParent, GameObject targetParent, Type animatorIdentifier, Transform transform)
        {
            if (!TryGetTemplateAnimatorMapping(templateParent, animatorIdentifier, transform, out var animatorMapping, out var templateAnimatorTransform)) {
                return null;
            }

            var targetAnimatorIdentifiers = targetParent.GetComponentsInChildren(animatorIdentifier, true);
            var targetAnimatorIdentifier = GetTargetAnimatorIdentifier(targetAnimatorIdentifiers, animatorMapping.AnimatorIndex, animatorMapping.FirstPersonPerspective);
            if (targetAnimatorIdentifier == null) {
                return null;
            }

            return GetRelativeTransform(templateAnimatorTransform, targetAnimatorIdentifier.transform, transform);
        }

        /// <summary>
        /// Tries to get the template animator mapping that owns the specified transform.
        /// </summary>
        private static bool TryGetTemplateAnimatorMapping(GameObject templateParent, Type animatorIdentifier, Transform transform, out TemplateAnimatorMapping animatorMapping, out Transform templateAnimatorTransform)
        {
            animatorMapping = default;
            templateAnimatorTransform = null;

            if (templateParent == null || animatorIdentifier == null || transform == null) {
                return false;
            }

            var templateAnimatorIdentifier = GetAncestorComponent(transform, animatorIdentifier);
            if (templateAnimatorIdentifier == null) {
                return false;
            }

            templateAnimatorTransform = templateAnimatorIdentifier.transform;
            var templateAnimatorIdentifiers = templateParent.GetComponentsInChildren(animatorIdentifier, true);
            for (int i = 0; i < templateAnimatorIdentifiers.Length; ++i) {
                if (templateAnimatorIdentifiers[i] == null || templateAnimatorIdentifiers[i].transform != templateAnimatorTransform) {
                    continue;
                }

                animatorMapping = new TemplateAnimatorMapping {
                    AnimatorIndex = i,
                    FirstPersonPerspective = IsFirstPersonAnimator(templateAnimatorTransform)
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the ancestor component of the specified type.
        /// </summary>
        private static Component GetAncestorComponent(Transform transform, Type componentType)
        {
            while (transform != null) {
                var component = transform.GetComponent(componentType);
                if (component != null) {
                    return component;
                }
                transform = transform.parent;
            }

            return null;
        }

        /// <summary>
        /// Returns the target animator identifier that best matches the specified template animator identifier.
        /// </summary>
        private static Component GetTargetAnimatorIdentifier(Component[] animatorIdentifiers, int preferredIndex, bool firstPersonPerspective)
        {
            var animatorIdentifier = GetTargetAnimatorIdentifier(animatorIdentifiers, preferredIndex, firstPersonPerspective, true);
            if (animatorIdentifier != null) {
                return animatorIdentifier;
            }

            animatorIdentifier = GetTargetAnimatorIdentifier(animatorIdentifiers, preferredIndex, !firstPersonPerspective, true);
            if (animatorIdentifier != null) {
                return animatorIdentifier;
            }

            return GetTargetAnimatorIdentifier(animatorIdentifiers, preferredIndex, firstPersonPerspective, false);
        }

        /// <summary>
        /// Returns the target animator identifier that best matches the specified template animator identifier.
        /// </summary>
        private static Component GetTargetAnimatorIdentifier(Component[] animatorIdentifiers, int preferredIndex, bool firstPersonPerspective, bool matchPerspective)
        {
            if (animatorIdentifiers == null || animatorIdentifiers.Length == 0) {
                return null;
            }

            if (preferredIndex >= 0 && preferredIndex < animatorIdentifiers.Length) {
                var preferredAnimatorIdentifier = animatorIdentifiers[preferredIndex];
                if (IsMatchingAnimatorIdentifier(preferredAnimatorIdentifier, firstPersonPerspective, matchPerspective)) {
                    return preferredAnimatorIdentifier;
                }
            }

            for (int i = 0; i < animatorIdentifiers.Length; ++i) {
                if (IsMatchingAnimatorIdentifier(animatorIdentifiers[i], firstPersonPerspective, matchPerspective)) {
                    return animatorIdentifiers[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Is the animator identifier a match for the specified constraints?
        /// </summary>
        private static bool IsMatchingAnimatorIdentifier(Component animatorIdentifier, bool firstPersonPerspective, bool matchPerspective)
        {
            if (animatorIdentifier == null) {
                return false;
            }

            if (matchPerspective && IsFirstPersonAnimator(animatorIdentifier.transform) != firstPersonPerspective) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the transform at the same relative path under the target root.
        /// </summary>
        private static Transform GetRelativeTransform(Transform templateRoot, Transform targetRoot, Transform templateTransform)
        {
            if (templateRoot == null || targetRoot == null || templateTransform == null) {
                return null;
            }

            if (templateTransform != templateRoot && !templateTransform.IsChildOf(templateRoot)) {
                return null;
            }

            if (templateTransform == templateRoot) {
                return targetRoot;
            }

            var hierarchySegments = new List<RelativeHierarchySegment>();
            var currentTransform = templateTransform;
            while (currentTransform != null && currentTransform != templateRoot) {
                hierarchySegments.Add(new RelativeHierarchySegment {
                    Name = currentTransform.name,
                    SiblingIndex = GetNamedSiblingIndex(currentTransform)
                });
                currentTransform = currentTransform.parent;
            }

            for (int i = hierarchySegments.Count - 1; i >= 0; --i) {
                targetRoot = GetChildTransform(targetRoot, hierarchySegments[i].Name, hierarchySegments[i].SiblingIndex);
                if (targetRoot == null) {
                    return null;
                }
            }

            return targetRoot;
        }

        /// <summary>
        /// Returns the sibling index among siblings with the same name.
        /// </summary>
        private static int GetNamedSiblingIndex(Transform transform)
        {
            if (transform.parent == null) {
                return 0;
            }

            var siblingIndex = 0;
            for (int i = 0; i < transform.parent.childCount; ++i) {
                var child = transform.parent.GetChild(i);
                if (child.name != transform.name) {
                    continue;
                }

                if (child == transform) {
                    return siblingIndex;
                }

                siblingIndex++;
            }

            return -1;
        }

        /// <summary>
        /// Returns the child transform with the specified name and sibling index.
        /// </summary>
        private static Transform GetChildTransform(Transform parent, string name, int siblingIndex)
        {
            var matchingIndex = 0;
            for (int i = 0; i < parent.childCount; ++i) {
                var child = parent.GetChild(i);
                if (child.name != name) {
                    continue;
                }

                if (matchingIndex == siblingIndex) {
                    return child;
                }

                matchingIndex++;
            }

            return null;
        }

        /// <summary>
        /// Returns the target transform based on the template transform bone.
        /// </summary>
        private static Transform GetHumanoidBoneMapping(GameObject templateParent, GameObject targetParent, Type animatorIdentifier, Transform transform)
        {
            // Build the mapping based off of the template target if it hasn't already been built.
            if (s_TemplateBoneByTransform.Count == 0) {
                if (s_TemplateAnimatorByTransform == null) {
                    s_TemplateAnimatorByTransform = new Dictionary<Transform, TemplateAnimatorMapping>();
                } else {
                    s_TemplateAnimatorByTransform.Clear();
                }

                var templateAnimatorIdentifiers = templateParent.GetComponentsInChildren(animatorIdentifier, true);
                for (int i = 0; i < templateAnimatorIdentifiers.Length; ++i) {
                    var templateAnimator = templateAnimatorIdentifiers[i].GetComponent<Animator>();
                    if (templateAnimator == null || !templateAnimator.isHuman) {
                        continue;
                    }

                    var firstPersonPerspective = IsFirstPersonAnimator(templateAnimatorIdentifiers[i].transform);
                    s_TemplateAnimatorByTransform[templateAnimator.transform] = new TemplateAnimatorMapping {
                        AnimatorIndex = i,
                        FirstPersonPerspective = firstPersonPerspective
                    };

                    for (int j = 0; j < (int)HumanBodyBones.LastBone; ++j) {
                        var templateBone = templateAnimator.GetBoneTransform((HumanBodyBones)j);
                        if (templateBone == null || s_TemplateBoneByTransform.ContainsKey(templateBone)) {
                            continue;
                        }

                        s_TemplateBoneByTransform.Add(templateBone, new TemplateBoneMapping {
                            AnimatorIndex = i,
                            Bone = (HumanBodyBones)j,
                            FirstPersonPerspective = firstPersonPerspective
                        });
                    }
                }
            }

            // Try to convert the template transform to the target transform based on the humanoid bone.
            var targetAnimatorIdentifiers = targetParent.GetComponentsInChildren(animatorIdentifier, true);
            if (s_TemplateBoneByTransform.TryGetValue(transform, out var boneMapping)) {
                var targetAnimator = GetTargetAnimator(targetAnimatorIdentifiers, boneMapping.AnimatorIndex, boneMapping.FirstPersonPerspective, boneMapping.Bone);
                if (targetAnimator != null) {
                    return targetAnimator.GetBoneTransform(boneMapping.Bone);
                }
            }


            // The target object may be the animator GameObject itself.
            if (s_TemplateAnimatorByTransform.TryGetValue(transform, out var animatorMapping)) {
                var targetAnimator = GetTargetAnimator(targetAnimatorIdentifiers, animatorMapping.AnimatorIndex, animatorMapping.FirstPersonPerspective);
                if (targetAnimator != null) {
                    return targetAnimator.transform;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the target animator that best matches the specified template animator.
        /// </summary>
        /// <param name="animatorIdentifiers">The animator identifiers to search.</param>
        /// <param name="preferredIndex">The preferred animator index.</param>
        /// <param name="firstPersonPerspective">Is the animator in the first person hierarchy?</param>
        /// <param name="bone">The optional humanoid bone that must exist.</param>
        /// <returns>The matching animator.</returns>
        private static Animator GetTargetAnimator(Component[] animatorIdentifiers, int preferredIndex, bool firstPersonPerspective, HumanBodyBones? bone = null)
        {
            var animator = GetTargetAnimator(animatorIdentifiers, preferredIndex, firstPersonPerspective, bone, true);
            if (animator != null) {
                return animator;
            }

            animator = GetTargetAnimator(animatorIdentifiers, preferredIndex, !firstPersonPerspective, bone, true);
            if (animator != null) {
                return animator;
            }

            return GetTargetAnimator(animatorIdentifiers, preferredIndex, firstPersonPerspective, bone, false);
        }

        /// <summary>
        /// Returns the target animator that best matches the specified template animator.
        /// </summary>
        /// <param name="animatorIdentifiers">The animator identifiers to search.</param>
        /// <param name="preferredIndex">The preferred animator index.</param>
        /// <param name="firstPersonPerspective">Is the animator in the first person hierarchy?</param>
        /// <param name="bone">The optional humanoid bone that must exist.</param>
        /// <param name="matchPerspective">Should the perspective classification match?</param>
        /// <returns>The matching animator.</returns>
        private static Animator GetTargetAnimator(Component[] animatorIdentifiers, int preferredIndex, bool firstPersonPerspective, HumanBodyBones? bone, bool matchPerspective)
        {
            if (animatorIdentifiers == null || animatorIdentifiers.Length == 0) {
                return null;
            }

            if (preferredIndex >= 0 && preferredIndex < animatorIdentifiers.Length) {
                var preferredAnimator = animatorIdentifiers[preferredIndex].GetComponent<Animator>();
                if (IsMatchingAnimator(preferredAnimator, animatorIdentifiers[preferredIndex].transform, firstPersonPerspective, bone, matchPerspective)) {
                    return preferredAnimator;
                }
            }

            for (int i = 0; i < animatorIdentifiers.Length; ++i) {
                var targetAnimator = animatorIdentifiers[i].GetComponent<Animator>();
                if (IsMatchingAnimator(targetAnimator, animatorIdentifiers[i].transform, firstPersonPerspective, bone, matchPerspective)) {
                    return targetAnimator;
                }
            }

            return null;
        }

        /// <summary>
        /// Is the animator a match for the specified constraints?
        /// </summary>
        /// <param name="animator">The animator to check.</param>
        /// <param name="transform">The animator transform.</param>
        /// <param name="firstPersonPerspective">Is the source animator in the first person hierarchy?</param>
        /// <param name="bone">The optional humanoid bone that must exist.</param>
        /// <param name="matchPerspective">Should the perspective classification match?</param>
        /// <returns>True if the animator is a match.</returns>
        private static bool IsMatchingAnimator(Animator animator, Transform transform, bool firstPersonPerspective, HumanBodyBones? bone, bool matchPerspective)
        {
            if (animator == null || !animator.isHuman) {
                return false;
            }

            if (matchPerspective && IsFirstPersonAnimator(transform) != firstPersonPerspective) {
                return false;
            }

            return !bone.HasValue || animator.GetBoneTransform(bone.Value) != null;
        }

        /// <summary>
        /// Is the specified animator in the first person hierarchy?
        /// </summary>
        /// <param name="transform">The transform to check.</param>
        /// <returns>True if the transform belongs to the first person hierarchy.</returns>
        private static bool IsFirstPersonAnimator(Transform transform)
        {
#if FIRST_PERSON_CONTROLLER
            return transform.GetComponentInParent<Opsive.UltimateCharacterController.FirstPersonController.Character.FirstPersonObjects>(true) != null;
#else
            return false;
#endif
        }
    }
}
