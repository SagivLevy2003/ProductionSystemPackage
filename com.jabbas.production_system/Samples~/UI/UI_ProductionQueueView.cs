using UnityEngine.Assertions;
using System.Collections.Generic;
using UnityEngine;
using Jabbas.ProductionSystem;

public sealed class UI_ProductionQueueView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private ProductionClientMirror _mirror;               // On same GO as handler
    [SerializeField] private ProductionOptionStateManager _options;        // For DisplayName lookup
    [SerializeField] private ProductionHandler _handler;                   // For cancel button (optional)
    [SerializeField] private UI_ProductionSlot _slotPrefab;
    [SerializeField] private Transform _container;                         // e.g., VerticalLayoutGroup

    // instanceId -> slot
    private readonly Dictionary<int, UI_ProductionSlot> _byId = new();

    private void Awake()
    {
        Assert.IsNotNull(_mirror, "[QueueView] Missing ProductionClientMirror");
        Assert.IsNotNull(_options, "[QueueView] Missing ProductionOptionStateManager");
        Assert.IsNotNull(_slotPrefab, "[QueueView] Missing Slot Prefab");
        Assert.IsNotNull(_container, "[QueueView] Missing Container");
    }

    private void OnEnable()
    {
        // Subscribe to structural changes (no Move anymore)
        _mirror.OnInstanceAdded += HandleAdded;
        _mirror.OnInstanceRemoved += HandleRemoved;
        _mirror.OnCleared += HandleCleared;

        // Initial paint (late join safe)
        RebuildAll();
    }

    private void OnDisable()
    {
        _mirror.OnInstanceAdded -= HandleAdded;
        _mirror.OnInstanceRemoved -= HandleRemoved;
        _mirror.OnCleared -= HandleCleared;

        ClearAll();
    }

    private void RebuildAll()
    {
        ClearAll();
        var rows = _mirror.MirroredProductionQueue;
        for (int i = 0; i < rows.Count; i++)
            HandleAdded(i, rows[i]);
    }

    private void ClearAll()
    {
        foreach (var kv in _byId)
        {
            var slot = kv.Value;
            if (!slot) continue;
            slot.Unbind(_mirror.InstanceHub);
            Destroy(slot.gameObject);
        }
        _byId.Clear();
    }

    /* ---------------- handlers ---------------- */

    private void HandleAdded(int index, MirroredProductionInstance row)
    {
        if (_byId.ContainsKey(row.InstanceId))
            return;

        var slot = Instantiate(_slotPrefab, _container);
        slot.transform.SetSiblingIndex(Mathf.Clamp(index, 0, _container.childCount));

        // Display name lookup
        string nameText = _options.TryGetOptionById(row.OptionId, out var opt)
            ? opt.DisplayName
            : $"Option {row.OptionId}";

        // Optional cancel wiring
        System.Action<int> cancelAction = null;
        if (_handler != null)
        {
            cancelAction = instId => _handler.Request_CancelProduction(instId);
        }

        // Bind to InstanceHub for live Set updates
        slot.Bind(row, nameText, _mirror.InstanceHub, cancelAction);

        _byId[row.InstanceId] = slot;
    }

    private void HandleRemoved(int index, MirroredProductionInstance row)
    {
        Debug.Log(row.InstanceId);
        if (_byId.TryGetValue(row.InstanceId, out var slot))
        {
            slot.Unbind(_mirror.InstanceHub);
            Destroy(slot.gameObject);
            _byId.Remove(row.InstanceId);
        }
    }

    private void HandleCleared() => ClearAll();
}
