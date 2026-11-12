
using System;
using System.Collections;
using System.Collections.Generic;

public class GameList<T> : IEnumerable<T>
{
    public struct ListEvent
    {
        public enum EventType
        {
            Value,
            Add,
            Insert,
            Remove,
            RemoveAt,
            Clear,
            Full
        }

        public EventType Type;
        public int Index;
        public T Value;
        public T PreviousValue;

        public ListEvent(EventType type, int index, T value, T previousValue = default)
        {
            Type = type;
            Index = index;
            Value = value;
            PreviousValue = previousValue;
        }
    }

    private readonly List<T> items = new();

    public event Action<ListEvent> OnListChanged;

    public int Count => items.Count;

    public T this[int index]
    {
        get => items[index];
        set
        {
            T previousValue = items[index];

            if (EqualityComparer<T>.Default.Equals(previousValue, value))
                return;

            items[index] = value;

            OnListChanged?.Invoke(new ListEvent(
                ListEvent.EventType.Value, index, value, previousValue));
        }
    }

    public void Add(T item)
    {
        items.Add(item);

        OnListChanged?.Invoke(new ListEvent(
            ListEvent.EventType.Add, items.Count - 1, item));
    }

    public void Insert(int index, T item)
    {
        items.Insert(index, item);

        OnListChanged?.Invoke(new ListEvent(
            ListEvent.EventType.Insert, index, item));
    }

    public void Remove(T item)
    {
        int index = items.IndexOf(item);

        if (index < 0)
            return;

        RemoveAt(index);
    }

    public void RemoveAt(int index)
    {
        T item = items[index];
        items.RemoveAt(index);

        OnListChanged?.Invoke(new ListEvent(
            ListEvent.EventType.RemoveAt, index, item));
    }

    public void Clear()
    {
        items.Clear();

        OnListChanged?.Invoke(new ListEvent(
            ListEvent.EventType.Clear, -1, default));
    }

    public void NotifyFull()
    {
        OnListChanged?.Invoke(new ListEvent(
            ListEvent.EventType.Full, -1, default));
    }

    public bool Contains(T item) => items.Contains(item);

    public int IndexOf(T item) => items.IndexOf(item);

    public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
