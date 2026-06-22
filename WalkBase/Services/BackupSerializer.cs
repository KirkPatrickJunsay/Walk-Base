using System.Text.Json;
using WalkBase.Models;

namespace WalkBase.Services;

/// <summary>Pure JSON (de)serialization for <see cref="BackupData"/>. No I/O; unit-tested.</summary>
public static class BackupSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(BackupData data) => JsonSerializer.Serialize(data, Options);

    /// <summary>Parse a backup; returns null if the text isn't valid backup JSON.</summary>
    public static BackupData? TryDeserialize(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<BackupData>(json, Options);
            // A real backup always has a player row; reject anything else.
            return data?.Player is null ? null : data;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
