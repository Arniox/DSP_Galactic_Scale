﻿using UnityEngine;
using UnityEngine.UI;

namespace GalacticScale
{
    public class ThemeSelectItem : MonoBehaviour
    {
        public Text text;
        public Toggle toggle;
        public string label = "Template";

        // [NonSerialized] 
        public GSTheme theme;
        // Start is called before the first frame update
        void Start()
        {
            text.text = label;
        }

        public void Set(bool value)
        {
            toggle.isOn = value;
        }

        public bool ticked => toggle.isOn;

    }
}