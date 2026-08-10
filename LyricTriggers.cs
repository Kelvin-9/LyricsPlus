using DG.Tweening;
using SpinCore.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ColoredLyrics
{
    internal class LyricTriggers
    {
        internal static Dictionary<string, Color32> LUTKeys = [];
        internal static Dictionary<Color32, List<Trigger<Color>>> colorTriggers = [];
        internal static Dictionary<string, List<Trigger<float>>> setTriggers = [];
        internal static Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers = [];   // Offset uses Vector4 as W stores easing
        internal static bool hasTriggers = false;

        internal static void LoadTriggers(
            Dictionary<string, List<Trigger<Color>>> colorTriggers, 
            Dictionary<string, Color32> colorKeys, 
            Dictionary<string, List<Trigger<float>>> setTriggers,
            Dictionary<string, List<Trigger<Vector4>>> offsetTriggers
            )
        {
            ClearAll();

            hasTriggers = colorTriggers.Count + setTriggers.Count + offsetTriggers.Count > 0;

            LUTKeys = colorKeys;

            foreach (KeyValuePair<string, List<Trigger<Color>>> item in colorTriggers)
            {
                if (!ColorUtility.TryParseHtmlString(item.Key.StartsWith("#") ? item.Key : "#" + item.Key, out UnityEngine.Color col))
                {
                    Debug.Log($"Could not parse color string {item.Key}");
                    continue;
                }

                LyricTriggers.colorTriggers.Add(col, item.Value);
            }

            foreach (KeyValuePair<string, List<Trigger<float>>> item in setTriggers)
            {
                LyricTriggers.setTriggers.Add(item.Key, item.Value);
                Debug.Log($"Loading {item.Key}: {item.Value.Count}");
            }

            foreach (KeyValuePair<string, List<Trigger<Vector4>>> item in offsetTriggers)
            {
                if (!ColorUtility.TryParseHtmlString(item.Key.StartsWith("#") ? item.Key : "#" + item.Key, out UnityEngine.Color col))
                {
                    Debug.Log($"Could not parse color string {item.Key}");
                    continue;
                }

                LyricTriggers.offsetTriggers.Add(col, item.Value);
            }

            RegisterEvents();
            LoadTriggersIntoManager(LUTKeys, LyricTriggers.colorTriggers, LyricTriggers.setTriggers, LyricTriggers.offsetTriggers);
        }

        internal static void LoadTriggers(
            Dictionary<Color32, List<Trigger<Color>>> colorTriggers, 
            Dictionary<string, Color32> colorKeys, 
            Dictionary<string, List<Trigger<float>>> setTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers
            )
        {
            ClearAll();

            hasTriggers = colorTriggers.Count + setTriggers.Count + offsetTriggers.Count > 0;

            LUTKeys = colorKeys;
            LyricTriggers.colorTriggers = colorTriggers;
            LyricTriggers.setTriggers = setTriggers;
            LyricTriggers.offsetTriggers = offsetTriggers;

            RegisterEvents();
            LoadTriggersIntoManager(LUTKeys, LyricTriggers.colorTriggers, LyricTriggers.setTriggers, LyricTriggers.offsetTriggers);
            foreach (var pair in LUTKeys.ToList())
            {
                LUTKeys.Add("o" + pair.Key, pair.Value);
            }
        }

        internal static void ClearAll()
        {
            foreach (var pair in LUTKeys)
            {
                TriggerManager.ClearTriggers(pair.Key);
            }
            LUTKeys.Clear();
            colorTriggers.Clear();
            setTriggers.Clear();
            offsetTriggers.Clear();
        }

        private static void RegisterEvents()
        {
            Dictionary<Color32, string> colorToKey = [];

            foreach (var pair in LUTKeys)
            {
                colorToKey.Add(pair.Value, pair.Key);
            }

            foreach (Color32 keyColor in colorTriggers.Keys)
            {
                string triggerKey = colorToKey[keyColor];
                TriggerManager.RegisterTriggerEvent<Trigger<Color>>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = LUTKeys[triggerKey];
                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyLUT(mapFrom, trigger.StartValue.ToColor32());
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Color32 col = Util.LerpHSL(trigger.StartValue.ToUnityColor(), trigger.EndValue.ToUnityColor(), t);
                    EmbeddedDataManager.ModifyLUT(mapFrom, col);
                });
            }

            foreach (string setKey in setTriggers.Keys)
            {
                TriggerManager.RegisterTriggerEvent<Trigger<float>>(setKey, (trigger, time) =>
                {
                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.SetVariable(setKey, trigger.StartValue);
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    float val = Mathf.Lerp(trigger.StartValue, trigger.EndValue, t);
                    EmbeddedDataManager.SetVariable(setKey, val);
                });
            }

            foreach (Color32 keyColor in offsetTriggers.Keys)
            {
                string triggerKey = "o" + colorToKey[keyColor];
                TriggerManager.RegisterTriggerEvent<Trigger<Vector4>>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = LUTKeys[triggerKey];
                    Vector3 from = new(trigger.StartValue.x, trigger.StartValue.y, trigger.StartValue.z);
                    Vector3 to = new(trigger.EndValue.x, trigger.EndValue.y, trigger.EndValue.z);
                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyOffset(mapFrom, from);
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Ease easing = (Ease)trigger.StartValue.w;
                    Vector3 offset = Vector3.LerpUnclamped(from, to, Easing.Evaluate(t, easing));
                    EmbeddedDataManager.ModifyOffset(mapFrom, offset);
                });
            }
        }


        private static void LoadTriggersIntoManager(
            Dictionary<string, Color32> keys, 
            Dictionary<Color32, List<Trigger<Color>>> colorTriggers, 
            Dictionary<string, List<Trigger<float>>> setTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers
        )
        {
            // By using the keys dictionary, the string keys are turned to Color32 before being used as key so that its easier to look up
            foreach (KeyValuePair<string, Color32> pair in keys)
            {
                string triggerKey = pair.Key;
                Color32 colorKey = pair.Value;
                List<ITrigger> triggers = [];

                if (colorTriggers.ContainsKey(colorKey))
                {
                    //Debug.Log($"LOADING EVENTS {triggerKey} {colorTriggers[colorKey].Count}");
                    colorTriggers[colorKey].OrderBy(a => a.Time);
                    colorTriggers[colorKey].Add(new Trigger<Color>
                    {
                        Time = 0,
                        StartValue = colorTriggers[colorKey][0].StartValue
                    });
                    TriggerManager.LoadTriggers(triggerKey, colorTriggers[colorKey].ToArray());
                }

                if (offsetTriggers.ContainsKey(colorKey))
                {
                    //Debug.Log($"LOADING EVENTS {"o" + triggerKey} {offsetTriggers[colorKey].Count}");
                    offsetTriggers[colorKey].OrderBy(a => a.Time);
                    offsetTriggers[colorKey].Add(new Trigger<Vector4>
                    {
                        Time = 0,
                        StartValue = offsetTriggers[colorKey][0].StartValue
                    });
                    TriggerManager.LoadTriggers("o" + triggerKey, offsetTriggers[colorKey].ToArray());
                }

            }

            foreach (string var in setTriggers.Keys)
            {
                TriggerManager.LoadTriggers(var, setTriggers[var].ToArray());
            }
        }
    }

    static internal class Easing
    {
        /// easings.net
        public static float Evaluate(float t, Ease type)
        {
            return DOVirtual.EasedValue(0, 1, Mathf.Clamp01(t), type, 1);
        }

        public static Ease ParseEase(string str)
        {
            try 
            { 
                return Enum.Parse<Ease>(str, true);
            }
            catch
            {
                return Ease.Linear;
            }
        }
    }


    internal class Trigger<T> : ITrigger 
    {
        public float Time { get; set; }
        public float Duration { get; set; }
        public T StartValue { get; set; }
        public T EndValue { get; set; }

        public Trigger() 
        { 

        }

        public Trigger(float Time, T StartValue)
        {
            this.Time = Time;
            Duration = 0;
            this.StartValue = StartValue;
            EndValue = StartValue;
        }

        public Trigger(float Time, float Duration, T StartValue, T EndValue)
        {
            this.Time = Time;
            this.Duration = Duration;
            this.StartValue = StartValue;
            this.EndValue = EndValue;
        }

        public override string ToString()
        {
            return $"Time: {Time} Duration: {Duration} Start: {StartValue} End: {EndValue}";
        }
    }


    internal struct LyricTriggerEmbedData
    {
        public Dictionary<string, List<Trigger<Color>>> colorTriggers = [];
        public Dictionary<string, Color32> colorKeys = [];
        public Dictionary<string, List<Trigger<float>>> setTriggers = [];
        public Dictionary<string, List<Trigger<Vector4>>> offsetTriggers = [];

        public LyricTriggerEmbedData()
        {
            colorTriggers = [];
            setTriggers = [];
            offsetTriggers = [];
        }

        public LyricTriggerEmbedData(Dictionary<string, Color32> colorKeys, 
            Dictionary<string, List<Trigger<Color>>> colorDict, 
            Dictionary<string, List<Trigger<float>>> setDict, 
            Dictionary<string, List<Trigger<Vector4>>> offsetDict)
        {
            this.colorKeys = colorKeys;
            colorTriggers = colorDict;
            setTriggers = setDict;
            offsetTriggers = offsetDict;
        }

        public LyricTriggerEmbedData(Dictionary<string, Color32> colorKeys, 
            Dictionary<Color32, List<Trigger<Color>>> colorDict, 
            Dictionary<string, List<Trigger<float>>> setDict, 
            Dictionary<Color32, List<Trigger<Vector4>>> offsetDict)
        {
            this.colorKeys = colorKeys;

            colorTriggers = [];
            foreach(var pair in colorDict)
            {
                colorTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }

            setTriggers = setDict;

            offsetTriggers = [];
            foreach (var pair in offsetDict)
            {
                offsetTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }
        }
    }
}
