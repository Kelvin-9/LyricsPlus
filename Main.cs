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

namespace LyricsPlus
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(SpinCorePlugin.Guid, SpinCorePlugin.Version)]
    public class Plugin : BaseUnityPlugin
    {
        const string modGUID = "LyricsPlus";
        private const string modName = "LyricsPlus";
        private const string modVersion = "1.0.2";

        private readonly Harmony harmony = new(modGUID);
        internal static Shader textShader;
        internal static List<TMP_FontAsset> fallbackFonts = [];

        void Awake()
        {
            Debug.Init(modGUID);

            InitShaders();
            InitFallbackFonts();
            ConfigManager.InitConfig();

            harmony.PatchAll(typeof(Patches));

            Debug.Log("LyricsPlus Loaded!");
        }


        void Update()
        {
            ConfigManager.quickModGroup?.Transform.SetAsLastSibling();
        }

        public static void InitShaders()
        {
            Stream shaderStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LyricsPlus.Shaders.textshader");
            AssetBundle bundle = AssetBundle.LoadFromStream(shaderStream);
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

        private readonly static string[] fallbackFontNames = ["NotoSansSymbols", "NotoSansSymbols2", "NotoColorEmoji"];
        public static void InitFallbackFonts()
        {
            Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LyricsPlus.Fonts.fallbackFont");
            AssetBundle bundle = AssetBundle.LoadFromStream(fontStream);
            if (bundle == null)
            {
                Debug.LogError("BUNDLE NOT FOUND!!!");
                return;
            }

            for (int i = 0; i < fallbackFontNames.Length; i++)
            {
                var font = bundle.LoadAsset<TMP_FontAsset>(fallbackFontNames[i]);
                if (font == null)
                {
                    Debug.LogError($"Font {fallbackFontNames[i]} not found!");
                    continue;
                }

                fallbackFonts.Add(font);
                TMP_Settings.fallbackFontAssets.Add(font);
            }

            Debug.Log("Fallback font added");
        }

        static Dictionary<TMP_FontAsset, Material> defaultMatMap = [];
        internal static TMP_FontAsset mainLyricFont;
        public static Material? GetTextMaterial(TMP_FontAsset font)
        {
            mainLyricFont = font;

            // Prefer chart specific mat
            Material? chartMat = EmbeddedDataManager.GetChartLyricMaterial(font);
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

        public static void LogError(object message)
        {
            debug?.LogError(message);
        }

        public static void LogWarning(object message)
        {
            debug?.LogWarning(message);
        }

        public static void Log(object message)
        {
            debug?.LogInfo(message);
        }
    }
}
