using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LyricPlus
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

        public static Vector5 WithV(this Vector5 vec, float v)
        {
            return new Vector5(vec.x, vec.y, vec.z, vec.w, v);
        }

        public static Vector4 WithW(this Vector4 vec, float w)
        {
            return new Vector4(vec.x, vec.y, vec.z, w);
        }

        public static Vector4 ToSerialiableVector4(this Vector4 vec)
        {
            return new Vector4(vec);
        }

        public static UnityEngine.Color LerpHSV(UnityEngine.Color a, UnityEngine.Color b, float t)
        {
            UnityEngine.Color.RGBToHSV(a, out float h1, out float s1, out float v1);
            UnityEngine.Color.RGBToHSV(b, out float h2, out float s2, out float v2);

            float h = Mathf.LerpAngle(h1 * 360f, h2 * 360f, t) / 360f;

            h = Mathf.Repeat(h, 1f);

            float s = Mathf.Lerp(s1, s2, t);
            float v = Mathf.Lerp(v1, v2, t);
            float aAlpha = Mathf.Lerp(a.a, b.a, t);

            UnityEngine.Color result = UnityEngine.Color.HSVToRGB(h, s, v);
            result.a = aAlpha;

            return result;
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

        public static implicit operator Vector4(Vector5 v)
        {
            return new Vector4(v.x, v.y, v.z, v.w);
        }

        public static implicit operator Vector3(Vector5 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector5 operator +(Vector5 v1, Vector5 v2) 
        { 
            return new(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z, v1.w + v2.w, v1.v + v2.v);
        }

        public override string ToString()
        {
            return $"({x},{y},{z},{w},{v})";
        }
    }

    public struct Vector4
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4()
        {

        }

        public Vector4(Vector4 vec)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
            w = vec.w;
        }

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t)
        {
            float x = Mathf.LerpUnclamped(a.x, b.x, t);
            float y = Mathf.LerpUnclamped(a.y, b.y, t);
            float z = Mathf.LerpUnclamped(a.z, b.z, t);
            float w = Mathf.LerpUnclamped(a.w, b.w, t);
            return new Vector4(x, y, z, w);
        }

        public Vector4 Convert()
        {
            return new Vector4(x, y, z, w);
        }

        public static Vector4 operator +(Vector4 left, Vector4 right) 
        {
            return new Vector4(left.x + right.x, left.y + right.y, left.z + right.z, left.w + right.w);
        }

        public static implicit operator Vector3(Vector4 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static implicit operator Vector4(Vector3 v)
        {
            return new Vector4(v.x, v.y, v.z, 0);
        }
    }
}
