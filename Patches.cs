using HarmonyLib;
using System;
using TMPro;
using UnityEngine;

namespace LyricPlus
{
    [HarmonyPatch]
    public class Patches
    {
        // Swaps material with one that uses unmodified textmeshpro shader
        [HarmonyPatch(typeof(CustomTextMeshProHelper), "SetFont")]
        [HarmonyPostfix]
        internal static void CustomTextMeshProHelper_SetFontPostfix(CustomTextMeshProHelper __instance)
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

        // Set animation properties 
        static bool _assignedDefault = false;
        static BackgroundLyricLineDisplay.AnimTimingSettings defaultAnim;
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "SetPhrase")]
        [HarmonyPrefix]
        internal static void BackgroundLyricLineDisplay_SetPhrasePrefix(BackgroundLyricLineDisplay __instance)
        {
            if (__instance.textRenderer.fontSharedMaterial.shader != Plugin.textShader) return;

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
                __instance.textRenderer.margin = new Vector4(-size, __instance.textRenderer.margin.y, -size, __instance.textRenderer.margin.w);

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


        // Apply color
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPostfix]
        internal static void BackgroundLyricLineDisplay_PrerenderPostfix(ref TMP_TextInfo textInfo)
        {
            if (textInfo.textComponent.fontSharedMaterial.shader != Plugin.textShader) return;

            Transform transform = textInfo.textComponent.transform;
            Vector3[] pivots = new Vector3[textInfo.characterCount];
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                pivots[i] = (
                    textInfo.characterInfo[i].vertex_BL.position +
                    textInfo.characterInfo[i].vertex_BR.position +
                    textInfo.characterInfo[i].vertex_TL.position +
                    textInfo.characterInfo[i].vertex_TR.position
                ) / 4f;
            }

            for (int i = 0; i < textInfo.characterCount; i++)
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
                    // LUT for color triggers
                    Color32 key = col;
                    col = EmbeddedDataManager.lyricConfig.EvaluateLUTColor(key);

                    // LUT for offset triggers
                    Vector3 p0 = textInfo.meshInfo[matInd].vertices[vert + 0];
                    Vector3 p1 = textInfo.meshInfo[matInd].vertices[vert + 1];
                    Vector3 p2 = textInfo.meshInfo[matInd].vertices[vert + 2];
                    Vector3 p3 = textInfo.meshInfo[matInd].vertices[vert + 3];

                    Vector3 offset = EmbeddedDataManager.GetOffset(key);

                    Vector3 up = ptr.vertex_TL.position - ptr.vertex_BL.position;
                    Vector3 right = ptr.vertex_TR.position - ptr.vertex_TL.position;
                    Vector3 forward = Vector3.Cross(right, up);
                    Vector3 worldOffset =
                        right.normalized   * offset.x +
                        up.normalized      * offset.y +
                        forward.normalized * offset.z;

                    // LUT for rotation
                    Vector5 rotInfo = EmbeddedDataManager.GetRotation(key);
                    Vector3 dir = new(rotInfo.x, rotInfo.y, rotInfo.z);
                    Vector3 axis =
                        right.normalized   * dir.x +
                        up.normalized      * dir.y +
                        forward.normalized * dir.z;
                    float degrees = rotInfo.w;
                    int pivotIndex = Mathf.Clamp((int)rotInfo.v, 0, pivots.Length - 1);

                    textInfo.meshInfo[matInd].vertices[vert + 0] = RotatePointAroundPivot(p0, pivots[pivotIndex], axis, degrees) + worldOffset;
                    textInfo.meshInfo[matInd].vertices[vert + 1] = RotatePointAroundPivot(p1, pivots[pivotIndex], axis, degrees) + worldOffset;
                    textInfo.meshInfo[matInd].vertices[vert + 2] = RotatePointAroundPivot(p2, pivots[pivotIndex], axis, degrees) + worldOffset;
                    textInfo.meshInfo[matInd].vertices[vert + 3] = RotatePointAroundPivot(p3, pivots[pivotIndex], axis, degrees) + worldOffset;
                }

                // Grabbing that tint / alpha data modified by Prerender
                float alpha = col.a * textInfo.meshInfo[matInd].colors32[vert].a / 255f;
                col = new Color32(col.r, col.g, col.b, (byte)alpha);

                textInfo.meshInfo[matInd].colors32[vert + 0] = col;
                textInfo.meshInfo[matInd].colors32[vert + 1] = col;
                textInfo.meshInfo[matInd].colors32[vert + 2] = col;
                textInfo.meshInfo[matInd].colors32[vert + 3] = col;
            }
        }

        static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 axis, float degrees)
        {
            Vector3 dir = point - pivot;
            Quaternion rotation = Quaternion.AngleAxis(degrees, axis.normalized);

            return pivot + (rotation * dir);
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
        [HarmonyPatch(typeof(SplineTrackData.DataToGenerate), MethodType.Constructor, typeof(PlayableTrackData))]
        [HarmonyPostfix]
        private static void TrackConstructor(PlayableTrackData trackData)
        {
            // Load chart's embedded data
            bool loaded = EmbeddedDataManager.Load(trackData);
            ConfigManager.quickModGroup?.GameObject.SetActive(loaded);
        }
    }
}