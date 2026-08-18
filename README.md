## Features
-  Working color tags
    -  To get color tags working, you need to first embed the shader parameters in the quick mod menu. This is for backwards compatibility so that unmodded charts look the same as before.
-  Tweakable lyric shader parameters such as Tint, Dilate, Outline Width and Outline Color
-  Trigger system to change text position, color, rotation and size during the chart
    -  Target triggers to any block of text using their color tag as "key". Details on this in the trigger section
    -  Animation curves that allows for smooth motion and rotation
    -  Macros such as REPEAT and FUNCTION to save some typing work
    -  Documentation included when the trigger file is created via quick mod menu
-  Removed lyric input textbox line limit
-  Added fallback fonts for thousands of emojis and Unicode characters from NotoSansSymbols, NotoSansSymbols2 and NotoColorEmoji
-  Tweakable lyric animation parameters like fade in, fade out, unspoken word opacity, text slant (as they are spoken) and textbox size. These are also modifiable via triggers

### Miscellaneous
-  Preloads fonts so that glyphs from fallback fonts don't cause lag spikes
-  Default lyric settings to change how lyrics look on all charts that aren't modded
-  Truncated lyric timeline text so that big text doesn't cover the whole screen in chart editor

## Triggers
Because I am lazy I will paste the documentation I have in the trigger file template here again.

### Notation
**<...> = optional  
[...] = number  
"..." = name  
"#..." = color  
(...) = vector**  
<i>All variables are separated by space so do NOT add spaces between vector components like (0, 0, 0). Write it like 0,0,0 or (0,0,0) instead.
Extra spaces between variables are fine. All commands and LUT keys are case insensitive.</i>

### COMMANDS
<b>LUT "LUTentry" "#color"</b>
- Creates a lookup table entry named "LUTentry" for color [#color]
- It is recommended to use colors that you are not going to use but will still look good for non-mod users, like reserving #01FFFF, #02FFFF etc to look up table keys
- Note that the lyric editor will automatically replace any instance of "LUTentry" to the respective "#color"
<i>
LUT color1 #01FFFF<br><br>
</i>

<b>COLOR "LUTentry" [time] "#startColor" <"#endColor" [duration]></b>  
- Replaces all text's color with color tag corresponding to the LUT entry's color to the new color "#startColor" and transitions to "#endColor" over [duration]
- Make sure that the LUT command is called before using COLOR for that entry
<i>
LUT color1 #01FFFF<br>
COLOR color1 10.0 #FF0000 #00FF00 1.2<br><br>
</i>
  
<b>SET "variable" [time] [startValue] <[endValue] [duration]></b>
- Smoothly curves "variable" at [time] from [startValue] to [endValue] over [duration]
- Variable names: FADEIN, FADEOUT, UNSPOKENALPHA, SLANT, TEXTBOXSIZE
<i>
SET FADEIN 10.2 1.0 0 5<br><br>
</i>

<b>OFFSET "LUTentry" [time] (startOffset) <(endOffset) [duration]> <"easing"></b>
- Offsets the position of all text with the given "LUTentry" from [startOffset] to [endOffset] over duration
- Give the offsets in the form of x,y,z or (x,y,z)
- Optional "easing" parameter can be set to any easing found in https://easings.net/
<i>
OFFSET color1 30.5 (0,0,0) (0,4,-10) 2 InOutElastic<br><br>
</i>

<b>RELATIVEOFFSET "LUTentry" [time] (offset) <[duration]> <"easing"></b>
- Increases the offset based on previous OFFSET/RELATIVEOFFSET trigger on this "LUTentry"
- For example, offsetting by (0,1,0) then doing RELATIVEOFFSET by (0,1,0) will make the text go to (0,2,0)
<i>
OFFSET color1 30.5 (0,0,0)<br>
RELATIVEOFFSET color1 31 (0,1,0)<br><br>
</i>

<b>SCALE [LUTindex] [time] (startScale) [pivotIndex] <(endScale) [duration]> <"easing"></b>
- Scales [LUTindex] around [pivotIndex] by (startScale)
<i>
SCALE color1 10.0 (1,1,1) 0 (1,2,1) 2.0 InOutQuint<br><br>
</i>

<b>ROTATE "LUTentry" [time] (axis) [degrees] [pivotIndex] <(endAxis) [endDegrees] [duration]> <"easing"></b>
- Rotates around (axis) and character index [pivotIndex] by [degrees], moving the axis towards (endAxis) and changing the degrees to [endDegrees] over [duration]
- Pivot index refers to the index of the character within the phrase, starting at 1 for the 1st character. (Use 0 to rotate in place)
- For example, "@heli<color=color1>copter" with trigger "ROTATE color1 10 (0,0,1) 10 2" would rotate "copter" by 10 degrees around the 2nd character of "helicopter" which would be the letter "e".
<i>
ROTATE color1 10.2 (0,0,1) 0 0 (0,0,1) 20 2 InOutQuint<br><br>
</i>

<b>RELATIVEROTATE "LUTentry" [time] (endAxis) [degreesIncrease] <[duration]> <"easing"></b>
- Rotates around the previous trigger's end axis and pivot, increasing the angle by [degreesIncrease] and moving axis towards (endAxis) over [duration]
- Makes writing consecutive rotations easier and compatible with REPEAT loops
<i>
ROTATE color1 9 (0,0,1) 0<br>
RELATIVEROTATE color1 10.2 (0,0,1) 20 2 OutSine<br>
RELATIVEROTATE color1 11.2 (0,0,1) 20 2 OutSine<br><br>
</i>

### MACROS
<b>REPEAT [numRepeats] interval [timeInterval]  
...commands go here...  
ENDREPEAT</b>
<br><br>

<b>FUNCTION [functionName]  
...commands go here...  
ENDFUNCTION</b>
<br><br>

<b>CALL [functionName] [time]</b>
- Note that infinitly recursive function calls will be ignored
