using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using SpinCore.Translation;
using SpinCore.UI;
using UnityEngine;


namespace LyricPlus
{
    internal class ConfigManager
    {
        private static ConfigFile _config = new ConfigFile(Path.Combine(Paths.ConfigPath, "LyricPlus.cfg"), true);

        private static ConfigEntry<bool> _enableLyricPlus;
        private static ConfigEntry<bool> _embeddedOnly;
        // Face
        private static ConfigEntry<UnityEngine.Color> _defaultFaceColor;
        private static ConfigEntry<float> _defaultFaceDilate;
        // Outline
        private static ConfigEntry<float> _defaultOutlineWidth;
        private static ConfigEntry<UnityEngine.Color> _defaultOutlineColor;
        // Default Color
        private static ConfigEntry<UnityEngine.Color> _defaultDefaultColor;
        // Fading
        private static ConfigEntry<float> _defaultFadeInRatio;
        private static ConfigEntry<float> _defaultFadeOutRatio;


        Dictionary<string, object> defaultShaderParams = [];

        public static CustomGroup? quickModGroup;
        public static CustomButton? openLyricFileButton;

        public static LyricPlusConfig config;

        public static void InitConfig()
        {
            BindConfigs();
            CreateSettingsUI();
            CreateQuickModSettings();
        }

        private static void BindConfigs()
        {
            config = new();

            // Toggles
            _enableLyricPlus = _config.Bind("LyricPlus",
                "Enable",
                defaultValue: true,
                "Global toggle for lyric effects"
            );
            config.enableLyricPlus = _enableLyricPlus.Value;

            _embeddedOnly = _config.Bind("LyricPlus",
                "EmbeddedOnly",
                defaultValue: true,
                "Toggles whether default values are used when chart does not have embedded lyric configs"
            );
            config.embeddedOnly = _embeddedOnly.Value;

            // Face
            _defaultFaceColor = _config.Bind("LyricPlus",
                "FaceColor",
                defaultValue: new UnityEngine.Color(1, 1, 1, 1),
                "Color of text surface"
            );
            config.SetColor("_FaceColor", _defaultFaceColor.Value.Convert());

            _defaultFaceDilate = _config.Bind("LyricPlus",
                "FaceDilate",
                defaultValue: 0f,
                "Face dilate between -1 and 1"
            );
            config.SetFloat("_FaceDilate", _defaultFaceDilate.Value);

            // Outline
            _defaultOutlineWidth = _config.Bind("LyricPlus",
                "OutlineWidth",
                defaultValue: 0.1f,
                "Width for outlines between 0 and 1"
            );
            config.SetFloat("_OutlineWidth", _defaultOutlineWidth.Value);

            _defaultOutlineColor = _config.Bind("LyricPlus",
                "OutlineColor",
                defaultValue: new UnityEngine.Color(0, 0, 0, 1),
                "Color of lyric outline"
            );
            config.SetColor("_OutlineColor", _defaultOutlineColor.Value.Convert());

            _defaultDefaultColor = _config.Bind("LyricPlus",
                "DefaultColor",
                defaultValue: new UnityEngine.Color(1, 1, 1, 1),
                "Default Lyric Color"
            );
            config.defaultColor = _defaultDefaultColor.Value.Convert();

            // Fade
            _defaultFadeInRatio = _config.Bind("LyricPlus",
                "FadeInRatio",
                defaultValue: 1f,
                "Lyric fade in"
            );
            config.fadeInRatio = _defaultFadeInRatio.Value;

            _defaultFadeOutRatio = _config.Bind("LyricPlus",
                "FadeOutRatio",
                defaultValue: 1f,
                "Lyric fade out"
            );
            config.fadeOutRatio = _defaultFadeOutRatio.Value;
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
                    "LyricPlus_ModSettings_Header",
                    false
                );

                // Global Toggles
                UIHelper.CreateLargeToggle(
                    group.Transform,
                    "EnableLyricPlus",
                    "LyricPlus_ModSettings_Enable",
                    config.enableLyricPlus,
                    v =>
                    {
                        config.enableLyricPlus = v;
                        _enableLyricPlus.Value = v;
                    }
                );

