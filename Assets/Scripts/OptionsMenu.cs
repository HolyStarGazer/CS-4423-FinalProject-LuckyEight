using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class OptionsMenu : MonoBehaviour
{
    [Header("UI Components")]
    //public Toggle fullscreenToggle;
    public Toggle vsyncToggle;
    public TMP_Dropdown fullscreenDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public GameObject optionsMenuGroup;

    [Header("Audio")]
    public AudioMixer audioMixer;

    private Resolution[] resolutions;

    void Start()
    {
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string>();
        int currentResIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width && resolutions[i].height == Screen.currentResolution.height)
                currentResIndex = i;
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();

        fullscreenDropdown.ClearOptions();
        fullscreenDropdown.AddOptions(new List<string> {
            "Windowed", "Borderless Window", "Fullscreen"
        });

        switch (Screen.fullScreenMode)
        {
            case FullScreenMode.Windowed:
                fullscreenDropdown.value = 0;
                break;
            case FullScreenMode.FullScreenWindow:
                fullscreenDropdown.value = 1;
                break;
            case FullScreenMode.ExclusiveFullScreen:
                fullscreenDropdown.value = 2;
                break;
            default:
                fullscreenDropdown.value = 0;
                break;
        }

        fullscreenDropdown.RefreshShownValue();
        
        vsyncToggle.isOn = QualitySettings.vSyncCount > 0;

        audioMixer.GetFloat("Master", out float master);
        masterVolumeSlider.value = Mathf.Pow(10f, master / 20f);

        audioMixer.GetFloat("Music", out float music);
        musicVolumeSlider.value = Mathf.Pow(10f, music / 20f);

        audioMixer.GetFloat("SFX", out float sfx);
        sfxVolumeSlider.value = Mathf.Pow(10f, sfx / 20f);
    }

    void Update()
    {
        if (!optionsMenuGroup.activeSelf) return;
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            optionsMenuGroup.SetActive(false);
        }   
    }

    public void SetFullscreen(int index)
    {
        switch (index)
        {
            case 0:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 1:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 2:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
        }
    }

    public void SetVSync()
    {
        QualitySettings.vSyncCount = vsyncToggle.enabled ? 1 : 0;
    }

    public void SetResolution(int index)
    {
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    public void SetMasterVolume()
    {
        float volume = masterVolumeSlider.value;
        audioMixer.SetFloat("Master", LinearToDecibel(volume));
    }

    public void SetMusicVolume()
    {
        float volume = musicVolumeSlider.value;
        audioMixer.SetFloat("Music", LinearToDecibel(volume));
    }

    public void SetSFXVolume()
    {
        float volume = sfxVolumeSlider.value;
        audioMixer.SetFloat("SFX", LinearToDecibel(volume));
    }

    private float LinearToDecibel(float linear)
    {
        float value = Mathf.Log10(linear) * 20f;
        return Mathf.Clamp(value, -80f, 0f);
    }
}
