using System;

public sealed class BalanceMetricRange
{
    public BalanceMetricRange(string key, string displayName, double minimum, double maximum, string gate = "")
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
        Gate = gate;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    // Phase gate this range belongs to ("C" role duties, "D" plausibility); empty for ungated ranges.
    public string Gate { get; }

    public bool Contains(double value)
    {
        return value >= Minimum && value <= Maximum;
    }
}
