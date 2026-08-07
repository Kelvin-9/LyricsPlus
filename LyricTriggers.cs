using SpinCore.Triggers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ColoredLyrics
{
    internal class LyricTriggers
    {
        internal static Dictionary<string, Color32> TriggerKeys = [];
        internal static Dictionary<Color32, List<LyricTrigger>> triggers = new();

        internal static void LoadTriggers(Dictionary<string, List<LyricTrigger>> data)
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
            LoadTriggersIntoManager(TriggerKeys, triggers);
        }

        internal static void LoadTriggers(Dictionary<Color32, List<LyricTrigger>> data)
        {
            ClearAll();

            triggers = data;

            RegisterEvents();
            LoadTriggersIntoManager(TriggerKeys, triggers);
        }

        internal static void ClearAll()
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
                string triggerKey = $"LUT{keyIndex}"; // Simplify LUT keys by indexing LUT_MyName, LUT_Name2 => LUT0, LUT1, etc.
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

        private static void LoadTriggersIntoManager(Dictionary<string, Color32> keys, Dictionary<Color32, List<LyricTrigger>> triggers)
        {
            foreach (KeyValuePair<string, Color32> pair in keys)
            {
                string LUTIndexKey = pair.Key;  // LUT0, LUT1 ... LUTn
                TriggerManager.LoadTriggers(LUTIndexKey, triggers[pair.Value].ToArray());
                Debug.Log($"Loaded {triggers[pair.Value].Count} {LUTIndexKey} triggers");
            }
        }
    }

    internal class TriggerFileParser 
    {
        private Dictionary<string, Color32> triggerKeys = [];
        private Dictionary<Color32, List<LyricTrigger>> triggers = [];
        private Dictionary<string, float> setTriggers = [];

        public TriggerFileParser()
        {
            triggerKeys = [];
            triggers.Clear();
        }

        internal bool LoadTriggersFromFile(PlayableTrackData file, out Dictionary<Color32, List<LyricTrigger>>? lyricTriggers)
        {
            lyricTriggers = null;
            (string? directory, string? fileName) = Util.GetDirectoryFromPlayData(file);
            Debug.Log($"Loading triggers from file {fileName}\n\n");
            if (directory == null || fileName == null) 
            {
                return false; 
            }

            string lyricPath = Path.Combine(directory, fileName + ".lyr");
            if (!File.Exists(lyricPath))
            {
                return false;
            }

            List<string> lines = File.ReadAllLines(lyricPath).ToList();

            PreprocessTriggerFile(ref lines);
            for (int i = 0; i < lines.Count; i++)
            {
                ParseLine(lines[i]);
            }

            lyricTriggers = triggers;
            return true;
        }

        static void PreprocessTriggerFile(ref List<string> lines)
        {
            // Remove empty lines and comments
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                lines[i] = lines[i].Trim().ToUpper();
                if (lines[i].Length == 0 || lines[i].StartsWith("#") || lines[i].StartsWith("//"))
                {
                    lines.RemoveAt(i);
                    continue;
                }
            }

            // Expand REPEATs
            for (int i = 0; i < lines.Count; i++)
            {

            }
        }

        /// <summary>
        /// COMMANDS:
        /// - LUT [LUTindex] [color]
        ///     Binds a color to a LUT index
        /// - COLOR [LUTindex] [time] [startColor] [endColor] [duration]
        /// - COLOR [LUTindex] [time] [Color]
        ///     Sets the color of a binded LUT entry
        /// - SET [variable] [number]
        /// 
        /// </summary>
        void ParseLine(string line)
        {
            Debug.Log($"Parsing {line}");

            string[] tokens = line.Split([' ']);
            string command = tokens[0];
            switch (command)
            {
                case "LUT":
                    ParseLUT(tokens);
                    break;
                case "COLOR":
                    ParseCOLOR(tokens);
                    break;
                case "SET":
                    ParseSET(tokens);
                    break;
                default:
                    break;
            }
        }

        /// LUT [LUTindex] [color]
        void ParseLUT(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nLUT [LUTkey] #[color]");
                return;
            }

            string LUTindex = tokens[1];
            UnityEngine.Color? col = ParseColor(tokens[2]);
            if (col == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                return;
            }

            triggerKeys.Add($"LUT_{LUTindex}", (Color32)col);
        }

        /// - COLOR [LUTindex] [time] [startColor] [endColor] [duration]
        /// - COLOR [LUTindex] [time] [Color]
        ///     Sets the color of a binded LUT entry
        void ParseCOLOR(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nCOLOR [LUTindex] [time] #[startColor] #[endColor] [duration]\nCOLOR [LUTindex] [time] #[color]");
                return;
            }

            float? time = ParseFloat(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!triggerKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with [LUT <name> #<color>]");
                return;
            }

            if (tokens.Length >= 6)
            {
                UnityEngine.Color? startColor = ParseColor(tokens[3]);
                UnityEngine.Color? endColor = ParseColor(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                if ( startColor == null || endColor == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    return;
                }


                Color32 LUTfrom = triggerKeys[LUTkey];
                if (!triggers.ContainsKey(LUTfrom))
                {
                    triggers[LUTfrom] = new();
                }

                triggers[LUTfrom].Add(new LyricTrigger(time.Value, duration.Value, startColor.Value.Convert(), endColor.Value.Convert()));

            }
            else if (tokens.Length >= 4)
            {
                UnityEngine.Color? color = ParseColor(tokens[3]);
                if (LUTindex == null || color == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                    return;
                }

                Color32 LUTfrom = triggerKeys[LUTkey];
                if (!triggers.ContainsKey(LUTfrom))
                {
                    triggers[LUTfrom] = new();
                }

                triggers[LUTfrom].Add(new LyricTrigger(time.Value, 0, color.Value.Convert(), color.Value.Convert()));
            }

            return;
        }

        /// - SET [variable] [number]
        void ParseSET(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nSET [variable] [number]");
                return;
            }

            string variable = tokens[1];
            float? value = ParseFloat(tokens[2]);
            if (value == null)
            {
                Debug.LogError($"Could not parse value from command {string.Join(' ', tokens)}");
                return;
            }

            
        }

        static float? ParseFloat(string num)
        {
            try
            {
                return float.Parse(num, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        static int? ParseInt(string num) 
        {
            try
            {
                return int.Parse(num, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
        static UnityEngine.Color? ParseColor(string color)
        {
            if (ColorUtility.TryParseHtmlString(color, out var colorValue))
            {
                return colorValue;
            }

            return null;
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
