
using System;

public class GameVariable<T>
{
    private T value;
    public event Action<T> OnValueChanged;

    public T Value
    {
        get => value;
        set
        {
            if (Equals(this.value, value)) return;
            prev = this.value;
            this.value = value;
            OnValueChanged?.Invoke(value);
        }
    }

    public T prev;

    public GameVariable(T init)
    {
        value = init;
        prev = init;
    }

    public GameVariable()
    {
        value = default;
        prev = default;
    }
}
