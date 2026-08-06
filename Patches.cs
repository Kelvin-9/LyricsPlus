using HarmonyLib;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColoredLyrics
{
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(HdrMeshEffect), "Awake")]
        [HarmonyPrefix]
        internal static void HdrMeshEffect_AwakePrefix(HdrMeshEffect __instance)
        {
            if (!ConfigManager.config.enableColoredLyrics || ConfigManager.config.embeddedOnly && !TrackLyricDataManager.hasEmbeddedData) return;

            bool isLyricsText = __instance.transform.parent?.name.Contains("BackgroundLyric(Clone)(Clone)") ?? false;
            if (!isLyricsText) return;

            Object.Destroy(__instance);
        }


        [HarmonyPatch(typeof(CustomTextMeshProHelper), "SetFont")]
        [HarmonyPostfix]
        internal static void CustomTextMeshProHelper_SetFontPostfix(CustomTextMeshProHelper __instance)
        {
            if (!ConfigManager.config.enableColoredLyrics) return;

            bool isLyricsText = __instance.parentText.transform.parent?.name.Contains("BackgroundLyric(Clone)(Clone)") ?? false;
            if (!isLyricsText) return;

            // Replace lyric's LUT material with default TextMeshPro/Distance Field stored in AssetBundle
            Material? newMat = ModBase.GetTextMaterial(__instance.parentText.font);
            if (newMat == null) 
            {
                return;
            }

            Debug.Log($"Material on {__instance.parentText.transform} replaced");
            __instance.parentText.fontSharedMaterial = newMat;
        }

        static bool _assignedDefault = false;
        static BackgroundLyricLineDisplay.AnimTimingSettings defaultAnim;
        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "SetPhrase")]
        [HarmonyPrefix]
        internal static void BackgroundLyricLineDisplay_SetPhrasePrefix(BackgroundLyricLineDisplay __instance)
        {
            if (__instance.textRenderer.fontSharedMaterial.shader != ModBase.textShader) return;

            BackgroundLyricLineDisplay.AnimTimingSettings anim = __instance.animTimingSettings;
            if (!_assignedDefault)
            {
                defaultAnim = anim;
                _assignedDefault = true;
            }

            float fadeIn  = TrackLyricDataManager.lyricConfig?.fadeInRatio  ?? ConfigManager.config.fadeInRatio;
            float fadeOut = TrackLyricDataManager.lyricConfig?.fadeOutRatio ?? ConfigManager.config.fadeOutRatio;
            //Debug.Log($"fade: {fadeIn} {fadeOut}");

            anim.fadeUpToFullRange.max   = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.fadeUpToFullRange.max,   fadeIn);
            anim.defaultFadeInRange.min  = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.defaultFadeInRange.min,  fadeIn);
            anim.defaultFadeInRange.max  = Mathf.Lerp(defaultAnim.fadeUpToFullRange.min, defaultAnim.defaultFadeInRange.max,  fadeIn);
            anim.defaultFadeOutRange.min = Mathf.Lerp(defaultAnim.fadeUpToFullRange.max, defaultAnim.defaultFadeOutRange.min, fadeOut);
            anim.defaultFadeOutRange.max = Mathf.Lerp(defaultAnim.fadeUpToFullRange.max, defaultAnim.defaultFadeOutRange.max, fadeOut);
            __instance.animTimingSettings = anim;
        }

        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPrefix]
        internal static void BackgroundLyricLineDisplay_PrerenderPrefix(ref TMP_TextInfo textInfo)
        {
            if (textInfo.textComponent.fontSharedMaterial.shader != ModBase.textShader) return;
            
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                if (!ptr.isVisible) continue;

                // Make sure vertex blue / alpha channels are maxed out to receive tint / alpha in Prerender().
                // Only using BL vertex for this since all vertices are applied the same data
                ptr.vertex_BL.color = new Color32(0, 0, 255, 255);
            }
        }

        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPostfix]
        internal static void BackgroundLyricLineDisplay_PrerenderPostfix(ref TMP_TextInfo textInfo)
        {
            if (textInfo.textComponent.fontSharedMaterial.shader != ModBase.textShader) return;

            textInfo.textComponent.enableVertexGradient = true;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                if (!ptr.isVisible) continue;

                int vert = ptr.vertexIndex;                                       
                int matInd = ptr.materialReferenceIndex;

                Color32 c = ptr.color;
                if (c.Equal(new Color32(255, 255, 255, 255), ignoreAlpha: true))
                {
                    // Default color hack
                    c = TrackLyricDataManager.lyricConfig?.defaultColor.ToUnityColor() ?? ConfigManager.config.defaultColor.ToUnityColor();
                }

                // Grabbing that tint / alpha data modified by Prerender
                float alpha = c.a * textInfo.meshInfo[matInd].colors32[vert].a / 255f;
                c = new Color32(c.r, c.g, c.b, (byte)alpha);

                textInfo.meshInfo[matInd].colors32[vert + 0] = c;
                textInfo.meshInfo[matInd].colors32[vert + 1] = c;
                textInfo.meshInfo[matInd].colors32[vert + 2] = c;
                textInfo.meshInfo[matInd].colors32[vert + 3] = c;
            }
        }

        [HarmonyPatch(typeof(TextMeshPro), "GenerateTextMesh")]
        [HarmonyPrefix]
        internal static void TextMeshPro_GenerateTextMeshPrefix(TextMeshPro __instance, ref UnityEngine.Color ___m_fontColor, ref Color32 ___m_fontColor32)
        {
            if (__instance.fontSharedMaterial.shader != ModBase.textShader) return;

            // Makes sure that font color doesn't default to the LUT color used to make text orange in base game
            ___m_fontColor = UnityEngine.Color.white;
            ___m_fontColor32 = new Color32(255, 255, 255, 255);
        }

        [HarmonyPatch(typeof(SplineTrackData.DataToGenerate), MethodType.Constructor, typeof(PlayableTrackData))]
        [HarmonyPostfix]
        private static void TrackConstructor(PlayableTrackData trackData)
        {
            // Load chart's embedded data
            TrackLyricDataManager.Load(trackData);
        }
    }
}