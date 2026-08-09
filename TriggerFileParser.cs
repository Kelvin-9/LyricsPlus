using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ColoredLyrics
{
    internal class TriggerFileParser
    {
        private Dictionary<string, Color32> colorKeys = [];
        private Dictionary<Color32, List<Trigger<Color>>> colorTriggers = [];
        private Dictionary<string, List<Trigger<float>>> setTriggers = [];
        private Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers = [];

        public TriggerFileParser()
        {
            colorKeys = [];
            colorTriggers.Clear();
        }

        internal bool LoadTriggersFromFile(PlayableTrackData file,
            out Dictionary<string, Color32>? colorKeys,
            out Dictionary<Color32, List<Trigger<Color>>>? colorTriggers,
            out Dictionary<string, List<Trigger<float>>>? setTriggers,
            out Dictionary<Color32, List<Trigger<Vector4>>>? offsetTriggers
            )
        {
            colorTriggers = null;
            setTriggers = null;
            colorKeys = null;
            offsetTriggers = null;
            (string? directory, string? fileName) = Util.GetDirectoryFromPlayData(file);
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


            colorTriggers = this.colorTriggers;
            setTriggers = this.setTriggers;
            colorKeys = this.colorKeys;
            offsetTriggers = this.offsetTriggers;
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
                // TODO: IMPLEMENT REPEAT TRIGGERS
            }
        }

        /// <summary>
        /// COMMANDS:
        /// - LUT [LUTindex] [color]
        ///     Binds a color to a LUT index
        /// - COLOR [LUTindex] [time] [startValue] [endValue] [duration]
        /// - COLOR [LUTindex] [time] [color]
        ///     Sets the color of a binded LUT entry
        /// - SET [variable] [time] [startValue] [endValue] [duration]
        /// - SET [variable] [time] [value]
        ///     Sets a variable to a decimal value
        /// 
        /// - OFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration] [easing]>
        ///     Offsets the position of all text with the given [LUTindex]
        ///     Give the offsets in the form of x,y,z or (x,y,z)
        /// </summary>
        void ParseLine(string line)
        {
            //Debug.Log($"Parsing {line}");

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
                case "OFFSET":
                    ParseOFFSET(tokens);
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

            string LUTkey = $"LUT_{tokens[1]}";
            UnityEngine.Color? col = ParseColor(tokens[2]);
            if (col == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                return;
            }

            foreach (var pair in colorKeys)
            {
                if (pair.Value.Equal((Color32)col, ignoreAlpha: true))
                {
                    Debug.LogError($"LUT {tokens[1]} uses the same color key as LUT {pair.Key}!");
                    return;
                }

                if (pair.Key == LUTkey)
                {
                    Debug.LogWarning($"LUT {tokens[1]} is declared multiple times");
                    break;
                }
            }

            colorKeys[LUTkey] = (Color32)col;
        }

        /// - COLOR [LUTindex] [time] [StartValue] [EndValue] [duration]
        /// - COLOR [LUTindex] [time] [Color]
        ///     Sets the color of a binded LUT entry
        void ParseCOLOR(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nCOLOR [LUTindex] [time] #[StartValue] #[EndValue] [duration]\nCOLOR [LUTindex] [time] #[color]");
                return;
            }

            float? time = ParseFloat(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with [LUT <name> #<color>]");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!colorTriggers.ContainsKey(LUTfrom))
            {
                colorTriggers[LUTfrom] = new();
            }

            if (tokens.Length >= 6)
            {
                UnityEngine.Color? StartValue = ParseColor(tokens[3]);
                UnityEngine.Color? EndValue = ParseColor(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    return;
                }

                colorTriggers[LUTfrom].Add(new Trigger<Color>(time.Value, duration.Value, StartValue.Value.Convert(), EndValue.Value.Convert()));

            }
            else if (tokens.Length >= 4)
            {
                UnityEngine.Color? color = ParseColor(tokens[3]);
                if (LUTindex == null || color == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                    return;
                }

                colorTriggers[LUTfrom].Add(new Trigger<Color>(time.Value, 0, color.Value.Convert(), color.Value.Convert()));
            }
        }

        /// - SET [variable] [time] [startValue] [endValue] [duration]
        /// - SET [variable] [time] [value]
        void ParseSET(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nCOLOR [LUTindex] [time] #[StartValue] #[EndValue] [duration]\nCOLOR [LUTindex] [time] #[color]");
                return;
            }

            string variableName = tokens[1];
            float? time = ParseFloat(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                return;
            }

            if (!setTriggers.ContainsKey(variableName))
            {
                setTriggers[variableName] = new();
            }

            if (tokens.Length >= 6)
            {
                float? StartValue = ParseFloat(tokens[3]);
                float? EndValue = ParseFloat(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    return;
                }

                setTriggers[variableName].Add(new Trigger<float>(time.Value, duration.Value, StartValue.Value, EndValue.Value));

            }
            else if (tokens.Length >= 4)
            {
                float? StartValue = ParseFloat(tokens[3]);
                if (StartValue == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                    return;
                }

                setTriggers[variableName].Add(new Trigger<float>(time.Value, StartValue.Value));
            }
        }

        /// - OFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration] [easing]>
        ///     Offsets the position of all text with the given [LUTindex]
        ///     Give the offsets in the form of x,y,z or (x,y,z)
        void ParseOFFSET(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nOFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration] [easing]>\ne.g. OFFSET Color1 10.02 (0,0,0) (0,0,10) 2.1 EASEIN");
                return;
            }

            float? time = ParseFloat(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with [LUT <name> #<color>]");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!offsetTriggers.ContainsKey(LUTfrom))
            {
                offsetTriggers[LUTfrom] = new();
            }

            if (tokens.Length >= 6)  // Optional 7th parameter for easing, defaults to LINEAR
            {
                Vector3? StartValue = ParseVector3(tokens[3]);
                Vector3? EndValue = ParseVector3(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                float ease = tokens.Length > 6 ? (int)Easing.ParseEase(tokens[6]) : 0;
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    return;
                }

                offsetTriggers[LUTfrom].Add(new Trigger<Vector4>(time.Value, duration.Value, ((Vector4)StartValue.Value).WithW(ease), EndValue.Value));

            }
            else if (tokens.Length >= 4)
            {
                Vector3? StartValue = ParseVector3(tokens[3]);
                if (LUTindex == null || StartValue == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                    return;
                }

                Vector4 value = ((Vector4)StartValue.Value).WithW(0);
                offsetTriggers[LUTfrom].Add(new Trigger<Vector4>(time.Value, 0, value, value));
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

        static Vector3? ParseVector3(string vector)
        {
            vector = vector.Trim('(', ')', '[', ']', '{', '}');
            string[] nums = vector.Split(',');
            if (nums.Length != 3) return null;

            return new Vector3(ParseFloat(nums[0]) ?? 0, ParseFloat(nums[1]) ?? 0, ParseFloat(nums[2]) ?? 0);
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
}
