using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ColoredLyrics
{
    public static class Util
    {
        public static bool Equal(this Color32 col, Color32 other, bool ignoreAlpha = false)
        {
            return col.r == other.r && col.g == other.g && col.b == other.b && (col.a == other.a || ignoreAlpha);
        }

        public static UnityEngine.Color ToUnityColor(this Color col)
        {
            return new UnityEngine.Color(col.r, col.g, col.b, col.a);
        }

        public static Color Convert(this UnityEngine.Color col)
        {
            return new Color(col.r, col.g, col.b, col.a);
        }

        public static Color32 ToColor32(this Color col)
        {
            return new Color32((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), (byte)(col.a * 255));
        }

        public static Color32 WithA(this Color32 col, byte a)
        {
            return new Color32(col.r, col.g, col.b, a);
        }

        public static UnityEngine.Color LerpHSL(UnityEngine.Color a, UnityEngine.Color b, float t)
        {
            a.ToHSLA();
            b.ToHSLA();
            Vector4 h = Vector4.Lerp(a, b, t);
            return UnityEngine.Color.HSVToRGB(h.x, h.y, h.z).WithA(h.w);
        }

        public static byte Remap(this byte value, byte fromMin, byte fromMax, byte toMin, byte toMax)
        {
            return (byte)(toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin));
        }

        /// Flags for shader:
        /// UNDERLAY = 1
        /// GLOW = 2

        static readonly Dictionary<string, string> SHADER_KEYWORDS = new()
        {
            ["_GlowColor"] = "GLOW_ON",
            ["_UnderlayColor"] = "UNDERLAY_ON"
        };

        public static void ApplyShaderParameter(this Material mat, string name, object value)
        {
            if (value is float f)
            {
                mat.SetFloat(name, f);
            }
            else if (value is Color c)
            {
                UnityEngine.Color col = c.ToUnityColor();
                Debug.Log($"Setting param {name} {col}");
                mat.SetColor(name, col);
                //if (SHADER_KEYWORDS.ContainsKey(name))
                //{
                //    string keyword = SHADER_KEYWORDS[name];
                //    Debug.Log(mat.IsKeywordEnabled(keyword));
                //    mat.EnableKeyword(keyword);
                //    Debug.Log($"Keyword enabled: {keyword}");
                //    Debug.Log(mat.enabledKeywords);
                //}
            }
            else
            {
                Debug.LogError($"Unsupported parameter type {value.GetType()}");
            }
        }

        public static void ApplyShaderParameter(this Material mat, Dictionary<string, object> parameters)
        {
            if (parameters.Count == 0)
            {
                Debug.Log("No parameters to apply");
            }

            foreach (var kvp in parameters)
            {
                ApplyShaderParameter(mat, kvp.Key, kvp.Value);
            }
        }

        public static void ApplyShaderParameter(List<Material> materials, string name, object value)
        {
            if (materials.Count == 0)
            {
                return;
            }

            foreach (var mat in materials)
            {
                Debug.Log($"Applying {name} {value} to {materials.Count} mats");
                mat.ApplyShaderParameter(name, value);
            }
        }

        public static void ApplyShaderParameter(List<Material> materials, Dictionary<string, object> parameters)
        {
            if (parameters.Count == 0)
            {
                Debug.Log("No parameters to apply");
                return;
            }

            foreach (var kvp in parameters)
            {
                ApplyShaderParameter(materials, kvp.Key, kvp.Value);
            }
        }

        public static IMultiAssetSaveFile? LoadSaveFromPlayData(PlayableTrackData playableTrackData)
        {
            (string? directory, string? fileName) = GetDirectoryFromPlayData(playableTrackData);
            if (directory == null) return null;

            var files = new List<IMultiAssetSaveFile>();
            playableTrackData.GetCustomFiles(files);

            IMultiAssetSaveFile file = files.First();
            return file;
        }

        public static (string? directory, string? fileName) GetDirectoryFromPlayData(PlayableTrackData playableTrackData)
        {
            if (playableTrackData.TrackDataList.Count == 0)
                return (null, null);

            // Get track data
            TrackData track = playableTrackData.TrackDataList[0];
            string path = track.CustomFile?.FilePath ?? "";
            if (string.IsNullOrEmpty(path))
                return (null, null);

            // Get file / directory
            string? directory = Directory.GetParent(path)?.FullName ?? null;
            string? filename = Path.GetFileNameWithoutExtension(path);

            return (directory, filename);
        }
    }

    // Witness
    public struct Vector5
    {
        public float x;
        public float y;
        public float z;
        public float w;
        public float v;

        public static Vector5 zero = new(0, 0, 0, 0, 0);

        public Vector5()
        {

        }

        public Vector5(float x, float y, float z, float w, float v)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
            this.v = v;
        }

        public Vector5(Vector4 vec, float v)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
            w = vec.w;
            this.v = v;
        }

        public Vector5(Vector3 vec, float w, float v)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
            this.w = w;
            this.v = v;
        }
    }
}
