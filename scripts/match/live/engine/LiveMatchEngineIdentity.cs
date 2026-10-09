using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public static class LiveMatchEngineIdentity
{
    public const string EngineVersion = "2.6.1-core-behaviour";

    public static string ConfigurationFingerprint(LiveMatchEngineConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        StringBuilder source = new();
        AppendConfiguration(source, string.Empty, configuration);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AppendConfiguration(StringBuilder source, string prefix, object configuration)
    {
        PropertyInfo[] properties = configuration.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(configuration);
            string key = string.IsNullOrEmpty(prefix)
                ? property.Name
                : $"{prefix}.{property.Name}";
            if (value is null)
            {
                source.Append(key).Append("=<null>;");
            }
            else if (value is string || value.GetType().IsPrimitive || value is decimal)
            {
                source.Append(key)
                    .Append('=')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
                    .Append(';');
            }
            else
            {
                AppendConfiguration(source, key, value);
            }
        }
    }
}
