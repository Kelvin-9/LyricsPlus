using SpinCore.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;

namespace ColoredLyrics
{
    internal class EmbeddedDataManager
    {
        public static PlayableTrackData? currentTrack;
        public static IMultiAssetSaveFile? currentFile;
        internal static Dictionary<string, object> shaderData = new();
        internal static LyricConfig? lyricConfig = null;
        internal static bool hasEmbeddedData => lyricConfig != null;

        public static void Reload()
        {
            if (currentTrack == null) return;

            Load(currentTrack);
        }

        public static bool Load(PlayableTrackData playableTrackData)
        {
            IMultiAssetSaveFile? file = Util.LoadSaveFromPlayData(playableTrackData);
            if (file == null)
            {
                ConfigManager.quickModGroup?.GameObject.SetActive(false);
                return false;
            }

            currentTrack = playableTrackData;
            currentFile = file;
            if (ConfigManager.embedTargetLabel != null)
            {
                ConfigManager.embedTargetLabel.ExtraText = currentFile?.FileNameNoExtension;
            }

            // Get embed data
            LoadLyricConfigFromFile(file);

            LyricTriggers.ClearAll();
            if (!LoadTriggersFromLyrFile(playableTrackData))
            {
                LoadTriggersFromChart(file);
            }

            ConfigManager.quickModGroup?.GameObject.SetActive(true);
            return true;
        }

        // TRIGGERS
        const string TRIGGER_KEY = "LyricTriggers";
        internal static void SaveTriggerDataForTrack(IMultiAssetSaveFile? file, LyricTriggerEmbedData data)
        {
            if (file == null)
            {
                return;
            }
            if (data.colorTriggers == null)
            {
                return;
            }

            CustomChartHelper.SetCustomData(file, TRIGGER_KEY, data, save: true);
        }

        public static bool LoadTriggersFromChart(IMultiAssetSaveFile? file)
        {
            if (file == null)
            {
                return false;
            }

            CustomChartHelper.TryGetCustomData(file, TRIGGER_KEY, out LyricTriggerEmbedData t);

            LyricTriggers.LoadTriggers(t.colorTriggers, t.colorKeys, t.setTriggers, t.offsetTriggers);
            return true;
        }

        public static bool LoadTriggersFromLyrFile(PlayableTrackData playableData)
        {
            TriggerFileParser parser = new();
            bool sucessful = parser.LoadTriggersFromFile(playableData, out var colorKeys, out var colorTriggers, out var setTriggers, out var offsetTriggers);
            if (!sucessful || (colorTriggers == null && setTriggers == null))
            {
                return false;
            }

            LyricTriggers.LoadTriggers(colorTriggers, colorKeys, setTriggers, offsetTriggers);

            return true;
        }

        private const string LYRIC_TRIGGER_TEMPLATE = """
        ///  ------------------------------------
        #
        #
        #   LUT [entryName] #[color]
        #      - Creates a lookup table entry named [entryName] for color #[color]
        #      - It is recommended to use colors that you are not going to use but will still look good for non-mod users, like reserving #01FFFF, #02FFFF etc to look up table keys
        #      - Note that the lyric editor will automatically replace any instance of [entryName] to the respective #[color]
        #
        #   COLOR [LUTentry] [time] #[StartValue] #[EndValue] [duration]
        #      - Replaces all text's color with color tag corresponding to the LUT entry's color to the new color #[StartValue] and transitions to #[EndValue] over [duration]
        #      - For example, if you declared 
        #            LUT color1 #000001
        #            COLOR color1 10.0 #FF0000 #00FF00 15.0
        #      - ^ This would make all texts with tag <color=#000001> turn red #FF0000 at time 10.0 then transition to green #00FF00 over the next 15 seconds
        #      - Make sure that the LUT command is called before using COLOR for that entry
        #   COLOR [LUTentry] [time] #[StartValue]
        #      - Change color of [LUTentry] to #[StartValue] at [time]
        #
        #   SET [variable] [time] [startValue] [endValue] [duration]
        #      - Smoothly curves [variable] at [time] from [startValue] to [endValue] over [duration]
        #   SET [variable] [time] [value]
        #      - Sets a number variable [variable] to [number] at [time]
        #      - Variable names: FADEIN, FADEOUT, UNSPOKENALPHA, SLANT, TEXTBOXSIZE
        #
        #   OFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration] [easing]>
        #      - Offsets the position of all text with the given [LUTindex]
        #      - Give the offsets in the form of x,y,z or (x,y,z)
        #      - Optionally give an [easing] parameter
        #           EASEIN EASEOUT
        #
        ///  ------------------------------------




        # LUT ENTRIES
        LUT color1 #01FFFF
        

        # COLOR TRIGGERS
        COLOR color1 0.0 #FFFFFF


        # OTHER STUFF


        """;

