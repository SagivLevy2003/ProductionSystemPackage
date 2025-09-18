using System;
using System.Collections.Generic;
using Jabbas.ProductionSystem;

/// <summary>
/// Routes per-instance 'Set' updates (value/state changes) to listeners keyed by InstanceId.
/// Keeps ProductionClientMirror slim; slots subscribe by InstanceId.
/// </summary>
public sealed class ProductionInstanceChangeHub
{
    // instanceId -> multicast(oldItem, newItem)
    private readonly Dictionary<int, Action<MirroredProductionInstance, MirroredProductionInstance>> _byId
        = new();

    public void Subscribe(int instanceId, Action<MirroredProductionInstance, MirroredProductionInstance> cb)
    {
        if (cb == null) return;
        if (_byId.TryGetValue(instanceId, out var existing))
            _byId[instanceId] = existing + cb;
        else
            _byId[instanceId] = cb;
    }

    public void Unsubscribe(int instanceId, Action<MirroredProductionInstance, MirroredProductionInstance> cb)
    {
        if (cb == null) return;
        if (_byId.TryGetValue(instanceId, out var existing))
        {
            existing -= cb;
            if (existing == null) _byId.Remove(instanceId);
            else _byId[instanceId] = existing;
        }
    }

    /// <summary>Call when SyncListOperation.Set fires on the client.</summary>
    public void EmitSet(in MirroredProductionInstance oldItem, in MirroredProductionInstance newItem)
    {
        if (_byId.TryGetValue(newItem.InstanceId, out var cb))
            cb?.Invoke(oldItem, newItem);
    }

    /// <summary>Optional hygiene: remove listeners for a specific instance when it’s removed.</summary>
    public void OnRemoved(int instanceId) => _byId.Remove(instanceId);

    /// <summary>Optional hygiene: clear all listeners when the list is cleared.</summary>
    public void OnCleared() => _byId.Clear();
}
