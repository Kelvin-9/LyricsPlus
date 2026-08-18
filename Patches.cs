using HarmonyLib;
using SpinCore.Triggers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

namespace LyricPlus
{
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(CustomTextMeshProHelper), "OnEnabled")]
        [HarmonyPostfix]
        internal static void CustomTextMeshProHelper_OnEnabledPostfix(CustomTextMeshProHelper __instance)
        {
            if (!ConfigManager.config.enableLyricPlus) return;

            bool isLyricsText = __instance.parentText.transform.parent?.name.Contains("BackgroundLyric(Clone)(Clone)") ?? false;
            if (!isLyricsText) return;

            // Replace lyric's LUT material with default TextMeshPro/Distance Field stored in AssetBundle
            Material? newMat = Plugin.GetTextMaterial(__instance.parentText.font);
            if (newMat == null)
            {
                return;
            }

            __instance.parentText.fontSharedMaterial = newMat;
        }


        // This prevents dynamic atlas generation from creating lag spikes as characters from other languages load in
        // Usually this lag is negligable but for more elaborate lyrics stuff this can noticably tank fps if not preloaded
        internal static void PreloadCharacters(TMP_FontAsset font, PlayableLyricData lyrics)
        {
            if (font == null) return;

            StringBuilder sb = new();
            foreach (var phrase in lyrics.Phrases)
            {
                sb.Append(phrase.Text);
            }
            string str = sb.ToString();

            foreach(var fallback in font.fallbackFontAssetTable)
            {
                PreloadForFont(str, fallback);
            }

            hasPreloadedLyricGlyphs = true;
        }


        private static void PreloadForFont(string str, TMP_FontAsset font)
        {
            if (font.atlasPopulationMode == AtlasPopulationMode.Static) return;

            font.HasCharacters(str, out List<char> missingChars);
            if (missingChars == null || missingChars.Count == 0)
            {
                return;
            }

            string missingStr = new(missingChars.ToArray());
            font.TryAddCharacters(missingStr);
        }


        // Set animation properties 
        static bool _assignedDefault = false;
        static BackgroundLyricLineDisplay.AnimTimingSettings defaultAnim;
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "SetPhrase")]
        [HarmonyPrefix]
        internal static void BackgroundLyricLineDisplay_SetPhrasePrefix(BackgroundLyricLineDisplay __instance)
        {
            if (!hasPreloadedLyricGlyphs)
            {
                PreloadCharacters(Plugin.mainLyricFont, __instance.GetLyrics());
            }

            BackgroundLyricLineDisplay.AnimTimingSettings anim = __instance.animTimingSettings;
            if (!_assignedDefault)
            {
                defaultAnim = anim;
                _assignedDefault = true;
            }

            float fadeIn  = EmbeddedDataManager.lyricConfig?.fadeInRatio  ?? ConfigManager.config.fadeInRatio;
            float fadeOut = EmbeddedDataManager.lyricConfig?.fadeOutRatio ?? ConfigManager.config.fadeOutRatio;
            anim.minLetterTime = Mathf.Lerp(0.000001f, defaultAnim.minLetterTime, fadeIn);
            anim.fadeUpToFullRange.max   = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.fadeUpToFullRange.max,   fadeIn);
            anim.defaultFadeInRange.min  = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.defaultFadeInRange.min,  fadeIn);
            anim.defaultFadeInRange.max  = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.defaultFadeInRange.max,  fadeIn);
            anim.defaultFadeOutRange.min = Mathf.Lerp(defaultAnim.fadeUpToFullRange.max, defaultAnim.defaultFadeOutRange.min, fadeOut);
            anim.defaultFadeOutRange.max = Mathf.Lerp(defaultAnim.fadeUpToFullRange.max, defaultAnim.defaultFadeOutRange.max, fadeOut);

            float slant = EmbeddedDataManager.lyricConfig?.slant ?? defaultAnim.slant;
            anim.slant = slant;

            __instance.animTimingSettings = anim;

            // EMBEDDED ONLY
            if (EmbeddedDataManager.lyricConfig != null)
            {
                // Set textbox size to effectively infinite when maxed out, otherwise scale linearly
                float size = EmbeddedDataManager.lyricConfig.textboxSize == 1 ? 9999999f : EmbeddedDataManager.lyricConfig.textboxSize * 100; 
                __instance.textRenderer.margin = new UnityEngine.Vector4(-size, __instance.textRenderer.margin.y, -size, __instance.textRenderer.margin.w);

                // Phrasing settings
                __instance.unspokenWordAlpha = EmbeddedDataManager.lyricConfig.unspokenWordAlpha;
            }
        }


        // Preprocess away LUT keys to their actual color
        [HarmonyPatch(typeof(SerializedLyricData), "BuildSyllables")]
        [HarmonyPrefix]
        internal static void SerializedLyricData_BuildSyllablesPrefix(ref string ___fullLyricsString)
        {
            if (!ConfigManager.config.enableLyricPlus) return;

            foreach (var pair in LyricTriggers.LUTKeys)
            {
                string key = pair.Key.Replace("LUT_", "");
                string target = "#" + ColorUtility.ToHtmlStringRGBA(pair.Value);
                ___fullLyricsString = ___fullLyricsString.Replace(key, target, StringComparison.OrdinalIgnoreCase);
            }
        }


        // Ensure alpha and tint has proper values to carry their value out of prerender
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPrefix]
        internal static bool BackgroundLyricLineDisplay_PrerenderPrefix(ref TMP_TextInfo textInfo)
        {
            if (textInfo.textComponent.fontSharedMaterial.shader != Plugin.textShader) return true;
            
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                if (!ptr.isVisible) continue;

                // Make sure vertex blue / alpha channels are maxed out to receive tint / alpha in Prerender().
                // Only using BL vertex for this since all vertices are applied the same data
                ptr.vertex_BL.color = new Color32(0, 0, 255, 255);
            }

            return true;
        }


        // DO ALL THE TRANSFORMATION AND COLORS
        static CharacterData[] charData = new CharacterData[100];
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPostfix]
        internal static void BackgroundLyricLineDisplay_PrerenderPostfix(BackgroundLyricLineDisplay __instance, ref TMP_TextInfo textInfo)
        {
            if (textInfo.textComponent.fontSharedMaterial.shader != Plugin.textShader) return;
            
            // Get fadein alpha
            PlayState playState = __instance.GetPlayState();
            TrackTick a = (playState != null) ? playState.currentTrackTick : default;
            float distanceAlpha = a.LinearMap(__instance.fadeOutRange).OneMinus() * a.LinearMap(__instance.fadeInRange) * a.LinearMap(__instance.fadeInToFullRange).LinearMapTo(__instance.animTimingSettings.preFadeInFullAlpha, 1f);


            int charCount = textInfo.characterCount;
            if (charCount > charData.Length)
            {
                Array.Resize(ref charData, charCount * 2);
            }

            if (EmbeddedDataManager.lyricConfig != null)
            {
                for (int i = 0; i < charCount; i++)
                {
                    ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                    if (!ptr.isVisible) continue;

                    Color32 col = ptr.color;

                    // LUT for offset triggers
                    LUTInfo info = EmbeddedDataManager.lyricConfig.GetLUTInfo(col);
                    Vector3 offset = info.offset;

                    Vector3 up = (ptr.vertex_TL.position - ptr.vertex_BL.position).normalized;
                    Vector3 right = (ptr.vertex_TR.position - ptr.vertex_TL.position).normalized;
                    Vector3 forward = Vector3.Cross(right, up);
                    Vector3 worldOffset =
                        right   * offset.x +
                        up      * offset.y +
                        forward * offset.z;

                    charData[i] = new(worldOffset, right, up, forward, Vector3.zero, info, false);
                }
            }

            for (int i = 0; i < charCount; i++)
            {
                ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                if (!ptr.isVisible) continue;

                int vert = ptr.vertexIndex;
                int matInd = ptr.materialReferenceIndex;

                Color32 col = ptr.color;

                // Default color hack
                if (col.Equal(new Color32(255, 255, 255, 255), ignoreAlpha: true))
                {
                    col = EmbeddedDataManager.lyricConfig?.defaultColor.ToUnityColor() ?? ConfigManager.config.defaultColor.ToUnityColor();
                }

                if (EmbeddedDataManager.lyricConfig != null)
                {
                    ref CharacterData data = ref charData[i];

                    // LUT for color triggers
                    col = data.lut.color;

                    // LUT for rotation
                    Vector5 rotInfo = data.lut.rotation;
                    Vector3 rotAxisDir = new(rotInfo.x, rotInfo.y, rotInfo.z);

                    int rotPivotIndex = Mathf.Clamp(rotInfo.v < 0 ? -(int)rotInfo.v : rotInfo.v == 0 ? i : (int)rotInfo.v - 1, 0, charCount - 1);
                    ref CharacterData rotPivotData = ref charData[rotPivotIndex];
                    ref TMP_CharacterInfo rotPivotChar = ref textInfo.characterInfo[rotPivotIndex];

                    if (!rotPivotData.pivotCalculated)
                    {
                        rotPivotData.pivot = ( 
                            rotPivotChar.vertex_BL.position +
                            rotPivotChar.vertex_BR.position +
                            rotPivotChar.vertex_TL.position +
                            rotPivotChar.vertex_TR.position
                        ) * 0.25f + rotPivotData.offset;

                        rotPivotData.pivotCalculated = true;
                    }
                    Vector3 rotPivot = rotPivotData.pivot;

                    Vector3 axis =
                        rotPivotData.right * rotAxisDir.x +
                        rotPivotData.up * rotAxisDir.y +
                        rotPivotData.forward * rotAxisDir.z;
                    float rotMult = rotInfo.v >= 0 ? 1 : 1 + Mathf.Abs(i - rotPivotIndex);
                    Quaternion rot = Quaternion.AngleAxis(rotInfo.w * rotMult, axis.normalized);

                    // LUT for scale
                    Vector4 scaleInfo = data.lut.scale;
                    int scalePivotIndex = Mathf.Clamp(scaleInfo.w < 0 ? -(int)scaleInfo.w : scaleInfo.w == 0 ? i : (int)scaleInfo.w - 1, 0, charCount - 1);

                    float scaleMult = scaleInfo.w >= 0 ? 1 : 1 + Mathf.Abs(i - scalePivotIndex);
                    Vector3 scale = new(scaleInfo.x * scaleMult, scaleInfo.y * scaleMult, scaleInfo.z * scaleMult);

                    ref CharacterData scalePivotData = ref charData[scalePivotIndex];
                    ref TMP_CharacterInfo scalePivotChar = ref textInfo.characterInfo[scalePivotIndex];

                    if (!scalePivotData.pivotCalculated)
                    {
                        scalePivotData.pivot = (
                            scalePivotChar.vertex_BL.position +
                            scalePivotChar.vertex_BR.position +
                            scalePivotChar.vertex_TL.position +
                            scalePivotChar.vertex_TR.position
                        ) * 0.25f + scalePivotData.offset;

                        scalePivotData.pivotCalculated = true;
                    }
                    Vector3 scalePivot = scalePivotData.pivot;

                    Vector3 p0 = textInfo.meshInfo[matInd].vertices[vert + 0];
                    Vector3 p1 = textInfo.meshInfo[matInd].vertices[vert + 1];
                    Vector3 p2 = textInfo.meshInfo[matInd].vertices[vert + 2];
                    Vector3 p3 = textInfo.meshInfo[matInd].vertices[vert + 3];
                    textInfo.meshInfo[matInd].vertices[vert + 0] = (p0 + data.offset).ScaleAroundPivot(scalePivot, scalePivotData.right, scalePivotData.up, scalePivotData.forward, scale).RotateAroundPivot(rotPivot, rot);
                    textInfo.meshInfo[matInd].vertices[vert + 1] = (p1 + data.offset).ScaleAroundPivot(scalePivot, scalePivotData.right, scalePivotData.up, scalePivotData.forward, scale).RotateAroundPivot(rotPivot, rot);
                    textInfo.meshInfo[matInd].vertices[vert + 2] = (p2 + data.offset).ScaleAroundPivot(scalePivot, scalePivotData.right, scalePivotData.up, scalePivotData.forward, scale).RotateAroundPivot(rotPivot, rot);
                    textInfo.meshInfo[matInd].vertices[vert + 3] = (p3 + data.offset).ScaleAroundPivot(scalePivot, scalePivotData.right, scalePivotData.up, scalePivotData.forward, scale).RotateAroundPivot(rotPivot, rot);
                }

                // Grabbing that tint / alpha data modified by Prerender
                bool ignoreTint = col.a == 254; // OVERRIDE TINT IF ALPHA OF LUT IS EXACTLY 254 (hacky i know)
                float alpha = col.a * (ignoreTint ? distanceAlpha : (textInfo.meshInfo[matInd].colors32[vert].a / 255f));
                col = new Color32(col.r, col.g, col.b, (byte)alpha);

                textInfo.meshInfo[matInd].colors32[vert + 0] = col;
                textInfo.meshInfo[matInd].colors32[vert + 1] = col;
                textInfo.meshInfo[matInd].colors32[vert + 2] = col;
                textInfo.meshInfo[matInd].colors32[vert + 3] = col;
            }

            for (int i = 0; i < charCount; i++)
                charData[i].pivotCalculated = false;
        }

        struct CharacterData
        {
            public Vector3 offset;
            public Vector3 right;
            public Vector3 up;
            public Vector3 forward;
            public Vector3 pivot;
            public bool pivotCalculated;
            public LUTInfo lut;

            public CharacterData(Vector3 offset, Vector3 right, Vector3 up, Vector3 forward, Vector3 pivot, LUTInfo lut, bool calculated)
            {
                this.offset = offset;
                this.right = right;
                this.up = up;
                this.forward = forward;
                this.pivot = pivot;
                this.lut = lut;
                pivotCalculated = calculated;
            }
        }

        [HarmonyPatch(typeof(TextMeshPro), "GenerateTextMesh")]
        [HarmonyPrefix]
        internal static void TextMeshProGenerateTextMeshPrefix(TextMeshPro __instance, ref UnityEngine.Color ___m_fontColor, ref Color32 ___m_fontColor32)
        {
            if (__instance.fontSharedMaterial.shader != Plugin.textShader) return;

            // Makes sure that font color doesn't default to the LUT color used to make text orange in base game
            ___m_fontColor = UnityEngine.Color.white;
            ___m_fontColor32 = new Color32(255, 255, 255, 255);
        }


        // On new chart selected
        static bool hasPreloadedLyricGlyphs = false;
        [HarmonyPatch(typeof(SplineTrackData.DataToGenerate), MethodType.Constructor, typeof(PlayableTrackData))]
        [HarmonyPostfix]
        private static void TrackConstructor(PlayableTrackData trackData)
        {
            // Load chart's embedded data
            bool loaded = EmbeddedDataManager.Load(trackData);
            ConfigManager.quickModGroup?.GameObject.SetActive(loaded);
            hasPreloadedLyricGlyphs = false;
        }

        [HarmonyPatch(typeof(TMP_InputField), "OnEnable")]
        [HarmonyPostfix]
        static void TMPInputField_OnEnablePostfix(TMP_InputField __instance)
        {
            if (__instance.gameObject.name == "LyricText")
            {
                __instance.lineLimit = 0;
            }
        }

        // Truncate timeline text so that big text doesn't cover the entire editor
        [HarmonyPatch(typeof(DetailedTimelineTextBar), "AddText")]
        [HarmonyPostfix]
        static void DetailedTimelineTextBar_AddTextPostfix(ref List<DetailedTimelineText> ___usedText)
        {
            if (___usedText == null || ___usedText.Count <= 0) return;

            var lastTextElement = ___usedText[^1];

            if (lastTextElement == null || lastTextElement.Text == null) return;

            lastTextElement.Text.enableAutoSizing = false;
            lastTextElement.Text.autoSizeTextContainer = false;
            lastTextElement.Text.overflowMode = TextOverflowModes.Ellipsis;

            var rect = lastTextElement.RectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 200f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);
        }

        // Patch spincore to allow unregistering events because the store keys are not the same for every chart
        internal static Dictionary<string, bool> registeredEvents = [];         // Maps fullkey to whether they should be cleared from the store
        [HarmonyPatch(typeof(TriggerManager), "InternalRegisterTriggerEvent")]
        [HarmonyPrefix]
        static void TriggerManager_InternalRegisterTriggerEventPrefix(ref string fullKey, object ___TriggerStores)
        {
            if (___TriggerStores is not IDictionary triggerStores) return;

            foreach (string key in registeredEvents.Keys.ToList())
            {
                if (!registeredEvents[key] || !triggerStores.Contains(key)) continue;
                triggerStores.Remove(key);
                registeredEvents.Remove(key);
            }

            if (fullKey.StartsWith(Assembly.GetAssembly(typeof(Plugin)).GetName().Name))
            {
                registeredEvents.Add(fullKey, false);
            }
        }

        internal static void ClearTriggerStores()
        {
            foreach (string key in registeredEvents.Keys.ToList())
            {
                registeredEvents[key] = true;
            }
        }
    }
}