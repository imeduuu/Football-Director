using System;
using System.Collections.Generic;

namespace BT.Core.Collections;

/// <summary>Deterministic ordering helpers. Equal primary keys are resolved by identifier.</summary>
public static class StableSort
{
    public static List<T> By<T, TKey, TId>(IEnumerable<T> source, Func<T, TKey> keySelector, Func<T, TId> idSelector,
        IComparer<TKey>? keyComparer = null, IComparer<TId>? idComparer = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        if (idSelector is null) throw new ArgumentNullException(nameof(idSelector));
        keyComparer ??= Comparer<TKey>.Default;
        idComparer ??= Comparer<TId>.Default;
        var result = new List<(T Item, TKey Key, TId Id, int Index)>();
        var index = 0;
        foreach (var item in source) result.Add((item, keySelector(item), idSelector(item), index++));
        result.Sort((left, right) =>
        {
            var comparison = keyComparer.Compare(left.Key, right.Key);
            if (comparison != 0) return comparison;
            comparison = idComparer.Compare(left.Id, right.Id);
            return comparison != 0 ? comparison : left.Index.CompareTo(right.Index);
        });
        var sorted = new List<T>(result.Count);
        foreach (var entry in result) sorted.Add(entry.Item);
        return sorted;
    }

    public static List<T> ById<T, TId>(IEnumerable<T> source, Func<T, TId> idSelector, IComparer<TId>? comparer = null) =>
        By(source, _ => 0, idSelector, Comparer<int>.Default, comparer);
}
