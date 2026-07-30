using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Streaming local de decors. Cette classe utilise volontairement SceneManager
/// directement : les cellules ne doivent contenir aucun objet gere par Netcode.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProximitySceneStreamingController : MonoBehaviour
{
    private enum CellState
    {
        Unloaded,
        Preloading,
        ReadyToActivate,
        Activating,
        Loaded
    }

    private sealed class Cell
    {
        public ProximitySceneVolume Volume;
        public CellState State;
        public AsyncOperation Operation;
    }

    [SerializeField, Min(0.05f)] private float pollingInterval = 0.25f;

    private readonly List<Cell> cells = new List<Cell>();
    private string primarySceneName;
    private float nextPollTime;
    private Cell pendingOperationCell;
    private bool acceptingRequests;

    public void BeginForPrimaryScene(string sceneName)
    {
        primarySceneName = sceneName;
        cells.Clear();
        pendingOperationCell = null;
        acceptingRequests = true;
        nextPollTime = 0f;

        ProximitySceneVolume[] volumes = FindObjectsByType<ProximitySceneVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            ProximitySceneVolume volume = volumes[i];
            if (volume == null || !volume.IsConfigured ||
                !string.Equals(volume.gameObject.scene.name, primarySceneName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cells.Add(new Cell { Volume = volume, State = CellState.Unloaded });
        }
    }

    private void Update()
    {
        if (!acceptingRequests || cells.Count == 0 || GameFlowService.Instance == null || GameFlowService.Instance.IsTransitioning)
        {
            return;
        }

        if (Time.unscaledTime < nextPollTime)
        {
            return;
        }

        nextPollTime = Time.unscaledTime + pollingInterval;
        GameObject player = LocalPlayerUtils.GetControlledCharacter();
        if (player == null)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        UpdateActiveOperation();
        TryActivateNearestReadyCell(playerPosition);
        TryStartNearestPreload(playerPosition);
    }

    public IEnumerator StopAndUnload()
    {
        acceptingRequests = false;

        // Unity ne sait pas annuler une scene arretee a 90 %. On termine donc
        // son activation derriere l'overlay avant de la liberer proprement.
        if (pendingOperationCell != null && pendingOperationCell.Operation != null && !pendingOperationCell.Operation.isDone)
        {
            pendingOperationCell.Operation.allowSceneActivation = true;
            while (!pendingOperationCell.Operation.isDone)
            {
                yield return null;
            }
            pendingOperationCell.State = CellState.Loaded;
            pendingOperationCell = null;
        }

        for (int i = cells.Count - 1; i >= 0; i--)
        {
            Cell cell = cells[i];
            if (cell == null || string.IsNullOrWhiteSpace(cell.Volume.SceneName))
            {
                continue;
            }

            Scene scene = SceneManager.GetSceneByName(cell.Volume.SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        cells.Clear();
        primarySceneName = null;
        pendingOperationCell = null;
    }

    private void UpdateActiveOperation()
    {
        if (pendingOperationCell == null || pendingOperationCell.Operation == null)
        {
            return;
        }

        if (pendingOperationCell.State == CellState.Preloading && pendingOperationCell.Operation.progress >= 0.9f)
        {
            pendingOperationCell.State = CellState.ReadyToActivate;
        }

        if (pendingOperationCell.State == CellState.Activating && pendingOperationCell.Operation.isDone)
        {
            pendingOperationCell.State = CellState.Loaded;
            pendingOperationCell.Operation = null;
            pendingOperationCell = null;
        }
    }

    private void TryActivateNearestReadyCell(Vector3 playerPosition)
    {
        if (pendingOperationCell == null || pendingOperationCell.State != CellState.ReadyToActivate)
        {
            return;
        }

        float activationDistance = pendingOperationCell.Volume.ActivationDistance;
        if ((pendingOperationCell.Volume.transform.position - playerPosition).sqrMagnitude > activationDistance * activationDistance)
        {
            return;
        }

        pendingOperationCell.Operation.allowSceneActivation = true;
        pendingOperationCell.State = CellState.Activating;
    }

    private void TryStartNearestPreload(Vector3 playerPosition)
    {
        // Une seule operation a la fois : une operation arretee a 90 % bloque
        // la file de chargement asynchrone Unity jusqu'a son activation.
        if (pendingOperationCell != null)
        {
            return;
        }

        Cell candidate = null;
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell.State != CellState.Unloaded)
            {
                continue;
            }

            float distanceSqr = (cell.Volume.transform.position - playerPosition).sqrMagnitude;
            float preloadDistance = cell.Volume.PreloadDistance;
            if (distanceSqr <= preloadDistance * preloadDistance && distanceSqr < bestDistanceSqr)
            {
                candidate = cell;
                bestDistanceSqr = distanceSqr;
            }
        }

        if (candidate == null)
        {
            return;
        }

        Scene existing = SceneManager.GetSceneByName(candidate.Volume.SceneName);
        if (existing.IsValid() && existing.isLoaded)
        {
            candidate.State = CellState.Loaded;
            return;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(candidate.Volume.SceneName, LoadSceneMode.Additive);
        if (operation == null)
        {
            Debug.LogWarning($"[ProximityScene] Impossible de precharger '{candidate.Volume.SceneName}'.", candidate.Volume);
            return;
        }

        operation.allowSceneActivation = false;
        candidate.Operation = operation;
        candidate.State = CellState.Preloading;
        pendingOperationCell = candidate;
    }
}