                CustomGroup defaultValuesGroup = UIHelper.CreateGroup(group, "DefaultLyricValuesGroup"); ;
                var label = UIHelper.CreateLabel(
                    group.Transform,
                    "EmbeddedOnlyTootip",
                    "LyricPlus_ModSettings_EmbeddedOnlyTooltip"
                );
                UIHelper.CreateLargeToggle(
                    group.Transform,
                    "EnableLyricPlus",
                    "LyricPlus_ModSettings_EmbeddedOnly",
                    config.embeddedOnly,
                    v =>
                    {
                        config.embeddedOnly = v;
                        _embeddedOnly.Value = v;
                        defaultValuesGroup.GameObject.SetActive(!config.embeddedOnly);
                        label.GameObject.SetActive(!config.embeddedOnly);
                    }
                );
                defaultValuesGroup.Transform.SetAsLastSibling();

                // Face
                var faceColorGroup = UIHelper.CreateGroup(defaultValuesGroup, "FaceColorGroup");
                faceColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    faceColorGroup.Transform,
                    "DefaultFaceColorLabel",
                    "LyricPlus_ModSettings_FaceColor"
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
                faceColorInput.InputField.text = ColorUtility.ToHtmlStringRGBA(config.GetColor("_FaceColor").ToUnityColor());

                UIHelper.CreateLargeMultiChoiceButton(
                    defaultValuesGroup.Transform,
                    "DefaultFaceDilate",
                    "LyricPlus_ModSettings_FaceDilate",
                    (int)(config.GetFloat("_FaceDilate") * 100),
                    v =>
                    {
                        config.SetFloat("_FaceDilate", v / 100f);
                        _defaultFaceDilate.Value = config.GetFloat("_FaceDilate");
                    },
                    () => new IntRange(-100, 101),
                    v => v.ToString()
                );

                // Outline
                UIHelper.CreateLargeMultiChoiceButton(
                    defaultValuesGroup.Transform,
                    "DefaultOutlineWidth",
                    "LyricPlus_ModSettings_OutlineWidth",
                    (int)(config.GetFloat("_OutlineWidth") * 100),
                    v =>
                    {
                        config.SetFloat("_OutlineWidth", v / 100f);
                        _defaultOutlineWidth.Value = config.GetFloat("_OutlineWidth");
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );

                var outlineColorGroup = UIHelper.CreateGroup(defaultValuesGroup, "OutlineColorGroup");
                outlineColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    outlineColorGroup.Transform,
                    "DefaultOutlineColorLabel",
                    "LyricPlus_ModSettings_OutlineColor"
                );
                CustomInputField outlineColorInput = UIHelper.CreateInputField(
                    outlineColorGroup.Transform,
                    "DefaultOutlineColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        config.SetColor("_OutlineColor", color.Convert());
                        _defaultOutlineColor.Value = config.GetColor("_OutlineColor").ToUnityColor();
                    }
                );
                outlineColorInput.CharacterLimit = 8;
                outlineColorInput.InputField.text = ColorUtility.ToHtmlStringRGBA(config.GetColor("_OutlineColor").ToUnityColor());