        internal static void CreateLyricTriggerFileForCurrentTrack()
        {
            if (currentTrack == null) return;

            (string? directory, string? fileName) = Util.GetDirectoryFromPlayData(currentTrack);
            if (directory == null) return;
            if (fileName == null) return;

            string path = Path.Combine(directory, fileName + ".lyr");
            if (!File.Exists(path))
            {
                // Create file
                try
                {
                    using (StreamWriter writer = new StreamWriter(path))
                    {
                        writer.Write(LYRIC_TRIGGER_TEMPLATE);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create .lyr file: {e.Message}");
                    return;
                }
            }

            // Open file
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception e) 
            {
                Debug.LogError($"Failed to open .lyr file: {e.Message}");
                return;
            }
        }


        // UI SYNC
        public static void SyncAllQuickmodEmbedUI()
        {
            if (!hasEmbeddedData) 
            {
                Debug.Log("No embedded data, skipping sync");
            }

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
        static void LoadLyricConfigFromFile(IMultiAssetSaveFile file)
        {
            CustomChartHelper.TryGetCustomData(file, CONFIG_KEY, out LyricConfig conf);

            lyricConfig = conf;
            if (lyricConfig == null)
            {
                Debug.Log("No lyric config found");
                ConfigManager.syncUI.Reset();
                shaderData.Clear();
                matMap.Clear();
                return;
            }

            shaderData = lyricConfig.GetParameterDictionary();

            Debug.Log($"<{file.FileNameNoExtension}> Loaded {shaderData.Count} shader params");
            Debug.Log($"{lyricConfig}");

            SyncAllQuickmodEmbedUI();
        }

        internal static void SetLyricConfigForFile(IMultiAssetSaveFile? file, LyricConfig config)
        {
            if (file == null)
            {
                Debug.LogError("NO FILE TO SET LYRIC CONFIG");
                return;
            }

            Debug.Log($"<{file.FileNameNoExtension}> Saving config: {config}");

            CustomChartHelper.SetCustomData(file, CONFIG_KEY, config, save: true);
            shaderData = config.GetParameterDictionary();
            Util.ApplyShaderParameter(matMap.Values.ToList(), shaderData);

            lyricConfig = config;
        }

        // TRIGGER EFFECTS
        public static void ModifyLUT(Color32 key, Color32 value)
        {
            if (lyricConfig == null) 
            {
                Debug.LogError("Tried to modify LUT with trigger but lyricConfig is null!");
                return;
            }

            lyricConfig.SetLUTColor(key, value);
        }

        public static void ModifyOffset(Color32 key, Vector3 offset)
        {
            if (lyricConfig == null)
            {
                Debug.LogError("Tried to modify LUT with trigger but lyricConfig is null!");
                return;
            }

            Debug.Log($"MOdifying offset of key {key} to {offset}");
            lyricConfig.SetLUTOffset(key, offset);
        }

        public static void SetVariable(string key, float value)
        {
            if (lyricConfig == null)
            {
                Debug.LogError($"Tried to modify variable {key} but lyricConfig is null!");
                return;
            }


            lyricConfig.SetVariable(key, value);
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
        public Color defaultColor = new(1, 1, 1, 1); // All text with #FFFFFF not controlled by a LUT is replaced with defaultColor
        
        // LUT
        private Dictionary<Color32, Color32> lutColor  = new();
        private Dictionary<Color32, Vector3> lutOffset = new();

        public List<ShaderParameter> parameters = new();

        // Variables
        public float fadeInRatio = 1;
        public float fadeOutRatio = 1;
        public float unspokenWordAlpha = 1;
        public float slant = 0.2f;
        public float textboxSize = 0;

        public LyricConfig()
        {

        }

        public LyricConfig(Dictionary<string, object> p)
        {
            defaultColor = new();
            SetParameters(p);
        }
        public override string ToString()
        {
            return $"\n___\nDefaultColor: {defaultColor}\nFadeIn: {fadeInRatio}\nFadeOut: {fadeOutRatio}\nunspoken: {unspokenWordAlpha}\nslant: {slant}\nTextboxSize: {textboxSize}\n___\n";
        }

        public void SetParameters(Dictionary<string, object> p)
        {
            parameters = new();
            foreach (var kvp in p)
            {
                AddParameter(kvp.Key, kvp.Value);
            }
        }

        public Dictionary<string, object> GetParameterDictionary()
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

        public void SetLUTColor(Color32 key, Color32 value)
        {
            lutColor[key] = value;
        }

        public void SetLUTOffset(Color32 key, Vector3 offset)
        {
            lutOffset[key] = offset;
        }

        public void SetVariable(string key, float value)
        {
            switch (key.ToUpper()) 
            {
                case "FADEIN":
                    fadeInRatio = value;
                    break;
                case "FADEOUT":
                    fadeOutRatio = value;
                    break;
                case "UNSPOKENALPHA":
                    unspokenWordAlpha = value;
                    break;
                case "SLANT":
                    slant = value;
                    break;
                case "TEXTBOXSIZE":
                    textboxSize = value;
                    break;
            }
        }

        public Color32 EvaluateLUTColor(Color32 key)
        {
            Color32 c = lutColor.GetValueOrDefault(key.WithA(255), key);

            return c.WithA(key.a);
        }

        public Vector3 EvaluateLUTOffset(Color32 key)
        {
            return lutOffset.GetValueOrDefault(key.WithA(255), Vector3.zero);
        }
    }
}
