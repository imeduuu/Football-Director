using System;
using System.Collections;
using System.Collections.Generic;

namespace BT.Core.Collections;

/// <summary>A read-only dictionary that exposes keys and entries in key order.</summary>
public sealed class OrderedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _values;
    private readonly List<TKey> _keys;

    public OrderedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> entries, IComparer<TKey>? comparer = null)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));
        _values = new Dictionary<TKey, TValue>();
        foreach (var entry in entries) _values.Add(entry.Key, entry.Value);
        _keys = new List<TKey>(_values.Keys);
        comparer ??= Comparer<TKey>.Default;
        _keys.Sort(comparer);
        for (var i = 1; i < _keys.Count; i++)
            if (comparer.Compare(_keys[i - 1], _keys[i]) == 0)
                throw new ArgumentException("The comparer must define a deterministic order for distinct keys.", nameof(comparer));
    }

    public int Count => _keys.Count;
    public IEnumerable<TKey> Keys => _keys;
    public IEnumerable<TValue> Values
    {
        get { foreach (var key in _keys) yield return _values[key]; }
    }
    public TValue this[TKey key] => _values[key];
    public bool ContainsKey(TKey key) => _values.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _values.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var key in _keys) yield return new KeyValuePair<TKey, TValue>(key, _values[key]);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
