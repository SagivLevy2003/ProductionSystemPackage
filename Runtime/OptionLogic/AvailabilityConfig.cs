using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class AvailabilityConfig
{
    [SerializeField]
    [SerializeReference, SerializeReferenceDropdown]
    private IAvailabilityRule[] _rules;

    public bool IsAvailable(int requestOwnerId, int sourceId, out List<string> messages)
    {
        bool failed = false;

        messages = new();

        foreach (var rule in _rules)
        {
            if (rule == null) continue;

            if (!rule.IsAvailable(requestOwnerId, sourceId, out string message))
            {
                failed = true;
                if (!string.IsNullOrEmpty(message)) messages.Add(message);
            }
        }

        return !failed;
    }

    public bool IsAvailable(int requestOwnerId, int sourceId)
    {
        foreach (var rule in _rules)
        {
            if (rule == null) continue;
            if (!rule.IsAvailable(requestOwnerId, sourceId, out string _)) return false;
        }

        return true;
    }
}