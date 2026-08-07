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

        public static void Log(object message)
        {
            debug?.LogInfo(message);
        }
    }
}
