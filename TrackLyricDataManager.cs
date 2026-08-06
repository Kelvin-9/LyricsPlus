using SpinCore.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ColoredLyrics
{
    internal class TrackLyricDataManager
    {
        public static PlayableTrackData? currentTrack;
        public static IMultiAssetSaveFile? currentFile;
        private static Dictionary<string, object> shaderData = new();
        internal static LyricConfig? lyricConfig = null;
        internal static bool hasEmbeddedData => shaderData.Count > 0 || lyricConfig != null;

        public static void Load(PlayableTrackData playableTrackData)
        {
            if (!ConfigManager.config.enableColoredLyrics) return;
            if (playableTrackData.TrackDataList.Count == 0) return;

            // Get track data
            TrackData track = playableTrackData.TrackDataList[0];
            string path = track.CustomFile?.FilePath ?? "";
            if (string.IsNullOrEmpty(path))
                return;

            // Get file / directory
            string filename = Path.GetFileNameWithoutExtension(path);
            string directory = Directory.GetParent(path)?.FullName ?? "";
            if (string.IsNullOrEmpty(directory))
                return;

            string diffStr = playableTrackData.Difficulty.ToString().ToUpper();
            var files = new List<IMultiAssetSaveFile>();
            playableTrackData.GetCustomFiles(files);
            IMultiAssetSaveFile file = files.First();

            if (file is null) return;
            currentTrack = playableTrackData;
            currentFile = file;

            // Get embed data
            LoadShaderParams(file);
            LoadLyricConfig(file);
        }

        // SHADER EMBED PARAMS
        const string SHADER_KEY = "LyricShaderParams";
        static void LoadShaderParams(IMultiAssetSaveFile file)
        {
            Dictionary<string, object>? shaderParams = TryGetShaderParamsFromTrack(file);
            if (shaderParams != null)
            {
                shaderData = shaderParams;
                Debug.Log($"<{file.FileNameNoExtension}> Loaded {shaderData.Count} shader params");
            }
            else
            {
                // Clear materials
                shaderData.Clear();
                matMap.Clear();
            }
        }

        static Dictionary<string, object>? TryGetShaderParamsFromTrack(IMultiAssetSaveFile file)
        {
            if (CustomChartHelper.TryGetCustomData(file, SHADER_KEY, out LyricShaderEmbedData shaderDataGen))
            {
                return shaderDataGen.ToDictionary();
            }

            return null;
        }

        internal static void SetShaderParametersForTrack(IMultiAssetSaveFile? file, LyricShaderEmbedData data)
        {
            if (file == null)
            {
                return;
            }
            if (data.parameters == null)
            {
                return;
            }

            CustomChartHelper.SetCustomData(file, SHADER_KEY, data, save: true);
            shaderData = data.ToDictionary();

            Util.ApplyShaderParameter(matMap.Values.ToList(), shaderData);
        }

        // CHART EMBED CONFIGS
        const string CONFIG_KEY = "LyricConfig";
        static void LoadLyricConfig(IMultiAssetSaveFile file)
        {
            lyricConfig = TryGetLyricConfigFromTrack(file, currentTrack?.Difficulty.ToString() ?? "");
        }

        static LyricConfig? TryGetLyricConfigFromTrack(IMultiAssetSaveFile file, string diff)
        {
            if (CustomChartHelper.TryGetCustomData(file, CONFIG_KEY, out LyricConfig config))
            {
                return config;
            }

            return null;
        }

        internal static void SetLyricConfigForTrack(IMultiAssetSaveFile? file, LyricConfig config)
        {
            if (file == null)
            {
                return;
            }

            Debug.Log($"<{file.FileNameNoExtension}> Saving config: {config}");

            CustomChartHelper.SetCustomData(file, CONFIG_KEY, config, save: true);
            lyricConfig = config;
        }

        // GET CHART LYRIC MATERIAL
        static Dictionary<TMP_FontAsset, Material> matMap = new();
        public static Material? GetChartLyricMaterial(TMP_FontAsset font)
        {
            if (!hasEmbeddedData)
            {
                Debug.Log("Chart has no shader params");
                return null;
            }

            if (matMap.TryGetValue(font, out var mat)) return mat;

            Debug.Log("Instantiating new material with chart embed data");
            mat = new Material(ModBase.textShader);
            mat.CopyPropertiesFromMaterial(font.material);
            matMap[font] = mat;

            Util.ApplyShaderParameter(mat, shaderData);

            return mat;
        }
    }

    internal struct LyricShaderEmbedData
    {
        public List<ShaderParameter> parameters;

        public LyricShaderEmbedData()
        {
            parameters = new();
        }

        public LyricShaderEmbedData(Dictionary<string, object> p)
        {
            parameters = new();
            foreach (var kvp in p)
            {
                AddParameter(kvp.Key, kvp.Value);
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> dict = new();
            if (parameters == null)
            {
                return dict;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                object val = parameters[i].value;
                switch (parameters[i].type)
                {
                    case ShaderParamType.Float:
                        val = Convert.ToSingle(parameters[i].value); // Cast double as float
                        break;
                    case ShaderParamType.Color:
                        val = parameters[i].value is JObject jo
                            ? jo.ToObject<Color>()
                            : parameters[i].value;
                        break;
                }

                dict.Add(parameters[i].key, val);
            }

            return dict;
        }

        public void AddParameter(string key, object val)
        {
            parameters.Add(new ShaderParameter(key, val));
        }
    }

    public enum ShaderParamType
    {
        Float,
        Color
    }

    public struct ShaderParameter
    {
        public string key;
        public ShaderParamType type;
        public object value;

        public ShaderParameter(string key, object value)
        {
            this.key = key;
            if (value is float f)
            {
                type = ShaderParamType.Float;
                value = f;
            }
            else if (value is Color color)
            {
                type = ShaderParamType.Color;
                value = color;
            }
            else
            {
                // UNSUPPORTED TYPE
                type = ShaderParamType.Float;
                this.value = 0;
                return;
            }

            this.value = value;
        }
    }

    // Using my own color struct because unity color doesn't serialize well
    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color()
        {
            r = 1; g = 1; b = 1; a = 1;
        }

        public Color(float r, float g, float b, float a = 1)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public override string ToString()
        {
            return $"({r},{g},{b},{a})";
        }
    }

    public struct LyricConfig
    {
        public Color defaultColor = new(1, 1, 1, 1);
        public float fadeInRatio = 1;
        public float fadeOutRatio = 1;

        public LyricConfig()
        {

        }

        public LyricConfig(Color defaultColor, float fadeInRatio, float fadeOutRatio)
        {
            this.defaultColor = defaultColor;
            this.fadeInRatio = fadeInRatio;
            this.fadeOutRatio = fadeOutRatio;
        }

        public override string ToString()
        {
            return $"\n___\nDefaultColor: {defaultColor}\nFadeIn: {fadeInRatio}\nFadeOut: {fadeOutRatio}\n___\n";
        }
    }
}
