using UnityEngine;
using UnityEngine.UI;

public static class MenuCursorSyncUtility
{
    public static void SyncCursorToItem(CursorController cursor, RectTransform item)
    {
        if (cursor == null || item == null)
        {
            return;
        }

        RectTransform root = ResolveItemsParent(cursor, item);
        if (root != null)
        {
            cursor.itemsParent = root;
            cursor.layoutGroup = root.GetComponent<LayoutGroup>();
        }

        cursor.Refresh();
        cursor.TrySetCurrentItem(item, false);
    }

    private static RectTransform ResolveItemsParent(CursorController cursor, RectTransform item)
    {
        RectTransform currentRoot = cursor != null
            ? cursor.itemsParent != null
                ? cursor.itemsParent
                : cursor.layoutGroup != null
                    ? cursor.layoutGroup.transform as RectTransform
                    : null
            : null;

        if (IsValidRoot(currentRoot, item))
        {
            return currentRoot;
        }

        RectTransform directParent = item.parent as RectTransform;
        for (RectTransform candidate = directParent; candidate != null; candidate = candidate.parent as RectTransform)
        {
            if (candidate.GetComponent<LayoutGroup>() != null)
            {
                return candidate;
            }
        }

        return directParent;
    }

    private static bool IsValidRoot(RectTransform root, RectTransform item)
    {
        return root != null && item != null && (item == root || item.IsChildOf(root));
    }
}
