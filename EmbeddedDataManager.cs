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
        internal static Dictionary<string, object> shaderData = [];
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

            if (!CustomChartHelper.TryGetCustomData(file, TRIGGER_KEY, out LyricTriggerEmbedData t)) return false;

            LyricTriggers.LoadTriggers(t.colorTriggers, t.colorKeys, t.setTriggers, t.offsetTriggers);
            return true;
        }

        public static bool LoadTriggersFromLyrFile(PlayableTrackData playableData)
        {
            TriggerFileParser parser = new();
            bool sucessful = parser.LoadTriggersFromFile(playableData, out var colorKeys, out var colorTriggers, out var setTriggers, out var offsetTriggers);
            if (!sucessful || (colorTriggers.Count == 0 && setTriggers.Count == 0))
            {
                return false;
            }

            LyricTriggers.LoadTriggers(colorTriggers, colorKeys, setTriggers, offsetTriggers);

            return true;
        }

        private const string LYRIC_TRIGGER_TEMPLATE = """
        ///  ------------------------------------
        #   COMMANDS:
        #
        #   LUT [entryName] #[color]
        #       - Creates a lookup table entry named [entryName] for color #[color]
        #       - It is recommended to use colors that you are not going to use but will still look good for non-mod users, like reserving #01FFFF, #02FFFF etc to look up table keys
        #       - Note that the lyric editor will automatically replace any instance of [entryName] to the respective #[color]
        #       Example:
        #           LUT color1 #01FFFF                                             // Binds color1 to #01FFFF
        #
        #   COLOR [LUTentry] [time] #[StartValue] <#[EndValue] [duration]>
        #       - Replaces all text's color with color tag corresponding to the LUT entry's color to the new color #[StartValue] and transitions to #[EndValue] over [duration]
        #       - Make sure that the LUT command is called before using COLOR for that entry
        #       Example:
        #           LUT color1 #01FFFF
        #           COLOR color1 10.0 #FF0000 #00FF00 1.2                          // Sets color1 to red (#FF0000) at time 10.0 then it turns green (#00FF00) over 1.2 seconds
        #
        #   SET [variable] [time] [startValue] <[endValue] [duration]>
        #       - Smoothly curves [variable] at [time] from [startValue] to [endValue] over [duration]
        #       - Variable names: FADEIN, FADEOUT, UNSPOKENALPHA, SLANT, TEXTBOXSIZE
        #       Example:
        #           SET FADEIN 10.2 1.0 0 5                                        // Sets FADEIN to 1.0 at time 10.2 then it smoothly falls back to 0 over 5 seconds
        #
        #   OFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration]> <[easing]>
        #       - Offsets the position of all text with the given [LUTindex] from [startOffset] to [endOffset] over duration
        #       - Give the offsets in the form of x,y,z or (x,y,z)
        #       - Optional [easing] parameter can be set to any easing found in https://easings.net/
        #       Example:
        #           OFFSET color1 30.5 (0,0,0) (0,4,-10) 2 ElasticInOut            // All text with same color tag as color1 gets moved from (0,0,0) to (0,4,-10) over 2 seconds while using the ElasticInOut animation curve
        #
        #   Note that parameters in <> are optional in this guide
        #
        ///  ------------------------------------




        # LUT ENTRIES
        LUT color1 #FFFFFF
        

        # COLOR TRIGGERS
        COLOR color1 10.0 #FF0000 #00FF00 2.0
        COLOR color1 12.0 #00FF00 #0000FF 2.0
        COLOR color1 14.0 #0000FF #FF0000 2.0


        # OTHER STUFF


        ///  ------------------------------------
        #
        #   MACROS:
        #
        #   REPEAT [numRepeats] interval [timeInterva]
        #       (commands go here)
        #   ENDREPEAT
        #
        #   FUNCTION [functionName]
        #       (commands go here)
        #   END
        #
        #   CALL [functionName] [time]
        #       - All commands in the function will have their time value increased by [time]
        #       - Note that infinitly recursive function calls will be ignored
        #
        ///  ------------------------------------

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

            //Debug.Log($"<{file.FileNameNoExtension}> Loaded {shaderData.Count} shader params");
            //Debug.Log($"config: \n{lyricConfig}");

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
        static Dictionary<TMP_FontAsset, Material> matMap = [];
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
        private Dictionary<Color32, Color32> lutColor  = [];
        private Dictionary<Color32, Vector3> lutOffset = [];

        public List<ShaderParameter> parameters = [];

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
            parameters = [];
            foreach (var kvp in p)
            {
                AddParameter(kvp.Key, kvp.Value);
            }
        }

        public Dictionary<string, object> GetParameterDictionary()
        {
            Dictionary<string, object> dict = [];
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
