using SpinCore.Triggers;
using System.Collections.Generic;
using UnityEngine;

namespace ColoredLyrics
{
    internal class LyricTriggers
    {
        public static Dictionary<string, Color32> TriggerKeys = [];
        internal static Dictionary<Color32, List<LyricTrigger>> triggers = new();

        public static void LoadTriggers(Dictionary<string, List<LyricTrigger>> data)
        {
            ClearAll();

            foreach (KeyValuePair<string, List<LyricTrigger>> item in data)
            {
                if (!ColorUtility.TryParseHtmlString(item.Key.StartsWith("#") ? item.Key : "#" + item.Key, out UnityEngine.Color col))
                {
                    Debug.Log($"Could not parse color string {item.Key}");
                    continue;
                }

                triggers.Add(col, item.Value);
            }

            RegisterEvents();
            LoadTriggers(TriggerKeys, triggers);
        }

        public static void ClearAll()
        {
            Debug.Log("Clearing Triggers");
            foreach (var pair in TriggerKeys)
            {
                TriggerManager.ClearTriggers(pair.Key);
            }
            TriggerKeys.Clear();
            triggers.Clear();
        }

        private static void RegisterEvents()
        {
            int keyIndex = 0;
            foreach (Color32 keyColor in triggers.Keys)
            {
                string triggerKey = $"LUT{keyIndex}";
                Debug.Log($"Adding key {triggerKey} mapping from {keyColor}");
                TriggerKeys[triggerKey] = keyColor;  // e.g. LUT0 = Color32(r, g, b, a)
                TriggerManager.RegisterTriggerEvent<LyricTrigger>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = TriggerKeys[triggerKey];
                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyLUT(mapFrom, trigger.StartColor.ToColor32());
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Color32 col = UnityEngine.Color.Lerp(trigger.StartColor.ToUnityColor(), trigger.EndColor.ToUnityColor(), t); // TODO: Use hsv lerping?
                    EmbeddedDataManager.ModifyLUT(mapFrom, col);
                });

                keyIndex++;
            }
        }

        private static void LoadTriggers(Dictionary<string, Color32> keys, Dictionary<Color32, List<LyricTrigger>> triggers)
        {
            foreach (KeyValuePair<string, Color32> pair in keys)
            {
                string LUTIndexKey = pair.Key;  // LUT0, LUT1 ... LUTn
                TriggerManager.LoadTriggers(LUTIndexKey, triggers[pair.Value].ToArray());
                Debug.Log($"Loaded {triggers[pair.Value].Count} {LUTIndexKey} triggers");
            }
        }
    }

    internal class LyricTrigger : ITrigger 
    {
        public float Time { get; set; }
        public float Duration { get; set; }
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }

        public LyricTrigger()
        {

        }

        public LyricTrigger(float Time, Color StartColor)
        {
            this.Time = Time;
            Duration = 0;
            this.StartColor = StartColor;
            EndColor = StartColor;
        }

        public LyricTrigger(float Time, float Duration, Color StartColor, Color EndColor)
        {
            this.Time = Time;
            this.Duration = Duration;
            this.StartColor = StartColor;
            this.EndColor = EndColor;
        }

        public override string ToString()
        {
            return $"Time: {Time} Duration: {Duration} Start: {StartColor} End: {EndColor}";
        }
    }


    internal struct LyricTriggerEmbedData
    {
        public Dictionary<string, List<LyricTrigger>> triggers = new();

        public LyricTriggerEmbedData()
        {
            triggers = new();
        }

        public LyricTriggerEmbedData(Dictionary<string, List<LyricTrigger>> dict)
        {
            triggers = dict;
        }

        public void AddTrigger(string key, LyricTrigger trigger)
        {
            triggers ??= new();

            if (!triggers.ContainsKey(key))
            {
                triggers[key] = new();
            }

            triggers[key].Add(trigger);
        }

        public void AddTriggers(string key, List<LyricTrigger> triggers)
        {
            for (int i = 0; i < triggers.Count; i++)
            {
                AddTrigger(key, triggers[i]);
            }
        }
    }
}