                var defaultColorGroup = UIHelper.CreateGroup(defaultValuesGroup, "DefaultColorGroup");
                defaultColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    defaultColorGroup.Transform,
                    "DefaultDefaultColorLabel", // default default heh
                    "LyricPlus_ModSettings_DefaultColor"
                );
                CustomInputField defaultColorInput = UIHelper.CreateInputField(
                    defaultColorGroup.Transform,
                    "DefaultDefaultColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        config.defaultColor = color.Convert();
                        _defaultDefaultColor.Value = color;
                    }
                );
                defaultColorInput.CharacterLimit = 8;
                defaultColorInput.InputField.text = ColorUtility.ToHtmlStringRGBA(config.defaultColor.ToUnityColor());


                UIHelper.CreateLargeMultiChoiceButton(
                    defaultValuesGroup.Transform,
                    "EmbedFadeInRatio",
                    "LyricPlus_ModSettings_FadeInRatio",
                    (int)(config.fadeInRatio * 100),
                    v =>
                    {
                        float value = Mathf.Clamp01(v / 100f);
                        config.fadeInRatio = value;  // 100 = normal, 0 = instant
                        _defaultFadeOutRatio.Value = value;
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );

                UIHelper.CreateLargeMultiChoiceButton(
                    defaultValuesGroup.Transform,
                    "EmbedFadeInRatio",
                    "LyricPlus_ModSettings_FadeOutRatio",
                    (int)(config.fadeOutRatio * 100),
                    v =>
                    {
                        float value = Mathf.Clamp01(v / 100f);
                        config.fadeOutRatio = value;  // 100 = normal, 0 = instant
                        _defaultFadeOutRatio.Value = value;
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );



                defaultValuesGroup.GameObject.SetActive(!config.embeddedOnly);
                label.GameObject.SetActive(!config.embeddedOnly);
                label.Transform.SetAsLastSibling();
            };

            var locale = Assembly.GetExecutingAssembly().GetManifestResourceStream("LyricPlus.locale.json");

            TranslationHelper.LoadTranslationsFromStream(locale);
            UIHelper.RegisterMenuInModSettingsRoot("LyricPlus_ModSettings_Name", page);
        }

        static Dictionary<string, object> embedShaderParams = [];
        static LyricConfig embedConfig = new();
        static UnityEngine.Color DEFAULT_EMBED_FACECOLOR = new(1, 1, 1, 1);
        static UnityEngine.Color DEFAULT_EMBED_OUTLINECOLOR = new(0, 0, 0, 1);
        static UnityEngine.Color DEFAULT_EMBED_COLOR = new(1, 1, 1, 1);
        internal static CustomTextComponent? embedTargetLabel;

        internal static SyncedUIStore syncUI = new();

        private static void CreateQuickModSettings()
        {
            UIHelper.RegisterGroupInQuickModSettings(panelTransform =>
            {
                // HEADER //
                var group = UIHelper.CreateGroup(panelTransform, "LyricPlusEmbed");
                UIHelper.CreateSectionHeader(
                    group.Transform,
                    "Header",
                    "LyricPlus_QuickSettings_Header",
                    false
                );
                embedTargetLabel = UIHelper.CreateLabel(
                    group.Transform,
                    "EmbedTargetLabel",
                    "____"
                );
                embedTargetLabel.ExtraText = EmbeddedDataManager.currentTrack?.Name;

                // FACE //
                var faceColorGroup = UIHelper.CreateGroup(group.Transform, "FaceColorGroup");
                faceColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    faceColorGroup.Transform,
                    "EmbedFaceColorLabel",
                    "LyricPlus_ModSettings_FaceColor"
                );
                var embedFaceColorInput = UIHelper.CreateInputField(
                    faceColorGroup.Transform,
                    "EmbedFaceColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedShaderParams["_FaceColor"] = color.Convert();
                    }
                );
                embedFaceColorInput.CharacterLimit = 8;
                UnityEngine.Color embedFaceCol = DEFAULT_EMBED_FACECOLOR;
                string startEmbedFaceCol = ColorUtility.ToHtmlStringRGBA(embedFaceCol);
                embedConfig.defaultColor = embedFaceCol.Convert();
                embedFaceColorInput.InputField.text = startEmbedFaceCol;
                syncUI.AddUI("_FaceColor", embedFaceColorInput, startEmbedFaceCol);

                var embedFaceDilateInput = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedFaceDilate",
                    "LyricPlus_ModSettings_FaceDilate",
                    (int)(config.GetFloat("_FaceDilate") * 100),
                    v =>
                    {
                        embedShaderParams["_FaceDilate"] = v / 100f;
                    },
                    () => new IntRange(-100, 101),
                    v => v.ToString()
                );
                int embedFaceDilate = 0;
                embedShaderParams["_FaceDilate"] = embedFaceDilate / 100f;
                embedFaceDilateInput.SetCurrentValue(embedFaceDilate);
                syncUI.AddUI("_FaceDilate", embedFaceDilateInput, 0);


                // OUTLINE //
                var embedOutlineWidthInput = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedOutlineWidth",
                    "LyricPlus_ModSettings_OutlineWidth",
                    0,
                    v =>
                    {
                        embedShaderParams["_OutlineWidth"] = v / 100f;
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int embedOutlineWidth = 50;
                embedShaderParams["_OutlineWidth"] = embedOutlineWidth / 100f;
                embedOutlineWidthInput.SetCurrentValue(embedOutlineWidth);
                syncUI.AddUI("_OutlineWidth", embedOutlineWidthInput, 0);


                var outlineColorGroup = UIHelper.CreateGroup(group.Transform, "OutlineColorGroup");
                outlineColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    outlineColorGroup.Transform,
                    "EmbedOutlineColorLabel",
                    "LyricPlus_ModSettings_OutlineColor"
                );
                
                var embedOutlineColor = UIHelper.CreateInputField(
                    outlineColorGroup.Transform,
                    "EmbedOutlineColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedShaderParams["_OutlineColor"] = color.Convert();
                    }
                );
                embedOutlineColor.CharacterLimit = 8;
                UnityEngine.Color embedOutlineCol = DEFAULT_EMBED_COLOR;
                string startEmbedOutlineCol = ColorUtility.ToHtmlStringRGBA(embedOutlineCol);
                embedConfig.defaultColor = embedOutlineCol.Convert();
                embedOutlineColor.InputField.text = startEmbedOutlineCol;
                syncUI.AddUI("_OutlineColor", embedOutlineColor, ColorUtility.ToHtmlStringRGBA(DEFAULT_EMBED_OUTLINECOLOR));


                // OTHER //
                var defaultColorGroup = UIHelper.CreateGroup(group.Transform, "DefaultColorGroup");
                defaultColorGroup.LayoutDirection = Axis.Horizontal;
                UIHelper.CreateLabel(
                    defaultColorGroup.Transform,
                    "EmbedDefaultColorLabel",
                    "LyricPlus_ModSettings_DefaultColor"
                );
                var embedDefaultColor = UIHelper.CreateInputField(
                    defaultColorGroup.Transform,
                    "EmbedDefaultColor",
                    (_, str) =>
                    {
                        if (!ColorUtility.TryParseHtmlString("#" + str, out UnityEngine.Color color))
                            return;

                        embedConfig.defaultColor = color.Convert();
                    }
                );
                embedDefaultColor.CharacterLimit = 8;
                UnityEngine.Color embedDefaultCol = DEFAULT_EMBED_COLOR;
                string startEmbedDefaultCol = ColorUtility.ToHtmlStringRGBA(embedDefaultCol);
                embedConfig.defaultColor = embedDefaultCol.Convert();
                embedDefaultColor.InputField.text = startEmbedDefaultCol;
                syncUI.AddUI("embedDefaultColor", embedDefaultColor, ColorUtility.ToHtmlStringRGBA(DEFAULT_EMBED_COLOR));

                var embedFadeIn = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedFadeInRatio",
                    "LyricPlus_ModSettings_FadeInRatio",
                    100,
                    v =>
                    {
                        embedConfig.fadeInRatio = Mathf.Clamp01(v / 100f);  // 100 = normal, 0 = instant
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int startEmbedFadeIn = 100;
                embedFadeIn.SetCurrentValue(startEmbedFadeIn);
                syncUI.AddUI("embedFadeIn", embedFadeIn, 100);

                var embedFadeOut = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedFadeOutRatio",
                    "LyricPlus_ModSettings_FadeOutRatio",
                    100,
                    v =>
                    {
                        embedConfig.fadeOutRatio = Mathf.Clamp01(v / 100f);  // 100 = normal, 0 = instant
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int startEmbedFadeOut = 100;
                embedFadeOut.SetCurrentValue(startEmbedFadeOut);
                syncUI.AddUI("embedFadeOut", embedFadeOut, 100);

                // Phrase
                var embedUnspokenWordAlpha = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedUnspokenWordAlpha",
                    "LyricPlus_ModSettings_UnspokenWordAlpha",
                    50,
                    v =>
                    {
                        embedConfig.unspokenWordAlpha = Mathf.Clamp01(v / 100f);  // 0 = Words pop in as they are spoken, 1 = unspoken alpha equal to spoken words
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int startEmbedUnspoken = 50;
                embedUnspokenWordAlpha.SetCurrentValue(startEmbedUnspoken);
                syncUI.AddUI("embedUnspokenWordAlpha", embedUnspokenWordAlpha, 50);

                var embedSlant = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedSlant",
                    "LyricPlus_ModSettings_Slant",
                    20,
                    v =>
                    {
                        embedConfig.slant = Mathf.Clamp01(v / 100f);  // 0 = no slant, 0.2 = default, 1 = epic slant
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int startEmbedSlant = 20;
                embedSlant.SetCurrentValue(startEmbedSlant);
                syncUI.AddUI("embedSlant", embedSlant, startEmbedSlant);

                // Textbox Size
                var embedTextboxSize = UIHelper.CreateLargeMultiChoiceButton(
                    group.Transform,
                    "EmbedTextboxSize",
                    "LyricPlus_ModSettings_TextboxSize",
                    0,
                    v =>
                    {
                        embedConfig.textboxSize = Mathf.Clamp01(v / 100f);  // 0 = unchanged, 1 = effectively unbounded textbox
                    },
                    () => new IntRange(0, 101),
                    v => v.ToString()
                );
                int startEmbedTextboxSize = 0;
                embedTextboxSize.SetCurrentValue(startEmbedTextboxSize);
                syncUI.AddUI("embedTextboxSize", embedTextboxSize, 0);
                

                // APPLY //
                UIHelper.CreateButton(
                    group.Transform,
                    "ApplyEmbedLyricSettings",
                    "LyricPlus_ModSettings_Apply",
                    () =>
                    {
                        //EmbeddedDataManager.SaveShaderParametersForTrack(EmbeddedDataManager.currentFile, new LyricConfig(embedShaderParams));
                        embedConfig.SetParameters(embedShaderParams);
                        EmbeddedDataManager.SetLyricConfigForFile(EmbeddedDataManager.currentFile, embedConfig);
                    }
                );

                // TRIGGER FILE //
                openLyricFileButton = UIHelper.CreateButton(
                    group.Transform,
                    "CreateLyricTriggerButton",
                    "LyricPlus_ModSettings_CreateTriggerFile",
                    () =>
                    {
                        EmbeddedDataManager.CreateLyricTriggerFileForCurrentTrack();
                        EmbeddedDataManager.Reload();
                    }
                );

                UIHelper.CreateButton(
                    group.Transform,
                    "EmbedLyricTriggerButton",
                    "LyricPlus_ModSettings_EmbedTriggerFile",
                    () =>
                    {
                        if (!EmbeddedDataManager.HasEmbeddedData)
                        {
                            Debug.Log("No embedded data, embedding default embed settings first");

                            // First embed shader params if it doesn't have any already
                            //EmbeddedDataManager.SaveShaderParametersForTrack(EmbeddedDataManager.currentFile, new LyricShaderEmbedData(embedShaderParams));
                            EmbeddedDataManager.SetLyricConfigForFile(EmbeddedDataManager.currentFile, embedConfig);
                        }

                        EmbeddedDataManager.Reload(); // Reloads triggers just in case trigger file was just created / changed
                        EmbeddedDataManager.SaveTriggerDataForTrack(
                            EmbeddedDataManager.currentFile, 
                            new LyricTriggerEmbedData(LyricTriggers.LUTBaseKeys, LyricTriggers.colorTriggers, LyricTriggers.setTriggers, LyricTriggers.offsetTriggers, LyricTriggers.scaleTriggers, LyricTriggers.rotateTriggers)
                        );
                    }
                );

                quickModGroup = group;
                quickModGroup.GameObject.SetActive(EmbeddedDataManager.currentTrack != null);

                EmbeddedDataManager.SyncAllQuickmodEmbedUI();
            });
        }

        public static Dictionary<string, object> GetDefaultShaderParams()
        {
            return config.shaderParams;
        }
    }

    public struct LyricPlusConfig
    {
        public bool enableLyricPlus; // This only takes effect on chart restart due to the nature of the patch
        public bool embeddedOnly;
        public Color defaultColor;
        public float fadeInRatio;
        public float fadeOutRatio;

        public Dictionary<string, object> shaderParams = [];

        public LyricPlusConfig()
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
                Plugin.ApplyDefaultShaderParameters(name, f);
            }
        }

        public void SetColor(string name, Color value)
        {
            if (value is Color c)
            {
                shaderParams[name] = value;
                Plugin.ApplyDefaultShaderParameters(name, c);
            }
        }
    }
}
