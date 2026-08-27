using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UiAuditUtility
{
    [MenuItem("Tools/Lit/UI/Audit Panels")]
    private static void AuditPanels()
    {
        UiPanel[] panels = Resources.FindObjectsOfTypeAll<UiPanel>();
        Dictionary<string, UiPanel> ids = new Dictionary<string, UiPanel>();
        int issues = 0;

        for (int i = 0; i < panels.Length; i++)
        {
            UiPanel panel = panels[i];
            if (panel == null || !panel.gameObject.scene.IsValid()) continue;
            if (panel.CanvasGroup == null)
            {
                Debug.LogWarning("[UI Audit] UiPanel sans CanvasGroup.", panel);
                issues++;
            }

            if (ids.TryGetValue(panel.PanelId, out UiPanel other) && other != panel)
            {
                Debug.LogWarning($"[UI Audit] Identifiant de panneau dupliqué '{panel.PanelId}'.", panel);
                issues++;
            }
            else ids[panel.PanelId] = panel;
        }

        CursorController[] cursors = Resources.FindObjectsOfTypeAll<CursorController>();
        for (int i = 0; i < cursors.Length; i++)
        {
            CursorController cursor = cursors[i];
            if (cursor == null || !cursor.gameObject.scene.IsValid()) continue;
            Graphic[] graphics = cursor.GetComponentsInChildren<Graphic>(true);
            for (int j = 0; j < graphics.Length; j++)
            {
                if (graphics[j] != null && graphics[j].raycastTarget)
                {
                    Debug.LogWarning("[UI Audit] Curseur avec un Graphic raycastable : il peut intercepter la sélection.", cursor);
                    issues++;
                    break;
                }
            }
        }

        Debug.Log($"[UI Audit] {panels.Length} panneaux déclaratifs, {cursors.Length} curseurs, {issues} problème(s) détecté(s).");
    }
}
