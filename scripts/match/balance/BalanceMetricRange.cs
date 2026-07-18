using System;

public sealed class BalanceMetricRange
{
    public BalanceMetricRange(string key, string displayName, double minimum, double maximum)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A metric key is required.", nameof(key));
        }
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be greater than or equal to minimum.");
        }

        Key = key;
        DisplayName = displayName;
        Minimum = minimum;
        Maximum = maximum;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public double Minimum { get; }
    public double Maximum { get; }

    public bool Contains(double value)
    {
        return value >= Minimum && value <= Maximum;
    }
}
