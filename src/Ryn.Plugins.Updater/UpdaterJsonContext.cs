using System.Text.Json.Serialization;

namespace Ryn.Plugins.Updater;

[JsonSerializable(typeof(UpdateInfo))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(GitHubAsset[]))]
[JsonSerializable(typeof(Uri))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class UpdaterJsonContext : JsonSerializerContext;
