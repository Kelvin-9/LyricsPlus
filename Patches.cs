using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ColoredLyrics
{
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyPatch(typeof(HdrMeshEffect), "Awake")]
        [HarmonyPrefix]
        internal static void HdrMeshEffect_AwakePrefix(HdrMeshEffect __instance)
        {
            if (!ConfigManager.config.enableColoredLyrics) return;

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
            if (newMat == null) return;
            Debug.Log("Swapped out mat");
            __instance.parentText.fontSharedMaterial = newMat;
        }

        [HarmonyPatch(typeof(BackgroundLyricLineDisplay), "PreRender")]
        [HarmonyPrefix]
        internal static void BackgroundLyricLineDisplay_PrerenderPrefix(ref TMP_TextInfo textInfo)
        {
            if (!ConfigManager.config.enableColoredLyrics) return;

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
            if (!ConfigManager.config.enableColoredLyrics) return;

            textInfo.textComponent.enableVertexGradient = true;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                ref TMP_CharacterInfo ptr = ref textInfo.characterInfo[i];
                if (!ptr.isVisible) continue;

                int vert = ptr.vertexIndex;                                       
                int matInd = ptr.materialReferenceIndex;

                // Grabbing that tint / alpha data modified by Prerender
                byte t = textInfo.meshInfo[matInd].colors32[vert].b;
                byte a = textInfo.meshInfo[matInd].colors32[vert].a;
                // Just straight up multiply tint and alpha who cares
                byte alpha = (byte)(a * (t / 255f));

                Color32 c = ptr.color;
                if (c.Equal(new Color32(255, 255, 255, 255)))
                {
                    // Default color hack
                    c = ModBase.defaultColor;
                }
                c = new Color32(c.r, c.g, c.b, alpha);

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
            if (!ConfigManager.config.enableColoredLyrics) return;

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