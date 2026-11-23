using System;
using System.Collections;
using System.Collections.Generic;

namespace BT.Core.Collections;

/// <summary>A read-only collection whose iteration order is explicit and deterministic.</summary>
public sealed class OrderedSet<T> : IReadOnlyList<T>
{
    private readonly List<T> _items;

    public OrderedSet(IEnumerable<T> source, IComparer<T>? comparer = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        _items = new List<T>(source);
        _items.Sort(comparer ?? Comparer<T>.Default);
        for (var i = _items.Count - 1; i > 0; i--)
            if (EqualityComparer<T>.Default.Equals(_items[i], _items[i - 1])) _items.RemoveAt(i);
    }

    public int Count => _items.Count;
    public T this[int index] => _items[index];
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
