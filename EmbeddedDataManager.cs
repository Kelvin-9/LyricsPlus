using SpinCore.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;

namespace LyricPlus
{
    internal class EmbeddedDataManager
    {
        public static PlayableTrackData? currentTrack;
        public static IMultiAssetSaveFile? currentFile;
        internal static Dictionary<string, object> shaderData = [];
        internal static LyricConfig? lyricConfig = null;
        internal static bool HasEmbeddedData => lyricConfig != null;

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

            LyricTriggers.LoadTriggers(t);
            return true;
        }

        public static bool LoadTriggersFromLyrFile(PlayableTrackData playableData)
        {
            TriggerFileParser parser = new();
            bool sucessful = parser.LoadTriggersFromFile(playableData, out var colorKeys, out var colorTriggers, out var setTriggers, out var offsetTriggers, out var scaleTriggers, out var rotationTriggers);
            if (!sucessful || (colorTriggers.Count == 0 && setTriggers.Count == 0))
            {
                return false;
            }

            LyricTriggers.LoadTriggers(colorTriggers, colorKeys, setTriggers, offsetTriggers, scaleTriggers, rotationTriggers);

            return true;
        }

        private const string LYRIC_TRIGGER_TEMPLATE = """
        ///  ------------------------------------
        #   COMMANDS:
        #
        #   LUT "LUTentry" "#color"
        #       - Creates a lookup table entry named "LUTentry" for color [#color]
        #       - It is recommended to use colors that you are not going to use but will still look good for non-mod users, like reserving #01FFFF, #02FFFF etc to look up table keys
        #       - Note that the lyric editor will automatically replace any instance of "LUTentry" to the respective "#color"
        #       Example:
        #           LUT color1 #01FFFF                                             // Binds color1 to #01FFFF
        #
        #   COLOR "LUTentry" [time] "#startColor" <"#endColor" [duration]>
        #       - Replaces all text's color with color tag corresponding to the LUT entry's color to the new color "#startColor" and transitions to "#endColor" over [duration]
        #       - Make sure that the LUT command is called before using COLOR for that entry
        #       Example:
        #           LUT color1 #01FFFF
        #           COLOR color1 10.0 #FF0000 #00FF00 1.2                          // Sets color1 to red (#FF0000) at time 10.0 then it turns green (#00FF00) over 1.2 seconds
        #
        #   SET "variable" [time] [startValue] <[endValue] [duration]>
        #       - Smoothly curves "variable" at [time] from [startValue] to [endValue] over [duration]
        #       - Variable names: FADEIN, FADEOUT, UNSPOKENALPHA, SLANT, TEXTBOXSIZE
        #       Example:
        #           SET FADEIN 10.2 1.0 0 5                                        // Sets FADEIN to 1.0 at time 10.2 then it linearly falls back to 0 over 5 seconds
        #
        #   OFFSET "LUTentry" [time] (startOffset) <(endOffset) [duration]> <"easing">
        #       - Offsets the position of all text with the given "LUTentry" from [startOffset] to [endOffset] over duration
        #       - Give the offsets in the form of x,y,z or (x,y,z)
        #       - Optional "easing" parameter can be set to any easing found in https://easings.net/
        #       Example:
        #           OFFSET color1 30.5 (0,0,0) (0,4,-10) 2 InOutElastic            // All text with same color tag as color1 gets moved from (0,0,0) to (0,4,-10) over 2 seconds while using the ElasticInOut animation curve
        #
        #   RELATIVEOFFSET "LUTentry" [time] (offset) <[duration]> <"easing">
        #       - Increases the offset based on previous OFFSET/RELATIVEOFFSET trigger on this "LUTentry"
        #       - For example, offsetting by (0,1,0) then doing RELATIVEOFFSET by (0,1,0) will make the text go to (0,2,0)
        #       Example:
        #           OFFSET color1 30.5 (0,0,0)
        #           RELATIVEOFFSET color1 31 (0,1,0)
        #
        #   ROTATE "LUTentry" [time] (axis) [degrees] [pivotIndex] <(endAxis) [endDegrees] [duration]> <"easing">
        #       - Rotates around (axis) and character index [pivotIndex] by [degrees], moving the axis towards (endAxis) and changing the degrees to [endDegrees] over [duration]
        #       - For example, "@<color=color1>helicopter" with trigger ROTATE color1 10 (0,0,1) 10 2 would pivot around index 2 (the 3rd character) of the phrase "helicopter" which would be the letter "l", rotated around the z axis by 10 degrees.
        #       Example: 
        #           ROTATE color1 10.2 (0,0,1) 0 0 (0,0,1) 20 2 InOutQuint
        #   
        #   RELATIVEROTATE "LUTentry" [time] (endAxis) [degreesIncrease] <[duration]> <"easing">
        #       - Rotates around the previous trigger's end axis and pivot, increasing the angle by [degreesIncrease] and moving axis towards (endAxis) over [duration]
        #       - Makes writing consecutive rotations easier and compatible with REPEAT loops
        #       Example:
        #           ROTATE color1 9 (0,0,1) 0                               // Set up axis and pivot
        #           RELATIVEROTATE color1 10.2 (0,0,1) 20 2 OutSine         // Rotate around previous axis (0,0,1) and pivot index 0 by 20 degrees
        #           RELATIVEROTATE color1 11.2 (0,0,1) 20 2 OutSine         // Do it again, it is now 40 degrees around z axis
        #
        #   SCALE [LUTindex] [time] (startScale) [pivotIndex] <(endScale) [duration]> <"easing">
        #       - Scales [LUTindex] around [pivotIndex] by (startScale)
        #       Example:
        #           SCALE color1 10.0 (1,1,1) 0 (1,2,1) 2.0 InOutQuint
        #
        #
        #   <> = optional, [] = number, "" = name, "#x" = color, () = vector
        #   All variables are separated by space so do NOT add spaces between vector components like (0, 0, 0). Write it like 0,0,0 or (0,0,0) instead
        #   Pivot index Refers to the index of the character that the effect is pivoted on. 
        #       In the phrase "@my lyrics so fine" if I want to rotate around the character "l" in lyrics, I would count (including spaces) the index of the character starting at 1 for 'm', so 'l' would be 4.
        #       To use the character itself as pivot, use index 0. 
        #       Negative numbers are the same as positive except the effect is multiplied by the character's index distance to the pivot. For example, "ROTATE color1 10 (0,0,1) 20 -1" for "@x<color=color1>Lyrics" would rotate 'L' around 'x' by 20 degrees, 'y' around 'x' by 40 degrees, etc.
        ///  ------------------------------------




        LUT color1 #01FFFF
        LUT color2 #02FFFF
        LUT color3 #03FFFF
        LUT color4 #04FFFF
        
        COLOR color1 10.0 #FF0000 #00FF00 2.0
        COLOR color1 12.0 #00FF00 #0000FF 2.0
        COLOR color1 14.0 #0000FF #FF0000 2.0




        ///  ------------------------------------
        #
        #   MACROS:
        #
        #   REPEAT [numRepeats] interval [timeInterval]
        #       (commands go here)
        #   ENDREPEAT
        #       - You can use RELATIVE triggers to repeatedly move/rotate text with this
        #
        #   FUNCTION [functionName]
        #       (commands go here)
        #   ENDFUNCTION
        #
        #   CALL [functionName] [time]
        #       - All commands in the function will have their time value increased by [time]
        #       - Note that infinitly recursive function calls will be ignored, i.e. if FUNCTION A has CALL B [time] and FUNCTION B has CALL A [time], the last CALL A will not execute
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
                    using (StreamWriter writer = new(path))
                    {
                        writer.Write(LYRIC_TRIGGER_TEMPLATE);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create path: {e.Message}");
                    return;
                }
            }

            // Open file
            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception e) 
            {
                Debug.LogError($"Failed to open {path}: {e.Message}");
                return;
            }
        }


        // UI SYNC
        public static void SyncAllQuickmodEmbedUI()
        {
            if (!HasEmbeddedData) 
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
                ConfigManager.syncUI.Reset();
                shaderData.Clear();
                matMap.Clear();
                return;
            }

            //Debug.Log($"<{file.FileNameNoExtension}> Loaded embedded lyric+ config");
            shaderData = lyricConfig.GetParameterDictionary();

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

        public static void ModifyScale(Color32 key, Vector4 scale)
        {
            if (lyricConfig == null)
            {
                Debug.LogError("Tried to modify LUT with trigger but lyricConfig is null!");
                return;
            }

            lyricConfig.SetLUTScale(key, scale);
        }

        public static void ModifyRotation(Color32 key, Vector5 rotation)
        {
            if (lyricConfig == null)
            {
                Debug.LogError("Tried to modify LUT with trigger but lyricConfig is null!");
                return;
            }

            lyricConfig.SetLUTRotation(key, rotation);
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
            if (!HasEmbeddedData)
            {
                return null;
            }

            if (matMap.TryGetValue(font, out var mat)) return mat;

            mat = new(Plugin.textShader);
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
        //private readonly Dictionary<Color32, Color32> lutColor    = [];
        //private readonly Dictionary<Color32, Vector3> lutOffset   = [];
        //private readonly Dictionary<Color32, Vector4> lutScale    = [];
        //private readonly Dictionary<Color32, Vector5> lutRotation = [];
        private readonly Dictionary<Color32, LUTInfo> lutInfos = [];

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
            lutInfos.GetOrCreateForKey(key, out var info);
            info.color = value;
        }

        public void SetLUTOffset(Color32 key, Vector3 offset)
        {
            lutInfos.GetOrCreateForKey(key, out var info);
            info.offset = offset;
        }

        public void SetLUTScale(Color32 key, Vector4 scale)
        {
            lutInfos.GetOrCreateForKey(key, out var info);
            info.scale = scale;
        }

        public void SetLUTRotation(Color32 key, Vector5 rotation)
        {
            lutInfos.GetOrCreateForKey(key, out var info);
            info.rotation = rotation;
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

        public LUTInfo GetLUTInfo(Color32 key)
        {
            if (!lutInfos.TryGetValue(key, out var value))
            {
                value = new(key.WithA(255));
                lutInfos[key] = value;
            }

            return value;
        }
    }

    public class LUTInfo
    {
        public Color32 color = new(255, 255, 255, 255);
        public Vector3 offset = new(0,0,0);
        public Vector4 scale = new(1,1,1,0);
        public Vector5 rotation = new(0,0,0,0,0);

        public LUTInfo() { }

        public LUTInfo(Color32 color) 
        {
            this.color = color;
        }
    }
}
