using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpinCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ColoredLyrics
{

    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(SpinCorePlugin.Guid, SpinCorePlugin.Version)]
    public class ModBase : BaseUnityPlugin
    {
        const string modGUID = "ColoredLyrics";
        private const string modName = "ColoredLyrics";
        private const string modVersion = "1.0.0";

        private readonly Harmony harmony = new(modGUID);
        internal static Shader textShader;


        void Awake()
        {
            Debug.Init(modGUID);

            InitShaders();
            ConfigManager.InitConfig();

            harmony.PatchAll(typeof(Patches));

            Debug.Log("ColoredLyrics Loaded!");
        }


        void Update()
        {
            ConfigManager.quickModGroup?.Transform.SetAsLastSibling();
        }

        public static void InitShaders()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ColoredLyrics.Shaders.textshader");
            AssetBundle bundle = AssetBundle.LoadFromStream(stream);
            if (bundle == null)
            {
                Debug.LogError("BUNDLE NOT FOUND!!!");
                return;
            }

            textShader = bundle.LoadAsset<Shader>("TextShader");
            if (textShader == null)
            {
                Debug.LogError("SHADER NOT FOUND!!!");
                return;
            }

            Debug.Log("Shaders initialized");
        }

        static Dictionary<TMP_FontAsset, Material> defaultMatMap = new();
        public static Material? GetTextMaterial(TMP_FontAsset font)
        {
            // Prefer chart specific mat
            Material? chartMat = TrackLyricDataManager.GetChartLyricMaterial(font);
            if (chartMat != null)
            {
                return chartMat;
            }

            if (ConfigManager.config.embeddedOnly) return null;
            if (defaultMatMap.TryGetValue(font, out var mat)) return mat;

            mat = new Material(textShader);
            mat.CopyPropertiesFromMaterial(font.material);
            defaultMatMap[font] = mat;

            ApplyDefaultShaderParameters();

            return mat;
        }

        public static void ApplyDefaultShaderParameters()
        {
            Util.ApplyShaderParameter(defaultMatMap.Values.ToList(), ConfigManager.config.shaderParams);
        }

        public static void ApplyDefaultShaderParameters(string name, object value, int flag = 0)
        {
            Util.ApplyShaderParameter(defaultMatMap.Values.ToList(), name, value);
        }
    }

    public class Debug
    {
        public static ManualLogSource? debug;

        public static void Init(string id)
        {
            debug = BepInEx.Logging.Logger.CreateLogSource(id);
        }

        public static void Log(object message)
        {
            debug?.LogInfo(message);
        }

        public static void LogError(object message)
        {
            debug?.LogError(message);
        }
    }

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

            foreach(var kvp in parameters)
            {
                ApplyShaderParameter(materials, kvp.Key, kvp.Value);
            }
        }
    }
}
