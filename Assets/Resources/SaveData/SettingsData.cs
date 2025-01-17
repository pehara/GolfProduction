using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SettingsData
{
    //********* Settings Data *********//

    // Accessibility
    public int language; // 0 - English
    public bool oneHandMode;

    // Game
    public float cameraSensitivity;
    public float volume;

    public SettingsData() 
    {
        // Initialize Game Settings Data
        this.language = 0;
        this.oneHandMode = false;

        this.cameraSensitivity = 5;
        this.volume = 0;
    }

    public SettingsData Copy()
    {
        return new SettingsData
        {
            language = this.language,
            oneHandMode = this.oneHandMode,
            cameraSensitivity = this.cameraSensitivity,
            volume = this.volume,
        };
    }
}
