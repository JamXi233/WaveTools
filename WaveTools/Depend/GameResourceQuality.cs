using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WaveTools.Depend
{
    internal static class GameResourceQuality
    {
        internal sealed record Option(string Code, string Name)
        {
            public string DisplayName => $"{Name} {Code.ToUpperInvariant()}";
        }

        internal sealed record Installation(bool RequiresArgument, IReadOnlyList<Option> Options, string Error);

        private static readonly Option[] KnownOptions =
        {
            new("uhd", "极致"), new("hd", "高清"), new("sd", "流畅")
        };

        // Keep quoted values together so a flag inside another argument's value is not removed.
        private static readonly Regex ArgumentTokens = new(@"(?:[^\s""]|""(?:\\.|[^""\\])*"")+", RegexOptions.Compiled);

        public static string GetDirectory(string gamePath) =>
            Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(gamePath)));

        public static Installation Detect(string gamePath)
        {
            bool requiresArgument = false;
            try
            {
                if (string.IsNullOrWhiteSpace(gamePath) || gamePath.Equals("Null", StringComparison.OrdinalIgnoreCase))
                    return new(false, Array.Empty<Option>(), "请先选择游戏本体");

                string root = GetDirectory(gamePath);
                string configPath = Path.Combine(root, "launcherDownloadConfig.json");
                requiresArgument = File.Exists(configPath);
                if (!requiresArgument)
                    return new(false, Array.Empty<Option>(), "未识别");

                var config = JObject.Parse(File.ReadAllText(configPath));
                var bundles = config["bundles"] as JObject;
                // Older clients use a single installation, without per-quality bundles.
                if (config.Property("bundles") == null)
                    return new(false, Array.Empty<Option>(), "未识别");
                if (bundles == null) throw new InvalidDataException("画质配置格式错误");

                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "LocalGameResources.json")))["resource"] as JArray;
                if (manifest == null) throw new InvalidDataException("资源清单为空");
                var installed = new List<Option>();
                foreach (var option in KnownOptions)
                {
                    try
                    {
                        var bundle = bundles.GetValue(option.Code, StringComparison.OrdinalIgnoreCase) as JObject;
                        string version = (string)bundle?["version"];
                        var packs = bundle?["resourcePacks"] as JArray;
                        if (string.IsNullOrWhiteSpace(version) || packs == null ||
                            !packs.Values<string>().Contains("common", StringComparer.OrdinalIgnoreCase) ||
                            !packs.Values<string>().Contains(option.Code, StringComparer.OrdinalIgnoreCase)) continue;

                        bool configsMatch = new[] { "common", option.Code }.All(pack =>
                        {
                            string path = Path.Combine(root, "launcherDownloadConfig", pack + ".json");
                            if (!File.Exists(path)) return false;
                            var config = JObject.Parse(File.ReadAllText(path));
                            return string.Equals((string)config["packName"], pack, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals((string)config["version"], version, StringComparison.Ordinal);
                        });
                        if (!configsMatch) continue;

                        string prefix = $"Client/Content/{option.Code}/";
                        var files = manifest.OfType<JObject>().Where(file =>
                            ((string)file["dest"] ?? "").Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (files.Count == 0 || !files.Any(file => ((string)file["dest"]).EndsWith(".pak", StringComparison.OrdinalIgnoreCase))) continue;
                        bool complete = files.All(file =>
                        {
                            string relative = ((string)file["dest"]).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                            string path = Path.GetFullPath(Path.Combine(root, relative));
                            string packRoot = Path.Combine(root, "Client", "Content", option.Code) + Path.DirectorySeparatorChar;
                            if (!path.StartsWith(packRoot, StringComparison.OrdinalIgnoreCase)) return false;
                            var info = new FileInfo(path);
                            return info.Exists && info.Length == (long?)file["size"];
                        });
                        if (complete) installed.Add(option);
                    }
                    catch (Exception ex) when (IsReadError(ex))
                    {
                        Logging.Write($"Read {option.Code} resource pack failed: {ex.Message}", 1);
                    }
                }
                return new(true, installed, installed.Count == 0 ? "未找到完整画质资源，请在官方启动器完成下载或修复" : null);
            }
            catch (Exception ex) when (IsReadError(ex))
            {
                Logging.Write($"Detect game resource quality failed: {ex.Message}", 1);
                return new(requiresArgument, Array.Empty<Option>(), "无法读取画质资源，请刷新或在官方启动器检查资源");
            }
        }

        private static bool IsReadError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is Newtonsoft.Json.JsonException || ex is ArgumentException || ex is NotSupportedException ||
            ex is FormatException || ex is OverflowException || ex is InvalidCastException;

        public static string Select(Installation installation, string saved, string arguments)
        {
            string requested = GetArgumentQuality(arguments);
            return installation.Options.FirstOrDefault(option => string.Equals(option.Code, saved, StringComparison.OrdinalIgnoreCase))?.Code
                ?? installation.Options.FirstOrDefault(option => string.Equals(option.Code, requested, StringComparison.OrdinalIgnoreCase))?.Code
                ?? installation.Options.FirstOrDefault()?.Code;
        }

        private static List<(int Start, int Length, string Value)> QualityArguments(string arguments)
        {
            var result = new List<(int, int, string)>();
            var tokens = ArgumentTokens.Matches(arguments ?? "");
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i]; string value = token.Value.Trim('"');
                if (value.StartsWith("-krqlv=", StringComparison.OrdinalIgnoreCase))
                    result.Add((token.Index, token.Length, value.Substring(7).Trim('"').ToLowerInvariant()));
                else if (value.Equals("-krqlv", StringComparison.OrdinalIgnoreCase))
                {
                    int end = token.Index + token.Length;
                    string quality = "";
                    if (i + 1 < tokens.Count && !tokens[i + 1].Value.Trim('"').StartsWith('-'))
                    {
                        var next = tokens[++i]; end = next.Index + next.Length;
                        quality = next.Value.Trim('"').ToLowerInvariant();
                    }
                    result.Add((token.Index, end - token.Index, quality));
                }
            }
            return result;
        }

        public static string GetArgumentQuality(string arguments) => QualityArguments(arguments).LastOrDefault().Value;

        public static string BuildArguments(string arguments, string quality, bool dx11)
        {
            string result = arguments ?? "";
            if (!string.IsNullOrEmpty(quality))
            {
                foreach (var argument in QualityArguments(result).AsEnumerable().Reverse())
                    result = result.Remove(argument.Start, argument.Length);
                result = (result.TrimEnd() + " -krqlv=" + quality).Trim();
            }
            if (dx11) result = (result.TrimEnd() + " -dx11").Trim();
            return result;
        }
    }
}
