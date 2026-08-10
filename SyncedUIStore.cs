using System.Collections.Generic;
using SpinCore.UI;
using UnityEngine;

namespace ColoredLyrics
{

    internal class SyncedUIStore
    {
        Dictionary<string, KeyValuePair<CustomMultiChoice, int>> multiChoice = [];
        Dictionary<string, KeyValuePair<CustomInputField, string>> textInput = [];


        public void AddUI(string key, CustomMultiChoice uiElement, int defaultVal = 0)
        {
            multiChoice.Add(key, KeyValuePair.Create(uiElement, defaultVal));
        }

        public void AddUI(string key, CustomInputField uiElement, string defaultVal = "")
        {
            textInput.Add(key, KeyValuePair.Create(uiElement, defaultVal));
        }

        public void Sync<T>(string key, T value)
        {
            if (value is float f)
            {
                if (!multiChoice.ContainsKey(key)) return;

                KeyValuePair<CustomMultiChoice, int> pair = multiChoice[key];
                CustomMultiChoice element = pair.Key;
                element.SetCurrentValue((int)(f * 100));
            }
            else if (value is Color c)
            {
                if (!textInput.ContainsKey(key)) return;

                KeyValuePair<CustomInputField, string> pair = textInput[key];
                CustomInputField element = pair.Key;
                element.InputField.text = ColorUtility.ToHtmlStringRGBA(c.ToUnityColor());
            }

            return;
        }

        public void Reset()
        {
            foreach(var v in multiChoice)
            {
                v.Value.Key.SetCurrentValue(v.Value.Value);
            }
            
            foreach (var v in textInput)
            {
                v.Value.Key.InputField.text = v.Value.Value;
            }
        }
    }
}
