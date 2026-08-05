using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using SpinCore.Translation;
using SpinCore.UI;
using UnityEngine;


namespace ColoredLyrics
{
    internal class ConfigManager
    {
        private static ConfigFile _config = new ConfigFile(Path.Combine(Paths.ConfigPath, "ColoredLyrics.cfg"), true);

        private static ConfigEntry<bool> _enableColoredLyrics;
        // Face
        private static ConfigEntry<UnityEngine.Color> _defaultFaceColor;
        private static ConfigEntry<float> _defaultFaceSoftness;
        // Outline
        private static ConfigEntry<float> _defaultOutlineWidth;
        private static ConfigEntry<UnityEngine.Color> _defaultOutlineColor;
        // Underlay
        private static ConfigEntry<UnityEngine.Color> _defaultUnderlayColor;


        Dictionary<string, object> defaultShaderParams = new();

        public static CustomGroup quickModGroup;

        public static ColoredLyricsConfig config;

        public static void InitConfig()
        {
            BindConfigs();
            CreateSettingsUI();
            CreateQuickModSettings();
        }

        private static void BindConfigs()
        {
            config = new();

            _enableColoredLyrics = _config.Bind("ColoredLyrics",
                "Enable",
                defaultValue: true,
                "Global toggle for lyric effects"
            );
            config.enableColoredLyrics = _enableColoredLyrics.Value;

            // Face
            _defaultFaceColor = _config.Bind("ColoredLyrics",
                "FaceColor",
                defaultValue: new UnityEngine.Color(1, 1, 1, 1),
                "Color of text surface"
            );
            config.SetColor("_FaceColor", _defaultFaceColor.Value.Convert());

            _defaultFaceSoftness = _config.Bind("ColoredLyrics",
                "FaceSoftness",
                defaultValue: 0f,
                "Face softness between 0 and 1"
            );
            config.SetFloat("_FaceSoftness", _defaultFaceSoftness.Value);

            // Outline
            _defaultOutlineWidth = _config.Bind("ColoredLyrics",
                "OutlineWidth",
                defaultValue: 0.1f,
                "Width for outlines between 0 and 1"
            );
            config.SetFloat("_OutlineWidth", _defaultOutlineWidth.Value);

            _defaultOutlineColor = _config.Bind("ColoredLyrics",
                "OutlineColor",
                defaultValue: new UnityEngine.Color(0, 0, 0, 1),
                "Color of lyric outline"
            );
            config.SetColor("_OutlineColor", _defaultOutlineColor.Value.Convert());

            // Underlay
            _defaultUnderlayColor = _config.Bind("ColoredLyrics",
                "UnderlayColor",
                defaultValue: new UnityEngine.Color(0, 0, 0, 0),
                "Color of lyric underlay"
            );
            config.SetColor("_UnderlayColor", _defaultUnderlayColor.Value.Convert());
        }

        private static void CreateSettingsUI()
        {
            CustomPage page = UIHelper.CreateCustomPage("Colored Lyrics");
            page.OnPageLoad += pageParent =>
            {
                var group = UIHelper.CreateGroup(pageParent, "General Settings");
                UIHelper.CreateSectionHeader(
                    group.Transform,
                    "General Header",
                    "ColoredLyrics_ModSettings_Header",
                    false
                );

                UIHelper.CreateLargeToggle(
                    group.Transform,
                    "EnableColoredLyrics",
                    "ColoredLyrics_ModSettings_Enable",
                    config.enableColoredLyrics,
                    v =>
                    {
                        config.enableColoredLyrics = v;
                    }
                );

                // Face Color
                var faceColorGroup = UIHelper.CreateGroup(group, "FaceColorGroup");
                faceColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    faceColorGroup.Transform,
                    "DefaultFaceColorLabel",
                    "ColoredLyrics_ModSettings_FaceColor"
                );
                CustomInputField faceColorInput = UIHelper.CreateInputField(
                    faceColorGroup.Transform,
                    "DefaultFaceColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        config.SetColor("_FaceColor", color.Convert());
                        _defaultFaceColor.Value = config.GetColor("_FaceColor").ToUnityColor();
                    }
                );
                faceColorInput.CharacterLimit = 8;
                faceColorInput.InputField.text = ColorUtility.ToHtmlStringRGB(config.GetColor("_FaceColor").ToUnityColor());

                // Outline
                UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "DefaultOutlineWidth",
                    "ColoredLyrics_ModSettings_OutlineWidth",
                    (int)(config.GetFloat("_OutlineWidth") * 100),
                    v =>
                    {
                        config.SetFloat("_OutlineWidth", v / 100f);
                        _defaultOutlineWidth.Value = config.GetFloat("_OutlineWidth");
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );

                var outlineColorGroup = UIHelper.CreateGroup(group, "OutlineColorGroup");
                outlineColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    outlineColorGroup.Transform,
                    "DefaultOutlineColorLabel",
                    "ColoredLyrics_ModSettings_OutlineColor"
                );
                CustomInputField outlineColorInput = UIHelper.CreateInputField(
                    outlineColorGroup.Transform,
                    "DefaultOutlineColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        config.SetColor("_OutlineColor", color.Convert(), flag: 2);
                        _defaultOutlineColor.Value = config.GetColor("_OutlineColor").ToUnityColor();
                    }
                );
                outlineColorInput.CharacterLimit = 8;
                outlineColorInput.InputField.text = ColorUtility.ToHtmlStringRGB(config.GetColor("_OutlineColor").ToUnityColor());

                // Underlay
                var underlayColorGroup = UIHelper.CreateGroup(group, "UnderlayColorGroup");
                underlayColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    underlayColorGroup.Transform,
                    "DefaultUnderlayColorLabel",
                    "ColoredLyrics_ModSettings_UnderlayColor"
                );
                CustomInputField underlayColorInput = UIHelper.CreateInputField(
                    underlayColorGroup.Transform,
                    "DefaultUnderlayColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        config.SetColor("_UnderlayColor", color.Convert(), flag: 1);
                        _defaultUnderlayColor.Value = config.GetColor("_UnderlayColor").ToUnityColor();
                    }
                );
                underlayColorInput.CharacterLimit = 8;
                underlayColorInput.InputField.text = ColorUtility.ToHtmlStringRGB(config.GetColor("_UnderlayColor").ToUnityColor());
            };

            var locale = Assembly.GetExecutingAssembly().GetManifestResourceStream("ColoredLyrics.locale.json");
            TranslationHelper.LoadTranslationsFromStream(locale);
            UIHelper.RegisterMenuInModSettingsRoot("ColoredLyrics_ModSettings_Name", page);
        }

        static readonly Dictionary<string, object> embedShaderParams = new();
        private static void CreateQuickModSettings()
        {
            UIHelper.RegisterGroupInQuickModSettings(panelTransform =>
            {
                // Header
                var group = UIHelper.CreateGroup(panelTransform, "ColoredLyricEmbed");
                UIHelper.CreateSectionHeader(
                    group.Transform,
                    "Header",
                    "ColoredLyrics_QuickSettings_Header",
                    false
                );

                // Face
                var faceColorGroup = UIHelper.CreateGroup(group.Transform, "FaceColorGroup");
                faceColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    faceColorGroup.Transform,
                    "EmbedFaceColorLabel",
                    "ColoredLyrics_ModSettings_FaceColor"
                );
                CustomInputField faceColorInput = UIHelper.CreateInputField(
                    faceColorGroup.Transform,
                    "EmbedFaceColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedShaderParams["_FaceColor"] = color.Convert();
                    }
                );
                faceColorInput.CharacterLimit = 8;
                faceColorInput.InputField.text = "FFFFFFFF";  // Default to white

                // Outline
                CustomMultiChoice outlineWidth = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedOutlineWidth",
                    "ColoredLyrics_ModSettings_OutlineWidth",
                    0,
                    v =>
                    {
                        embedShaderParams["_OutlineWidth"] = v / 100f;
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                outlineWidth.SetCurrentValue(5);

                var outlineColorGroup = UIHelper.CreateGroup(group.Transform, "OutlineColorGroup");
                outlineColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    outlineColorGroup.Transform,
                    "EmbedOutlineColorLabel",
                    "ColoredLyrics_ModSettings_OutlineColor"
                );
                CustomInputField outlineColorInput = UIHelper.CreateInputField(
                    outlineColorGroup.Transform,
                    "EmbedOutlineColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedShaderParams["_OutlineColor"] = color.Convert();
                    }
                );
                outlineColorInput.CharacterLimit = 8;
                outlineColorInput.InputField.text = "000000FF"; // Default to black

                // Underlay
                var underlayColorGroup = UIHelper.CreateGroup(group, "UnderlayColorGroup");
                underlayColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    underlayColorGroup.Transform,
                    "EmbedUnderlayColorLabel",
                    "ColoredLyrics_ModSettings_UnderlayColor"
                );
                CustomInputField underlayColorInput = UIHelper.CreateInputField(
                    underlayColorGroup.Transform,
                    "EmbedUnderlayColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedShaderParams["_UnderlayColor"] = color.Convert();
                    }
                );
                underlayColorInput.CharacterLimit = 8;
                underlayColorInput.InputField.text = "000000FF"; // Default to black

                // Apply
                UIHelper.CreateButton(
                    group.Transform,
                    "Apply",
                    "ColoredLyrics_ModSettings_Apply",
                    () =>
                    {
                        TrackLyricDataManager.SetShaderParametersForTrack(TrackLyricDataManager.currentFile, new LyricShaderEmbedData(embedShaderParams));
                    }
                );

                quickModGroup = group;
            });
        }

        public static Dictionary<string, object> GetDefaultShaderParams()
        {
            return config.shaderParams;
        }
    }

    public struct ColoredLyricsConfig
    {
        public bool enableColoredLyrics; // This only takes effect on chart restart due to the nature of the patch

        public Dictionary<string, object> shaderParams = new();

        public ColoredLyricsConfig()
        {
            shaderParams = [];
        }

        public float GetFloat(string name)
        {
            return shaderParams[name] is float f ? f : 0f;
        }

        public Color GetColor(string name) 
        {
            return shaderParams[name] is Color c ? c : new Color(1, 1, 1, 1);
        }

        public void SetFloat(string name, float value)
        {
            if (value is float f)
            {
                shaderParams[name] = value;
                ModBase.ApplyDefaultShaderParameters(name, f);
            }
        }

        public void SetColor(string name, Color value, int flag = 0)
        {
            if (value is Color c)
            {
                shaderParams[name] = value;
                ModBase.ApplyDefaultShaderParameters(name, c, flag);
            }
        }
    }
}
