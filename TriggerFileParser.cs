using DG.Tweening;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LyricPlus
{
    internal class TriggerFileParser
    {
        private readonly Dictionary<string, Color32> colorKeys = [];
        private readonly Dictionary<Color32, List<Trigger<Color>>> colorTriggers = [];
        private readonly Dictionary<string, List<Trigger<float>>> setTriggers = [];
        private readonly Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers = [];
        private readonly Dictionary<Color32, List<Trigger<Vector5>>> rotateTriggers = [];

        readonly List<string[]> lines = [];

        readonly Dictionary<string, int> functionPositions = [];
        readonly List<float> functionTimeOffset = [];

        public TriggerFileParser()
        {
            colorKeys = [];
            colorTriggers.Clear();
        }

        internal bool LoadTriggersFromFile(PlayableTrackData file,
            out Dictionary<string, Color32> colorKeys,
            out Dictionary<Color32, List<Trigger<Color>>> colorTriggers,
            out Dictionary<string, List<Trigger<float>>> setTriggers,
            out Dictionary<Color32, List<Trigger<Vector4>>> offsetTriggers,
            out Dictionary<Color32, List<Trigger<Vector5>>> rotateTriggers
            )
        {
            colorTriggers = [];
            setTriggers = [];
            colorKeys = [];
            offsetTriggers = [];
            rotateTriggers = [];
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

            var fullLines = File.ReadAllLines(lyricPath).ToList();
            PreprocessLines(fullLines);

            for (int pc = 0; pc < lines.Count; pc++)
            {
                pc = ParseLine(lines[pc], pc);
            }


            colorTriggers = this.colorTriggers;
            setTriggers = this.setTriggers;
            colorKeys = this.colorKeys;
            offsetTriggers = this.offsetTriggers;
            rotateTriggers = this.rotateTriggers;
            return true;
        }

        void PreprocessLines(List<string> fullLines)
        {
            // Remove comments, uppercase strings and split into tokens
            for (int pc = 0; pc < fullLines.Count; pc++)
            {
                fullLines[pc] = fullLines[pc].Trim().ToUpper();
                if (fullLines[pc].Length == 0 || fullLines[pc].StartsWith("#") || fullLines[pc].StartsWith("//"))
                {
                    lines.Add([]); // Empty line
                    continue;
                }

                lines.Add(fullLines[pc].Split(' ', System.StringSplitOptions.RemoveEmptyEntries));
            }
        }

        int ParseLine(string[] tokens, int lineNum)
        {
            if (tokens.Length <= 0)
            {
                return lineNum;
            }

            //Debug.Log($"Parsing {string.Join(' ', tokens)}");
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
                case "RELATIVEOFFSET":
                    ParseRelativeOFFSET(tokens);
                    break;
                case "ROTATE":
                    ParseROTATE(tokens);
                    break;
                case "RELATIVEROTATE":
                    ParseRelativeROTATE(tokens);
                    break;
                case "REPEAT":
                    ParseREPEAT(tokens, lineNum);
                    break;
                case "ENDREPEAT":
                    return ParseENDREPEAT(tokens, lineNum);
                case "FUNCTION":
                    return ParseFUNCTION(tokens, lineNum);
                case "ENDFUNCTION":
                    return ParseEND(tokens, lineNum);
                case "CALL":
                    return ParseCALL(tokens, lineNum);
                default:
                    break;
            }

            return lineNum;
        }

        /// LUT [LUTindex] [color]
        static readonly string lutUsage = "LUT \"LUTentry\" \"#color\"";
        void ParseLUT(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {lutUsage}");
                return;
            }

            string LUTkey = $"LUT_{tokens[1]}";
            UnityEngine.Color? col = ParseColor(tokens[2]);
            if (col == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {lutUsage}");
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
                }
            }

            colorKeys[LUTkey] = (Color32)col;
        }

        /// - COLOR [LUTindex] [time] [StartValue] <[EndValue] [duration]>
        ///     Sets the color of a binded LUT entry
        static readonly string colorUsage = "COLOR \"LUTentry\" [time] \"#startColor\" <\"#endColor\" [duration]>";
        void ParseCOLOR(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {colorUsage}");
                return;
            }

            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {colorUsage}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with {lutUsage}");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!colorTriggers.ContainsKey(LUTfrom))
            {
                colorTriggers[LUTfrom] = [];
            }

            if (tokens.Length >= 6)
            {
                UnityEngine.Color? StartValue = ParseColor(tokens[3]);
                UnityEngine.Color? EndValue = ParseColor(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    Debug.LogError($"Usage:\n   {colorUsage}");
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
                    Debug.LogError($"Usage:\n   {colorUsage}");
                    return;
                }

                colorTriggers[LUTfrom].Add(new Trigger<Color>(time.Value, 0, color.Value.Convert(), color.Value.Convert()));
            }
        }

        /// - SET "variable" [time] [startValue] <[endValue] [duration]>
        static readonly string setUsage = "SET \"variable\" [time] [startValue] <[endValue] [duration]>";
        void ParseSET(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {setUsage}");
                return;
            }

            string variableName = tokens[1];
            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {setUsage}");
                return;
            }

            if (!setTriggers.ContainsKey(variableName))
            {
                setTriggers[variableName] = [];
            }

            if (tokens.Length >= 6)
            {
                float? StartValue = ParseFloat(tokens[3]);
                float? EndValue = ParseFloat(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    Debug.LogError($"Usage:\n   {setUsage}");
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
                    Debug.LogError($"Usage:\n   {setUsage}");
                    return;
                }

                setTriggers[variableName].Add(new Trigger<float>(time.Value, StartValue.Value));
            }
        }

        /// - OFFSET [LUTindex] [time] [startOffset] <[endOffset] [duration] "easing">
        ///     Offsets the position of all text with the given [LUTindex]
        ///     Give the offsets in the form of x,y,z or (x,y,z)
        static readonly string offsetUsage = "OFFSET \"LUTentry\" [time] (startOffset) <(endOffset) [duration]> <\"easing\">";
        void ParseOFFSET(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {offsetUsage}");
                return;
            }

            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {offsetUsage}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with {lutUsage}");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!offsetTriggers.ContainsKey(LUTfrom))
            {
                offsetTriggers[LUTfrom] = [];
            }

            if (tokens.Length >= 6)  // Optional 7th parameter for easing, defaults to LINEAR
            {
                Vector3? StartValue = ParseVector3(tokens[3]);
                Vector3? EndValue = ParseVector3(tokens[4]);
                float? duration = ParseFloat(tokens[5]);
                int ease = tokens.Length > 6 ? (int)Easing.ParseEase(tokens[6]) : 0;
                if (StartValue == null || EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    Debug.LogError($"Usage:\n   {offsetUsage}");
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
                    Debug.LogError($"Usage:\n   {offsetUsage}");
                    return;
                }

                Vector4 value = ((Vector4)StartValue.Value).WithW(0);
                offsetTriggers[LUTfrom].Add(new Trigger<Vector4>(time.Value, value));
            }
        }


        /// - RELATIVEOFFSET "LUTindex" [time] (offset) <[duration]> <"easing">
        static readonly string relativeoffsetUsage = "RelativeOFFSET \"LUTentry\" [time] (offset) <[duration]> <\"easing\">";
        void ParseRelativeOFFSET(string[] tokens)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {relativeoffsetUsage}");
                return;
            }

            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {relativeoffsetUsage}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with {lutUsage}");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!offsetTriggers.ContainsKey(LUTfrom))
            {
                offsetTriggers[LUTfrom] = [];
            }

            if (tokens.Length >= 5)  // Optional 6th parameter for easing, defaults to LINEAR
            {
                Vector3? EndValue = ParseVector3(tokens[3]);
                float? duration = ParseFloat(tokens[4]);
                int ease = tokens.Length > 5 ? (int)Easing.ParseEase(tokens[5]) : 0;
                if (EndValue == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    Debug.LogError($"Usage:\n   {relativeoffsetUsage}");
                    return;
                }

                offsetTriggers[LUTfrom].Add(new Trigger<Vector4>(time.Value, duration.Value, new Vector4().WithW(ease), EndValue.Value, isRelative: true));

            }
            else if (tokens.Length >= 4)
            {
                Vector3? EndValue = ParseVector3(tokens[3]);
                if (LUTindex == null || EndValue == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                    Debug.LogError($"Usage:\n   {relativeoffsetUsage}");
                    return;
                }

                Vector4 value = ((Vector4)EndValue.Value).WithW(0);
                offsetTriggers[LUTfrom].Add(new Trigger<Vector4>(time.Value, 0, new Vector4(), value, isRelative: true));
            }
        }

        /// - ROTATE [LUTindex] [time] (axis) [degrees] [pivotIndex] <(endAxis) [endDegrees] [duration]> <"easing">
        ///     Rotates all characters with [LUTindex] at [time] around (axis) and the character [pivotIndex] by [degrees]
        static readonly string rotateUsage = "ROTATE \"LUTentry\" [time] (axis) [degrees] [pivotIndex] <(endAxis) [endDegrees] [duration]> <\"easing\">";
        void ParseROTATE(string[] tokens)
        {
            if (tokens.Length < 6)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {rotateUsage}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with {lutUsage}");
                return;
            }

            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {rotateUsage}");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!rotateTriggers.ContainsKey(LUTfrom))
            {
                rotateTriggers[LUTfrom] = [];
            }

            Vector3? StartAxis = ParseVector3(tokens[3]);
            float? degrees = ParseFloat(tokens[4]);
            int? pivotInd = ParseInt(tokens[5]);
            if (StartAxis == null || degrees == null || pivotInd == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                Debug.LogError($"Usage:\n   {rotateUsage}");
                return;
            }

            if (tokens.Length >= 9)  // Optional 10th parameter for easing, defaults to LINEAR
            {
                Vector3? EndAxis = ParseVector3(tokens[6]);
                float? endDegrees = ParseFloat(tokens[7]);
                float? duration = ParseFloat(tokens[8]);
                int ease = tokens.Length > 9 ? (int)Easing.ParseEase(tokens[9]) : 0;
                if (EndAxis == null || endDegrees == null || duration == null)
                {
                    Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                    Debug.LogError($"Usage:\n   {rotateUsage}");
                    return;
                }

                rotateTriggers[LUTfrom].Add(new Trigger<Vector5>(
                    time.Value,
                    duration.Value,
                    new Vector5(StartAxis.Value, degrees.Value, pivotInd.Value),              // Start value packs pivot
                    new Vector5(EndAxis.Value, endDegrees.Value, ease)                        // End value packs ease
                ));

            }
            else if (tokens.Length >= 6)
            {
                rotateTriggers[LUTfrom].Add(new Trigger<Vector5>(time.Value, new Vector5(StartAxis.Value, degrees.Value, pivotInd.Value)));
            }
        }

        /// - RELATIVEROTATE [LUTindex] [time] (endAxis) [endDegrees] <[duration]> <"easing">
        ///     Continues the rotation from previous trigger, effectively using the previous trigger's end values as this trigger's start value
        static readonly string relativeRotateUsage = "RelativeROTATE \"LUTentry\" [time] (endAxis) [degreesIncrease] <[duration]> <\"easing\">";
        void ParseRelativeROTATE(string[] tokens)
        {
            if (tokens.Length < 5)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {relativeRotateUsage}");
                return;
            }

            string LUTindex = tokens[1];
            string LUTkey = $"LUT_{LUTindex}";
            if (!colorKeys.ContainsKey(LUTkey))
            {
                Debug.LogError($"LUT key '{LUTkey}' was used before being declared!\nDeclare it at the start of the file with {lutUsage}");
                return;
            }

            float? time = ParseTime(tokens[2]);
            if (time == null)
            {
                Debug.LogError($"Could not parse time variable in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n   {relativeRotateUsage}");
                return;
            }

            Color32 LUTfrom = colorKeys[LUTkey];
            if (!rotateTriggers.ContainsKey(LUTfrom))
            {
                rotateTriggers[LUTfrom] = [];
            }

            Vector3? EndAxis = ParseVector3(tokens[3]);
            float? endDegrees = ParseFloat(tokens[4]);
            float duration = tokens.Length > 5 ? ParseFloat(tokens[5]) ?? 0 : 0;
            int ease = (int)(tokens.Length > 6 ? Easing.ParseEase(tokens[6]) : Ease.Linear);
            if (EndAxis == null || endDegrees == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}!");
                Debug.LogError($"Usage:\n   {relativeRotateUsage}");
                return;
            }

            rotateTriggers[LUTfrom].Add(new Trigger<Vector5>(
                time.Value,
                duration,
                new Vector5(EndAxis.Value, 0, -1),                                        // RELATIVEROTATE triggers default to rotating on itself (with -1 pivot index)
                new Vector5(EndAxis.Value, endDegrees.Value, ease),                       // End value packs ease
                isRelative: true
            ));
        }


        int repeatDepth = 0;
        readonly List<int> repeatCounts = [];
        readonly List<int> currentRepeatIterations = [];
        readonly List<int> repeatLineBeginnings = [];
        readonly List<float> repeatIntervals = [];

        readonly Stack<int> callstack = [];
        readonly Stack<int> returnstack = [];

        /// - REPEAT [numrepeats] interval [interval]
        static readonly string repeatUsage = "REPEAT [numRepeats] interval [interval]\n...commands...\nENDREPEAT";
        void ParseREPEAT(string[] tokens, int lineNum)
        {
            if (tokens.Length < 4)
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n{repeatUsage}");
                return;
            }

            int? repeats = ParseInt(tokens[1]);
            float? interval = ParseFloat(tokens[3]);
            if (interval == null || repeats == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\n{repeatUsage}");
                return;
            }

            repeatDepth++;
            repeatCounts.Add(repeats.Value);
            repeatIntervals.Add(interval.Value);
            repeatLineBeginnings.Add(lineNum);
            currentRepeatIterations.Add(0);
        }

        int ParseENDREPEAT(string[] tokens, int lineNum)
        {
            if (repeatDepth <= 0)
            {
                Debug.LogError("Unexpected ENDREPEAT");
                Debug.LogError($"Usage:\n{repeatUsage}");
                return lineNum;
            }

            // Keep looping
            if (++currentRepeatIterations[repeatDepth - 1] != repeatCounts[repeatDepth - 1])
            {
                lineNum = repeatLineBeginnings[repeatDepth - 1];
                return lineNum;
            }

            // End loop
            repeatDepth--;
            repeatCounts.RemoveAt(repeatDepth);
            currentRepeatIterations.RemoveAt(repeatDepth);
            repeatIntervals.RemoveAt(repeatDepth);
            repeatLineBeginnings.RemoveAt(repeatDepth);

            return lineNum;
        }

        // FUNCTION [name]
        // ENDFUNCTION
        static readonly string functionUsage = "FUNCTION \"name\"\n...commands...\nENDFUNCTION";
        int ParseFUNCTION(string[] tokens, int lineNum)
        {
            if (tokens.Length < 2)
            {
                Debug.LogError($"FUNCTION {tokens[1]} was does not have name defined!");
                Debug.LogError($"Usage:\n{functionUsage}");
            }

            int functionPos = lineNum;

            // Move pointer forward until END
            int depth = 0;
            int endLine = lineNum + 1;
            for (int i = lineNum; i < lines.Count; i++)
            {
                if (lines[i].Length == 0) continue;

                switch (lines[i][0]) 
                {
                    case "FUNCTION":
                        depth++;
                        break;
                    case "ENDFUNCTION":
                        depth--;
                        break;
                    default:
                        break;
                }

                if (depth <= 0)
                {
                    endLine = i;
                    break;
                }
            }

            if (depth > 0)
            {
                Debug.LogError($"FUNCTION {tokens[1]} was does not have ENDFUNCTION defined!\nUsage:\nFUNCTION [name]\n...commands...\nENDFUNCTION");
                return lineNum;
            }

            functionPositions[tokens[1]] = lineNum;
            lineNum = endLine;

            return lineNum;
        }

        int ParseEND(string[] tokens, int lineNum)
        {
            // Return to call stack original position
            if (returnstack.TryPop(out int returnPos))
            {
                callstack.Pop();
                functionTimeOffset.RemoveAt(functionTimeOffset.Count - 1);
                return returnPos;
            }

            return lineNum;
        }

        // CALL [functionName] [time]
        int ParseCALL(string[] tokens, int lineNum)
        {
            if (tokens.Length < 3) 
            {
                Debug.LogError($"Not enough tokens in command:\n{string.Join(' ', tokens)}");
                Debug.LogError($"Usage:\nCALL [functionName] [time]");
                return lineNum;
            }

            if (!functionPositions.ContainsKey(tokens[1]))
            {
                Debug.LogError($"Function {tokens[1]} does not exist!");
                return lineNum;
            }

            int functionPos = functionPositions[tokens[1]];

            // Check for infinite recursion
            foreach(int lineNo in callstack)
            {
                if (lineNo != functionPos) continue;

                Debug.LogError($"Infinite recursion detected! Function call {tokens[1]} aborted.\n Avoid making infinite loops with function calls.");
                return lineNum;
            }

            float? time = ParseFloat(tokens[2]);  //! Important to not use ParseTime here so the time offset stack is separate from the repeat offsets
            if (time == null)
            {
                Debug.LogError($"Could not parse tokens from line:\n{string.Join(' ', tokens)}");
                return lineNum;
            }

            returnstack.Push(lineNum);
            callstack.Push(functionPos);
            functionTimeOffset.Add(time.Value);
            lineNum = functionPos;

            return lineNum;
        }

        float? ParseTime(string time)
        {
            float? t = ParseFloat(time);
            if (t == null)
            {
                return null;
            }

            float timeOffset = functionTimeOffset.Sum();

            if (repeatDepth <= 0)
                return t + timeOffset;

            float repeatTimeOffset = 0;
            for (int i = 0; i < repeatDepth; i++)
                repeatTimeOffset += repeatIntervals[i] * currentRepeatIterations[i];

            return t + repeatTimeOffset + timeOffset;
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
