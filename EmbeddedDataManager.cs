using SpinCore.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ColoredLyrics
{
    internal class EmbeddedDataManager
    {
        public static PlayableTrackData? currentTrack;
        public static IMultiAssetSaveFile? currentFile;
        private static Dictionary<string, object> shaderData = new();
        internal static LyricConfig? lyricConfig = null;
        internal static bool hasEmbeddedData => shaderData.Count > 0 || lyricConfig != null;

        public static bool Load(PlayableTrackData playableTrackData)
        {
            IMultiAssetSaveFile? file = Util.LoadSaveFromPlayData(playableTrackData);
            if (file == null)
            {
                return false;
            }

            currentTrack = playableTrackData;
            currentFile = file;
            if (ConfigManager.embedTargetLabel != null)
            {
                ConfigManager.embedTargetLabel.ExtraText = currentFile?.FileNameNoExtension;
            }

            // Get embed data
            LoadShaderParams(file);
            LoadLyricConfig(file);
            LoadTriggersFromFile(file);


            return true;
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
                foreach (var item in shaderParams)
                {
                    ConfigManager.syncUI.Sync(item.Key, item.Value);
                }
            }
            else
            {
                // Clear materials
                ConfigManager.syncUI.Reset();
                shaderData.Clear();
                matMap.Clear();
            }
        }

        public static T GetValueFromShaderParams<T>(string key, T defaultTo)
        {
            if (!shaderData.TryGetValue(key, out var val))
            {
                return defaultTo;
            }

            if (val is T t)
            {
                return t;
            }

            return defaultTo;
        }

        static Dictionary<string, object>? TryGetShaderParamsFromTrack(IMultiAssetSaveFile file)
        {
            if (CustomChartHelper.TryGetCustomData(file, SHADER_KEY, out LyricShaderEmbedData shaderDataGen))
            {
                return shaderDataGen.ToDictionary();
            }

            return null;
        }

        internal static void SaveShaderParametersForTrack(IMultiAssetSaveFile? file, LyricShaderEmbedData data)
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


        internal static void DEBUG_SAVETRIGGER(IMultiAssetSaveFile? file)
        {
            if (file == null)
            {
                return;
            }

            LyricTriggerEmbedData data = new();
            for (int i = 1; i < 255; i++)
            {
                Color c = UnityEngine.Color.HSVToRGB(i / 255f, 1, 1).Convert();
                Debug.Log(c);
            }
            data.AddTrigger("#000001FF", new LyricTrigger(1, 10, new Color(1, 0, 0), new Color(0, 1, 0)));
            data.AddTrigger("#000001FF", new LyricTrigger(10, 10, new Color(0, 1, 0), new Color(0, 0, 1)));

            SaveTriggerDataForTrack(file, data);
        }

        // TRIGGERS
        const string TRIGGER_KEY = "LyricTriggers";
        internal static void SaveTriggerDataForTrack(IMultiAssetSaveFile? file, LyricTriggerEmbedData data)
        {
            if (file == null)
            {
                return;
            }
            if (data.triggers == null)
            {
                return;
            }

            CustomChartHelper.SetCustomData(file, TRIGGER_KEY, data, save: true);
        }

        public static Dictionary<string, List<LyricTrigger>>? LoadTriggersFromFile(IMultiAssetSaveFile? file)
        {
            if (!CustomChartHelper.TryGetCustomData(file, TRIGGER_KEY, out LyricTriggerEmbedData t))
            {
                LyricTriggers.ClearAll();
                return null;
            }

            LyricTriggers.LoadTriggers(t.triggers);
            return t.triggers;
        }

        // UI SYNC
        public static void SyncAllQuickmodEmbedUI()
        {
            if (!hasEmbeddedData) return;

            foreach (var item in shaderData)
            {
                ConfigManager.syncUI.Sync(item.Key, item.Value);
            }

            ConfigManager.syncUI.Sync("embedDefaultColor", lyricConfig?.defaultColor);
            ConfigManager.syncUI.Sync("embedFadeIn", lyricConfig?.fadeInRatio);
            ConfigManager.syncUI.Sync("embedFadeOut", lyricConfig?.fadeOutRatio);
            ConfigManager.syncUI.Sync("embedUnspokenWordAlpha", lyricConfig?.unspokenWordAlpha);
            ConfigManager.syncUI.Sync("embedSlant", lyricConfig?.slant);
            ConfigManager.syncUI.Sync("embedTextboxSize", lyricConfig?.textboxSize);
        }


        // LYRIC CONFIGS
        const string CONFIG_KEY = "LyricConfig";
        static void LoadLyricConfig(IMultiAssetSaveFile file)
        {
            lyricConfig = TryGetLyricConfigFromTrack(file, currentTrack?.Difficulty.ToString() ?? "");

            if (lyricConfig == null)
            {
                ConfigManager.syncUI.Reset();
                return;
            }

            SyncAllQuickmodEmbedUI();
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

        // MODIFY SHADER PARAMETER DURING PLAY
        public static void ModifyLUT(Color32 key, Color32 value)
        {
            if (lyricConfig == null) 
            {
                Debug.LogError("Tried to modify LUT with trigger but lyricConfig is null!");
                return;
            }

            lyricConfig?.SetLUT(key, value);
        }

        // GET CHART LYRIC MATERIAL
        static Dictionary<TMP_FontAsset, Material> matMap = new();
        public static Material? GetChartLyricMaterial(TMP_FontAsset font)
        {
            if (!hasEmbeddedData)
            {
                return null;
            }

            if (matMap.TryGetValue(font, out var mat)) return mat;

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

    public class LyricConfig
    {
        public Color defaultColor = new(1, 1, 1, 1);
        private Dictionary<Color32, Color32> lut = new();
        public float fadeInRatio = 1;
        public float fadeOutRatio = 1;
        public float unspokenWordAlpha = 1;
        public float slant = 0;
        public float textboxSize = 0;

        public LyricConfig()
        {
            defaultColor = new();
            lut = new();
        }

        public override string ToString()
        {
            return $"\n___\nDefaultColor: {defaultColor}\nFadeIn: {fadeInRatio}\nFadeOut: {fadeOutRatio}\nunspoken: {unspokenWordAlpha}\nslant: {slant}\nTextboxSize: {textboxSize}\n___\n";
        }

        public void SetLUT(Color32 key, Color32 value)
        {
            lut[key] = value;
        }

        public Color32 EvaluateLUT(Color32 key)
        {
            Color32 c = lut.GetValueOrDefault(key.WithA(255), key);

            return c.WithA(key.a);
        }
    }
}
