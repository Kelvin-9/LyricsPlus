using DG.Tweening;
using Newtonsoft.Json;
using SpinCore.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LyricPlus
{
    internal class LyricTriggers
    {
        internal static Dictionary<string, Color32> LUTKeys = [];
        internal static Dictionary<string, Color32> LUTBaseKeys = [];
        internal static HashSet<string> allTriggerKeys = [];
        internal static Dictionary<Color32, List<Trigger<Color>>>   colorTriggers  = [];
        internal static Dictionary<string,  List<Trigger<float>>>   setTriggers    = [];
        internal static Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers = [];   // Offset uses Vector4 as W stores easing
        internal static Dictionary<Color32, List<Trigger<Vector4>>> scaleTriggers  = [];   // Scale uses Vector4 as W stores pivot and ease (start.w = pivot, end.w = ease)
        internal static Dictionary<Color32, List<Trigger<Vector5>>> rotateTriggers = [];   // Rotation uses first 3 values as direction, 4th value as degrees and 5th as easing
        internal static bool hasTriggers = false;

        internal static void LoadTriggers(LyricTriggerEmbedData data)
        {
            ClearAll();
            if (!EmbeddedDataManager.HasEmbeddedData) return;

            LUTKeys = data.colorKeys;
            foreach(var pair in data.colorKeys)
            {
                LUTBaseKeys.Add(pair.Key, pair.Value);
            }
            var colorTriggers = data.colorTriggers ?? [];
            var setTriggers = data.setTriggers ?? [];
            var offsetTriggers = data.offsetTriggers ?? [];
            var scaleTriggers  = data.scaleTriggers ?? [];
            var rotateTriggers = data.rotateTriggers ?? [];

            hasTriggers = colorTriggers.Count + setTriggers.Count + offsetTriggers.Count + scaleTriggers.Count + rotateTriggers.Count > 0;

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

            foreach (KeyValuePair<string, List<Trigger<Vector4>>> item in scaleTriggers)
            {
                if (!ColorUtility.TryParseHtmlString(item.Key.StartsWith("#") ? item.Key : "#" + item.Key, out UnityEngine.Color col))
                {
                    Debug.Log($"Could not parse color string {item.Key}");
                    continue;
                }

                LyricTriggers.scaleTriggers.Add(col, item.Value);
            }

            foreach (KeyValuePair<string, List<Trigger<Vector5>>> item in rotateTriggers)
            {
                if (!ColorUtility.TryParseHtmlString(item.Key.StartsWith("#") ? item.Key : "#" + item.Key, out UnityEngine.Color col))
                {
                    Debug.Log($"Could not parse color string {item.Key}");
                    continue;
                }

                LyricTriggers.rotateTriggers.Add(col, item.Value);
            }

            RegisterEvents();
            LoadTriggersIntoManager(LUTBaseKeys, LyricTriggers.colorTriggers, LyricTriggers.setTriggers, LyricTriggers.offsetTriggers, LyricTriggers.scaleTriggers, LyricTriggers.rotateTriggers);
        }

        internal static void LoadTriggers(
            Dictionary<Color32, List<Trigger<Color>>> colorTriggers,
            Dictionary<string, Color32> colorKeys,
            Dictionary<string, List<Trigger<float>>> setTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> scaleTriggers,
            Dictionary<Color32, List<Trigger<Vector5>>> rotateTriggers
            )
        {
            ClearAll();
            if (!EmbeddedDataManager.HasEmbeddedData) return;

            hasTriggers = colorTriggers.Count + setTriggers.Count + offsetTriggers.Count + scaleTriggers.Count + rotateTriggers.Count > 0;

            LUTKeys = colorKeys;
            foreach (var pair in colorKeys)
            {
                LUTBaseKeys.Add(pair.Key, pair.Value);
            }
            LyricTriggers.colorTriggers  = colorTriggers;
            LyricTriggers.setTriggers    = setTriggers;
            LyricTriggers.offsetTriggers = offsetTriggers;
            LyricTriggers.scaleTriggers  = scaleTriggers;
            LyricTriggers.rotateTriggers = rotateTriggers;

            RegisterEvents();
            LoadTriggersIntoManager(LUTBaseKeys, LyricTriggers.colorTriggers, LyricTriggers.setTriggers, LyricTriggers.offsetTriggers, LyricTriggers.scaleTriggers, LyricTriggers.rotateTriggers);
        }

        internal static Dictionary<Color32, List<Trigger<T>>> SortTriggers<T>(Dictionary<Color32, List<Trigger<T>>> dict, Func<Trigger<T>, Trigger<T>, Trigger<T>> RelativeResolver)
        {
            var keys = dict.Keys.ToList();
            for (int i = 0; i < dict.Count; i++)
            {
                var key = keys[i];
                var item = dict[key];
                var li = item.OrderBy(t => t.Time).ToList();

                if (li.Count <= 0) continue;
                for (int j = 1; j < li.Count; j++)
                {
                    if (RelativeResolver == null) break;
                    if (!li[j].IsRelative) continue;

                    li[j] = RelativeResolver(li[j], li[j - 1]);
                    li[j].IsRelative = false;
                }
                dict[key] = li;

                //for (int j = 0; j < li.Count; j++)
                //{
                //    Debug.Log($"trigger [{key}] {li[j]}");
                //}

                if (li[0].Time == 0) continue;

                dict[key].Add(new Trigger<T>
                {
                    Time = 0,
                    StartValue = li[0].StartValue
                });
            }


            return dict;
        }

        internal static void ClearAll()
        {
            foreach (string key in allTriggerKeys)
            {
                TriggerManager.ClearTriggers(key);
            }
            Patches.ClearTriggerStores();

            //foreach (string key in setTriggers.Keys)
            //{
            //    TriggerManager.ClearTriggers(key);
            //}

            LUTKeys.Clear();
            LUTBaseKeys.Clear();
            colorTriggers.Clear();
            setTriggers.Clear();
            offsetTriggers.Clear();
            scaleTriggers.Clear();
            rotateTriggers.Clear();
            allTriggerKeys.Clear();
        }

        private static void RegisterEvents()
        {
            Dictionary<Color32, string> colorToKey = [];

            foreach (var pair in LUTKeys)
            {
                if (colorToKey.ContainsKey(pair.Value)) continue;
                colorToKey.Add(pair.Value, pair.Key);
            }

            foreach (Color32 keyColor in colorTriggers.Keys)
            {
                string triggerKey = colorToKey[keyColor];
                //Debug.Log($"Registering color trigger {triggerKey}");
                TriggerManager.RegisterTriggerEvent<Trigger<Color>>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = LUTKeys[triggerKey];
                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyLUT(mapFrom, trigger.StartValue.ToColor32());
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Color32 col = Util.LerpHSV(trigger.StartValue.ToUnityColor(), trigger.EndValue.ToUnityColor(), t);
                    EmbeddedDataManager.ModifyLUT(mapFrom, col);
                });
            }

            foreach (string setKey in setTriggers.Keys)
            {
                //Debug.Log($"Registering set trigger {setKey}");
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
                //Debug.Log($"Registering offset trigger {triggerKey}");
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

            foreach (Color32 keyColor in scaleTriggers.Keys)
            {
                string triggerKey = "s" + colorToKey[keyColor];
                //Debug.Log($"Registering scale trigger {triggerKey}");
                TriggerManager.RegisterTriggerEvent<Trigger<Vector4>>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = LUTKeys[triggerKey];
                    Vector4 from = new(trigger.StartValue.x, trigger.StartValue.y, trigger.StartValue.z, trigger.StartValue.w);
                    Vector4 to = new(trigger.EndValue.x, trigger.EndValue.y, trigger.EndValue.z, trigger.StartValue.w);
                    int pivot = (int)trigger.StartValue.w;

                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyScale(mapFrom, from);
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Ease easing = (Ease)trigger.EndValue.w;
                    Vector4 scale = Vector4.LerpUnclamped(from, to, Easing.Evaluate(t, easing));
                    EmbeddedDataManager.ModifyScale(mapFrom, scale.WithW(pivot));
                });
            }

            foreach (Color32 keyColor in rotateTriggers.Keys)
            {
                string triggerKey = "r" + colorToKey[keyColor];
                //Debug.Log($"Registering rotate trigger {triggerKey}");
                TriggerManager.RegisterTriggerEvent<Trigger<Vector5>>(triggerKey, (trigger, time) =>
                {
                    Color32 mapFrom = LUTKeys[triggerKey];
                    Vector4 from = new(trigger.StartValue.x, trigger.StartValue.y, trigger.StartValue.z, trigger.StartValue.w);
                    Vector4 to = new(trigger.EndValue.x, trigger.EndValue.y, trigger.EndValue.z, trigger.EndValue.w);
                    int pivotCharIndex = (int)trigger.StartValue.v;

                    if (trigger.Duration == 0f)
                    {
                        EmbeddedDataManager.ModifyRotation(mapFrom, new Vector5(from, pivotCharIndex));
                        return;
                    }

                    float t = (time - trigger.Time) / trigger.Duration;
                    Ease easing = (Ease)trigger.EndValue.v;
                    Vector4 rotation = Vector4.LerpUnclamped(from, to, Easing.Evaluate(t, easing));
                    EmbeddedDataManager.ModifyRotation(mapFrom, new Vector5(rotation, pivotCharIndex));
                });
            }
        }


        private static void LoadTriggersIntoManager(
            Dictionary<string, Color32> keys, 
            Dictionary<Color32, List<Trigger<Color>>> colorTriggers, 
            Dictionary<string, List<Trigger<float>>> setTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers,
            Dictionary<Color32, List<Trigger<Vector4>>> scaleTriggers,
            Dictionary<Color32, List<Trigger<Vector5>>> rotateTriggers
        )
        {
            // Sort and resolve relative triggers
            colorTriggers = SortTriggers(colorTriggers, (t, prev) => {
                t.StartValue = prev.EndValue;
                return t;
            });

            offsetTriggers = SortTriggers(offsetTriggers, (t, prev) => {
                t.StartValue = prev.EndValue.WithW(t.StartValue.w);
                t.EndValue = t.EndValue + t.StartValue;
                return t;
            });

            scaleTriggers = SortTriggers(scaleTriggers, (t, prev) => {
                t.StartValue = prev.EndValue.WithW(t.StartValue.w);                 // Set pivot
                t.EndValue = (t.EndValue + t.StartValue).WithW(t.EndValue.w);       // Set ease
                return t;
            });

            rotateTriggers = SortTriggers(rotateTriggers, (t, prev) => {
                t.StartValue = prev.EndValue.WithV(prev.StartValue.v);                               // Set pivot to prev trigger's
                t.EndValue = new Vector5(t.EndValue, t.EndValue.w + prev.EndValue.w, t.EndValue.v);  // Increase angle
                return t;
            });

            // By using the keys dictionary, the string keys are turned to Color32 before being used as key so that its easier to look up
            foreach (KeyValuePair<string, Color32> pair in keys)
            {
                string triggerKey = pair.Key;
                Color32 colorKey = pair.Value;
                List<ITrigger> triggers = [];

                if (colorTriggers.ContainsKey(colorKey) && colorTriggers[colorKey].Count > 0)
                {
                    allTriggerKeys.Add(triggerKey);
                    //Debug.Log($"LOADING EVENTS {triggerKey} {colorTriggers[colorKey].Count}");
                    TriggerManager.LoadTriggers(triggerKey, colorTriggers[colorKey].ToArray());
                }

                if (offsetTriggers.ContainsKey(colorKey) && offsetTriggers[colorKey].Count > 0)
                {
                    string key = "o" + triggerKey;
                    //Debug.Log($"LOADING EVENTS {key} {offsetTriggers[colorKey].Count}");
                    allTriggerKeys.Add(key);
                    TriggerManager.LoadTriggers(key, offsetTriggers[colorKey].ToArray());
                }

                if (scaleTriggers.ContainsKey(colorKey) && scaleTriggers[colorKey].Count > 0)
                {
                    string key = "s" + triggerKey;
                    //Debug.Log($"LOADING EVENTS {key} {scaleTriggers[colorKey].Count}");
                    allTriggerKeys.Add(key);
                    TriggerManager.LoadTriggers(key, scaleTriggers[colorKey].ToArray());
                }

                if (rotateTriggers.ContainsKey(colorKey) && rotateTriggers[colorKey].Count > 0)
                {
                    string key = "r" + triggerKey;
                    //Debug.Log($"LOADING EVENTS {key} {rotateTriggers[colorKey].Count}");
                    allTriggerKeys.Add(key);
                    TriggerManager.LoadTriggers(key, rotateTriggers[colorKey].ToArray());
                }
            }

            foreach (var item in keys.ToList())
            {
                LUTKeys["o" + item.Key] = item.Value;
                LUTKeys["s" + item.Key] = item.Value;
                LUTKeys["r" + item.Key] = item.Value;
            }

            foreach (string var in setTriggers.Keys)
            {
                allTriggerKeys.Add(var);
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
                return Enum.Parse<Ease>(str.ToUpper().Replace("EASE", ""), true);
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

        [JsonIgnore]
        public bool IsRelative { get; set; }

        public Trigger() 
        {
            IsRelative = false;
        }

        public Trigger(float Time, T StartValue)
        {
            this.Time = Time;
            Duration = 0;
            this.StartValue = StartValue;
            EndValue = StartValue;
            IsRelative = false;
        }

        public Trigger(float Time, float Duration, T StartValue, T EndValue, bool isRelative = false)
        {
            this.Time = Time;
            this.Duration = Duration;
            this.StartValue = StartValue;
            this.EndValue = EndValue;
            this.IsRelative = isRelative;
        }

        public override string ToString()
        {
            return $"Time: {Time} Duration: {Duration} Start: {StartValue} End: {EndValue}";
        }
    }


    internal struct LyricTriggerEmbedData
    {
        public Dictionary<string, Color32> colorKeys = [];
        public Dictionary<string, List<Trigger<Color>>> colorTriggers = [];
        public Dictionary<string, List<Trigger<float>>> setTriggers = [];
        public Dictionary<string, List<Trigger<Vector4>>> offsetTriggers = [];
        public Dictionary<string, List<Trigger<Vector4>>> scaleTriggers = [];
        public Dictionary<string, List<Trigger<Vector5>>> rotateTriggers = [];

        public LyricTriggerEmbedData()
        {
            colorKeys = [];
            colorTriggers = [];
            setTriggers = [];
            offsetTriggers = [];
            scaleTriggers = [];
            rotateTriggers = [];
        }

        public LyricTriggerEmbedData(Dictionary<string, Color32> colorKeys,
            Dictionary<Color32, List<Trigger<Color>>> colorDict,
            Dictionary<string, List<Trigger<float>>> setDict,
            Dictionary<Color32, List<Trigger<Vector4>>> offsetDict,
            Dictionary<Color32, List<Trigger<Vector4>>> scaleDict,
            Dictionary<Color32, List<Trigger<Vector5>>> rotateDict
            )
        {
            this.colorKeys = colorKeys ?? [];
            colorTriggers  = [];
            setTriggers    = setDict   ?? [];
            offsetTriggers = [];
            scaleTriggers  = [];
            rotateTriggers = [];

            foreach(var pair in colorDict)
            {
                colorTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }

            foreach (var pair in offsetDict)
            {
                offsetTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }

            foreach (var pair in scaleDict)
            {
                scaleTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }

            foreach (var pair in rotateDict)
            {
                rotateTriggers.Add(ColorUtility.ToHtmlStringRGBA(pair.Key), pair.Value);
            }
        }
    }
}
