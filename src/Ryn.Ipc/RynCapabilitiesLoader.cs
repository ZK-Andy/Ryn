using System.Text.Json;

namespace Ryn.Ipc;

public static class RynCapabilitiesLoader
{
    public static RynCapabilities Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ryn.json");
        if (!File.Exists(path))
            return RynCapabilities.AllowAll();

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    internal static RynCapabilities Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("capabilities", out var caps))
            return RynCapabilities.AllowAll();

        var rules = new Dictionary<string, CapabilityRule>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in caps.EnumerateObject())
        {
            var pluginName = prop.Name;

            if (prop.Value.ValueKind == JsonValueKind.True)
            {
                rules[pluginName] = new CapabilityRule { AllowAll = true };
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.False)
            {
                rules[pluginName] = new CapabilityRule { AllowAll = false };
                continue;
            }

            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                HashSet<string>? allow = null;
                HashSet<string>? deny = null;

                if (prop.Value.TryGetProperty("allow", out var allowArray)
                    && allowArray.ValueKind == JsonValueKind.Array)
                {
                    allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in allowArray.EnumerateArray())
                    {
                        if (item.GetString() is { } cmd)
                            allow.Add(cmd);
                    }
                }

                if (prop.Value.TryGetProperty("deny", out var denyArray)
                    && denyArray.ValueKind == JsonValueKind.Array)
                {
                    deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in denyArray.EnumerateArray())
                    {
                        if (item.GetString() is { } cmd)
                            deny.Add(cmd);
                    }
                }

                var allowAll = allow is null && deny is not null;
                rules[pluginName] = new CapabilityRule { AllowAll = allowAll, Allow = allow, Deny = deny };
                continue;
            }

            throw new InvalidOperationException(
                $"Invalid capability value for plugin '{pluginName}': expected true, false, or object");
        }

        return RynCapabilities.FromRules(rules);
    }
}
