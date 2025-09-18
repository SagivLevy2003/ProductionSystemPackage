using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionOptionStateManager : NetworkBehaviour
{
    [SerializeField] private ProductionDatabase _database;

    // Sync only {optionId -> enabled}
    private readonly SyncDictionary<int, bool> _states = new();
    public IReadOnlyDictionary<int, bool> States => _states;

    public event Action<int /*optionId*/, bool /*enabled*/> OnClientOptionToggled;

    public override void OnStartServer()
    {
        base.OnStartServer();
        _states.Clear();
        foreach (var option in _database.Options)
        {
            if (!option) continue;

             _states[option.Id] = option.AvailabilityConfig.IsAvailable(OwnerId, ObjectId);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _states.OnChange += HandleStatesChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        _states.OnChange -= HandleStatesChanged;
    }


    [Server]
    public void QueryOptionState()
    {
        foreach (var option in _database.Options)
        {
            if (!option) continue;
            bool updatedState = option.AvailabilityConfig.IsAvailable(OwnerId, ObjectId);
            SetOptionActiveServer(option.Id, updatedState);
        }
    }


    /// <summary>
    /// Server-side API to directly change the state of an option.
    /// </summary>
    /// <param name="optionId"></param>
    /// <param name="isActive"></param>
    /// <returns>Whether the operation was successful. Assigning identical values returns false.</returns>
    [Server]
    public bool SetOptionActiveServer(int optionId, bool isActive)
    {
        if (!_database.Contains(optionId))
        {
            ProductionDebugLogger.LogMessage(this,
                $"Attempted to change state for unknown option id <color=cyan>{optionId}</color>", true);
            return false;
        }

        if (!_states.TryGetValue(optionId, out bool old) || old != isActive)
        {
            _states[optionId] = isActive;                 // add or update (syncs to clients)
            OnClientOptionToggled?.Invoke(optionId, isActive); // server-side listeners
            return true;
        }
        return false;
    }

    [Server]
    public void RefreshOption(int optionId)
    {
        if (_database.TryGetOptionById(optionId, out var opt) && opt)
            SetOptionActiveServer(optionId, opt.AvailabilityConfig.IsAvailable(OwnerId, ObjectId));
    }

    private void HandleStatesChanged(SyncDictionaryOperation op, int key, bool newVal, bool asServer)
    {
        if (asServer) return; // fire only on clients
        if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set)
            OnClientOptionToggled?.Invoke(key, newVal);
        //Can add handling of removal / clearing later on if required
    }

    public bool IsOptionEnabled(int optionId)
        => _states.TryGetValue(optionId, out var v) && v;

    public bool TryGetOptionById(int optionId, out ProductionOption option)
    {
        return _database.TryGetOptionById(optionId, out option);
    }
}