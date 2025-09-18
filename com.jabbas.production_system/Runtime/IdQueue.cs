using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


[Serializable]
public sealed class IdQueue<T>
{
    // Config
    [field: SerializeField] public int MaxQueueSize { get; private set; } = 0;
    

    [SerializeField] private List<Entry<T>> _items = new();
    private readonly Dictionary<int, int> _indexById = new();


    // Events
    public event Action<int, T, int> ItemEnqueued;          // (id, value, index)
    public event Action<int, T, int> ItemRemoved;           // (id, value, formerIndex)
    public event Action<int, int, int> ItemMoved;           // (id, fromIndex, toIndex)
    public event Action QueueCleared;
    public event Action QueueChanged;


    // Queries
    public int Count => _items.Count;
    public bool IsFull => MaxQueueSize > 0 && Count >= MaxQueueSize;

    public IReadOnlyList<Entry<T>> Entries => _items; // id+value view
    public IEnumerable<T> Values                          // value-only view
    {
        get { foreach (var e in _items) yield return e.Value; }
    }


    public bool ContainsId(int id) => _indexById.ContainsKey(id);
    public int IndexOfId(int id) => _indexById.TryGetValue(id, out var i) ? i : -1;

    public bool TryGetById(int id, out T value)
    {
        if (_indexById.TryGetValue(id, out int idx)) { value = _items[idx].Value; return true; }
        value = default; return false;
    }

    public bool TryGetEntryById(int id, out Entry<T> entry)
    {
        if (_indexById.TryGetValue(id, out int idx)) { entry = _items[idx]; return true; }
        entry = default; return false;
    }

    public bool TryPeek(out Entry<T> entry)
    {
        if (_items.Count == 0) { entry = default; return false; }
        entry = _items[0]; return true;
    }

    // Mutations
    public bool Enqueue(int id, T value)
    {
        if (IsFull) return false;
        if (_indexById.ContainsKey(id)) return false;

        int index = _items.Count;
        _items.Add(new Entry<T>(id, value));
        _indexById[id] = index;

        ItemEnqueued?.Invoke(id, value, index);
        QueueChanged?.Invoke();
        return true;
    }

    public bool TryDequeue(out Entry<T> entry)
    {
        if (_items.Count == 0) { entry = default; return false; }
        entry = _items[0];
        RemoveAtInternal(0);
        return true;
    }

    public bool RemoveById(int id)
    {
        if (!_indexById.TryGetValue(id, out int idx)) return false;
        RemoveAtInternal(idx);
        return true;
    }

    public bool Move(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return true;
        if ((uint)fromIndex >= (uint)_items.Count) return false;
        if ((uint)toIndex >= (uint)_items.Count) return false;

        var item = _items[fromIndex];
        _items.RemoveAt(fromIndex);
        _items.Insert(toIndex, item);

        // Fix indices for affected range
        int lo = Math.Min(fromIndex, toIndex);
        int hi = Math.Max(fromIndex, toIndex);
        for (int i = lo; i <= hi; i++) _indexById[_items[i].Id] = i;

        ItemMoved?.Invoke(item.Id, fromIndex, toIndex);
        QueueChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        _items.Clear();
        _indexById.Clear();
        QueueCleared?.Invoke();
        QueueChanged?.Invoke();
    }

    // Utilities
    private void RemoveAtInternal(int index, bool fireQueueChanged = true)
    {
        var removed = _items[index];
        _items.RemoveAt(index);
        _indexById.Remove(removed.Id);

        // Shift subsequent indices
        for (int i = index; i < _items.Count; i++)
            _indexById[_items[i].Id] = i;

        ItemRemoved?.Invoke(removed.Id, removed.Value, index);
        if (fireQueueChanged) QueueChanged?.Invoke();
    }

    // Optional helper to copy top N values into an array without allocs
    public int CopyTopValues(int count, T[] dest)
    {
        if (dest == null) throw new ArgumentNullException(nameof(dest));
        if (count < 0) count = 0;
        int end = Math.Min(count, Math.Min(dest.Length, _items.Count));
        for (int i = 0; i < end; i++) dest[i] = _items[i].Value;
        return end;
    }

    // Optional: copy top N entries (id+value)
    public int CopyTopEntries(int count, Entry<T>[] dest)
    {
        if (dest == null) throw new ArgumentNullException(nameof(dest));
        if (count < 0) count = 0;
        int end = Math.Min(count, Math.Min(dest.Length, _items.Count));
        for (int i = 0; i < end; i++) dest[i] = _items[i];
        return end;
    }

    public void ChangeMaxSize(int newSize)
    {
        MaxQueueSize = newSize;

        // <= 0 means "unlimited" in your IsFull logic — don't trim.
        if (MaxQueueSize <= 0) return;

        bool removedAny = false;
        while (_items.Count > MaxQueueSize)
        {
            RemoveAtInternal(MaxQueueSize, fireQueueChanged: false); // fires ItemRemoved per item
            removedAny = true;
        }

        if (removedAny) QueueChanged?.Invoke(); // single batch change event
    }
}

[Serializable]
public struct Entry<T>
{
    [field: SerializeField] public int Id { get; private set; }
    [field: SerializeReference] public T Value { get; private set; }
    public Entry(int id, T value) { Id = id; Value = value; }
    public readonly void Deconstruct(out int id, out T value) { id = Id; value = Value; }
}
