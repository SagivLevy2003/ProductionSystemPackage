using System;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Jabbas.ProductionSystem;
using UnityEngine;

[RequireComponent(typeof(ProductionHandler))]
public class ProductionClientMirror : NetworkBehaviour
{
    /* =========================
     *  NETWORKED MIRROR (Sync)
     * ========================= */
    [AllowMutableSyncType]
    [SerializeField] private SyncList<MirroredProductionInstance> _mirroredQueue = new();
    public IReadOnlyList<MirroredProductionInstance> MirroredProductionQueue => _mirroredQueue;

    /* ================
     *  SERVER SOURCE
     * ================ */
    private IdQueue<ProductionInstance> _originalQueue;

    // Server-only helpers
    private readonly Dictionary<int /*InstanceId*/, int /*index*/> _indexByInstanceId = new();
    private readonly Dictionary<int /*InstanceId*/, ProductionInstance> _instanceById = new();

    //Cached for removal on the host in order to avoid sending default parameters
    //(since the data is deleted before the event fires on the client side of the host)
    private readonly Queue<(int formerIndex, MirroredProductionInstance snap)> _pendingRemovals = new();

    /* ======================
     *  CLIENT EVENTS
     * ====================== */
    public event Action<SyncListOperation, int, MirroredProductionInstance, MirroredProductionInstance> OnQueueChanged;
    public event Action<int, MirroredProductionInstance> OnInstanceAdded;
    public event Action<int, MirroredProductionInstance> OnInstanceRemoved;
    public event Action OnCleared;

    // Per-instance Set routing for UI
    public ProductionInstanceChangeHub InstanceHub { get; } = new();

    private void Awake()
    {
        var handler = GetComponent<ProductionHandler>();
        _originalQueue = handler.Queue;
    }

    /* ===========================
     *  SERVER MIRRORING LOGIC
     * =========================== */

    public override void OnStartServer()
    {
        base.OnStartServer();

        _mirroredQueue.Clear();
        _indexByInstanceId.Clear();
        _instanceById.Clear();

        // initial snapshot
        for (int i = 0; i < _originalQueue.Count; i++)
        {
            var entry = _originalQueue.Entries[i];
            var inst = entry.Value;

            _mirroredQueue.Add(BuildMirror(inst));
            _indexByInstanceId[inst.Id] = i;
            _instanceById[inst.Id] = inst;

            SubscribeInstanceEvents(inst);
        }

        // structural hooks
        _originalQueue.ItemEnqueued += OnSrc_ItemEnqueued;
        _originalQueue.ItemRemoved += OnSrc_ItemRemoved;
        _originalQueue.ItemMoved += OnSrc_ItemMoved;   // server can still reorder; clients will see it as remove+add
        _originalQueue.QueueCleared += OnSrc_QueueCleared;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (_originalQueue != null)
        {
            _originalQueue.ItemEnqueued -= OnSrc_ItemEnqueued;
            _originalQueue.ItemRemoved -= OnSrc_ItemRemoved;
            _originalQueue.ItemMoved -= OnSrc_ItemMoved;
            _originalQueue.QueueCleared -= OnSrc_QueueCleared;
        }

        foreach (var kv in _instanceById)
            UnsubscribeInstanceEvents(kv.Value);

        _instanceById.Clear();
        _indexByInstanceId.Clear();
    }

    private static MirroredProductionInstance BuildMirror(ProductionInstance inst)
    {
        return new MirroredProductionInstance(
            optionId: inst.OptionId,
            instanceId: inst.Id,
            requestOwnerId: inst.RequestOwnerId,
            sourceId: inst.SourceId,
            progress: inst.Progress,
            completed: inst.IsCompleted,
            paused: inst.IsPaused
        );
    }

    // ---- source queue handlers (server) ----
    [Server]
    private void OnSrc_ItemEnqueued(int instanceId, ProductionInstance inst, int index)
    {
        _instanceById[instanceId] = inst;
        SubscribeInstanceEvents(inst);

        var mirror = BuildMirror(inst);
        if (index >= 0 && index <= _mirroredQueue.Count) _mirroredQueue.Insert(index, mirror);
        else _mirroredQueue.Add(mirror);

        RebuildIndexMap();
    }


    [Server]
    private void OnSrc_ItemRemoved(int instanceId, ProductionInstance inst, int formerIndex)
    {
        // Find the actual index currently holding this instance.
        int idx = -1;
        if (_indexByInstanceId.TryGetValue(instanceId, out int mapped) &&
            mapped >= 0 && mapped < _mirroredQueue.Count &&
            _mirroredQueue[mapped].InstanceId == instanceId)
        {
            idx = mapped;
        }
        else if (formerIndex >= 0 && formerIndex < _mirroredQueue.Count &&
                 _mirroredQueue[formerIndex].InstanceId == instanceId)
        {
            idx = formerIndex;
        }

        // Cache a snapshot so the host's client-path can use it.
        if (idx != -1)
            _pendingRemovals.Enqueue((idx, _mirroredQueue[idx]));

        // Perform the removal on the sync list.
        if (idx != -1)
            _mirroredQueue.RemoveAt(idx);

        if (_instanceById.Remove(instanceId, out var pi))
            UnsubscribeInstanceEvents(pi);

        RebuildIndexMap();
    }


