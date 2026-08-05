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
        static Dictionary<string, object> shaderData = new();

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
            Dictionary<string, object>? shaderParams = TryGetLyricDataFromTrack(file, diffStr);
            if (shaderParams != null)
            {
                Debug.Log($"<{file.FileNameNoExtension}> Loaded {shaderData.Count} shader params");
                shaderData = shaderParams;
            }
            else
            {
                // Clear materials
                shaderData.Clear();
                matMap.Clear();
            }
        }

        static Dictionary<string, object>? TryGetLyricDataFromTrack(IMultiAssetSaveFile file, string diff)
        {
            if (CustomChartHelper.TryGetCustomData(file, "ColoredLyrics_" + diff, out LyricShaderEmbedData shaderDataDiff))
            {
                Debug.Log($"<{file.FileNameNoExtension}> Getting shader data for difficulty {diff}");
                return shaderDataDiff.ToDictionary();
            }

            if (CustomChartHelper.TryGetCustomData(file, "ColoredLyrics", out LyricShaderEmbedData shaderDataGen))
            {
                Debug.Log($"<{file.FileNameNoExtension}> Getting shader data");
                return shaderDataGen.ToDictionary();
            }

            return null;
        }

        internal static void SetShaderParametersForTrack(IMultiAssetSaveFile? file, LyricShaderEmbedData data)
        {
            if (file == null)
            {
                Debug.Log("NO FILE");
                return;
            }
            if (data.parameters == null)
            {
                Debug.Log("NO DATA");
                return;
            }

            //Debug.Log($"<{file.FileNameNoExtension}> Trying to set shader params");
            CustomChartHelper.SetCustomData(file, "ColoredLyrics", data, save: true);
            shaderData = data.ToDictionary();

            Util.ApplyShaderParameter(matMap.Values.ToList(), shaderData);
        }

        static Dictionary<TMP_FontAsset, Material> matMap = new();
        public static Material? GetChartLyricMaterial(TMP_FontAsset font)
        {
            if (shaderData.Count == 0)
            {
                Debug.Log("Chart has no shader params");
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
                Debug.Log($"{parameters[i].key} {parameters[i].type} {val} {val.GetType()}");
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
    public struct Color(float r, float g, float b, float a = 1f)
    {
        public float r = r;
        public float g = g;
        public float b = b;
        public float a = a;

        public override string ToString()
        {
            return $"({r},{g},{b},{a})";
        }
    }
}
