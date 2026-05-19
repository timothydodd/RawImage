using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RawImage.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(EditorSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
public partial class AppJsonContext : JsonSerializerContext;