    [Server]
    private void OnSrc_ItemMoved(int instanceId, int fromIndex, int toIndex)
    {
        // still update the server list; clients will just see remove+insert
        if (_indexByInstanceId.TryGetValue(instanceId, out int current) &&
            current >= 0 && current < _mirroredQueue.Count)
        {
            var item = _mirroredQueue[current];
            _mirroredQueue.RemoveAt(current);
            _mirroredQueue.Insert(Mathf.Clamp(toIndex, 0, _mirroredQueue.Count), item);
            RebuildIndexMap();
            return;
        }

        if (fromIndex >= 0 && fromIndex < _mirroredQueue.Count &&
            _mirroredQueue[fromIndex].InstanceId == instanceId)
        {
            var item = _mirroredQueue[fromIndex];
            _mirroredQueue.RemoveAt(fromIndex);
            _mirroredQueue.Insert(Mathf.Clamp(toIndex, 0, _mirroredQueue.Count), item);
            RebuildIndexMap();
        }
    }

    [Server]
    private void OnSrc_QueueCleared()
    {
        _mirroredQueue.Clear();

        foreach (var kv in _instanceById)
            UnsubscribeInstanceEvents(kv.Value);

        _instanceById.Clear();
        _indexByInstanceId.Clear();
    }

    // ---- per-instance event mirroring (server) ----
    [Server]
    private void SubscribeInstanceEvents(ProductionInstance inst)
    {
        inst.OnProgressThrottled += OnInstanceProgressThrottled;
        inst.OnCompleted += OnInstanceCompleted;
    }

    [Server]
    private void UnsubscribeInstanceEvents(ProductionInstance inst)
    {
        inst.OnProgressThrottled -= OnInstanceProgressThrottled;
        inst.OnCompleted -= OnInstanceCompleted;
    }

    [Server]
    private void OnInstanceProgressThrottled(ProductionInstance inst, float prev, float curr)
    {
        if (!_indexByInstanceId.TryGetValue(inst.Id, out int idx))
        {
            RebuildIndexMap();
            if (!_indexByInstanceId.TryGetValue(inst.Id, out idx)) return;
        }
        if (idx < 0 || idx >= _mirroredQueue.Count) return;

        _mirroredQueue[idx] = BuildMirror(inst); // -> Set
    }

    [Server]
    private void OnInstanceCompleted(ProductionInstance inst)
    {
        if (!_indexByInstanceId.TryGetValue(inst.Id, out int idx))
        {
            RebuildIndexMap();
            if (!_indexByInstanceId.TryGetValue(inst.Id, out idx)) return;
        }
        if (idx < 0 || idx >= _mirroredQueue.Count) return;

        _mirroredQueue[idx] = new MirroredProductionInstance(
            optionId: inst.OptionId,
            instanceId: inst.Id,
            requestOwnerId: inst.RequestOwnerId,
            sourceId: inst.SourceId,
            progress: 1f,
            completed: true,
            paused: inst.IsPaused
        ); // -> Set
    }

    private void RebuildIndexMap()
    {
        _indexByInstanceId.Clear();
        for (int i = 0; i < _mirroredQueue.Count; i++)
            _indexByInstanceId[_mirroredQueue[i].InstanceId] = i;
    }

    /* =========================
     *  CLIENT MIRRORING LOGIC
     * ========================= */
    public override void OnStartClient()
    {
        base.OnStartClient();
        _mirroredQueue.OnChange += FireQueueChangeEvent;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _mirroredQueue.OnChange -= FireQueueChangeEvent;
        InstanceHub.OnCleared();
    }

    private void FireQueueChangeEvent(
        SyncListOperation op,
        int index,
        MirroredProductionInstance oldItem,
        MirroredProductionInstance newItem,
        bool asServer)
    {
        if (asServer) return;

        OnQueueChanged?.Invoke(op, index, oldItem, newItem);

        switch (op)
        {
            case SyncListOperation.Add:
            case SyncListOperation.Insert:
                OnInstanceAdded?.Invoke(index, newItem);
                break;

            case SyncListOperation.Set:
                InstanceHub.EmitSet(oldItem, newItem);
                break;

            case SyncListOperation.RemoveAt:
                {
                    var payload = oldItem;

                    // On host, FishNet may deliver default oldItem. Use the server-cached snapshot.
                    if (payload.InstanceId == 0 && _pendingRemovals.Count > 0)
                    {
                        var (formerIndex, snap) = _pendingRemovals.Dequeue();
                        // Optionally verify indices match (they usually do); if not, still use snap.
                        payload = snap;
                    }

                    OnInstanceRemoved?.Invoke(index, payload);
                    if (payload.InstanceId != 0)
                        InstanceHub.OnRemoved(payload.InstanceId);
                    break;
                }

            case SyncListOperation.Clear:
                OnCleared?.Invoke();
                InstanceHub.OnCleared();
                _pendingRemovals.Clear(); // hygiene
                break;
        }
    }


    /* =========================
     *   CLIENT HELPERS
     * ========================= */
    public bool TryGetByInstanceId(int instanceId, out int index, out MirroredProductionInstance item)
    {
        for (int i = 0; i < _mirroredQueue.Count; i++)
        {
            var row = _mirroredQueue[i];
            if (row.InstanceId == instanceId)
            {
                index = i; item = row; return true;
            }
        }
        index = -1; item = default; return false;
    }
}
