﻿using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RocketSoundEnhancement
{
    //[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Settings : MonoBehaviour
    {
        public static Settings Instance { get; private set; } = new Settings();

        public string ModPath = "GameData/RocketSoundEnhancement/";
        public string SettingsName = "RSE_SETTINGS";

        public Dictionary<string, CollidingObject> CollisionData = new Dictionary<string, CollidingObject>();

        public bool DisableStagingSound = false;
        public bool MuteStockAeroSounds = false;

        public float ExteriorVolume = 1;
        public float InteriorVolume = 1;

        private List<ConfigNode> _shipEffectsNodes = new List<ConfigNode>();
        public List<ConfigNode> ShipEffectsNodes()
        {
            if(_shipEffectsNodes.Count == 0) {
                foreach(var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                    _shipEffectsNodes.AddRange(configNode.GetNodes());
                }
            }

            return _shipEffectsNodes;
        }

        ConfigNode _settings;
        public void Load()
        {
            CollisionData.Clear();
            AudioLimiter.Presets.Clear();
            AudioMuffler.Presets.Clear();

            foreach(var configNode in GameDatabase.Instance.GetConfigNodes("SHIPEFFECTS_SOUNDLAYERS")) {
                if(configNode.HasValue("MuteStockAeroSounds")) {
                    bool.TryParse(configNode.GetValue("MuteStockAeroSounds"), out MuteStockAeroSounds);
                }

                if(configNode.HasValue("nextStageClip")) {
                    StageManager.Instance.nextStageClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("nextStageClip"));
                }
                if(configNode.HasValue("cannotSeparateClip")) {
                    StageManager.Instance.cannotSeparateClip = GameDatabase.Instance.GetAudioClip(configNode.GetValue("cannotSeparateClip"));
                }
            }

            _settings = ConfigNode.Load(ModPath + "Settings.cfg");

            if(_settings == null) {
                Debug.LogError("[RSE]: Settings.cfg not found! using internal settings");
                _settings = new ConfigNode();
                _settings.AddNode(SettingsName);
            }

            ConfigNode settingsNode = _settings.GetNode(SettingsName);
            if(settingsNode.HasValue("ExteriorVolume")) {
                if(!float.TryParse(settingsNode.GetValue("ExteriorVolume"), out ExteriorVolume)) {
                    ExteriorVolume = 1;
                }
            }

            if(settingsNode.HasValue("InteriorVolume")) {
                if(!float.TryParse(settingsNode.GetValue("InteriorVolume"), out InteriorVolume)) {
                    InteriorVolume = 1;
                }
            }

            if(settingsNode.HasValue("DisableStagingSound")) {
                if(!bool.TryParse(settingsNode.GetValue("DisableStagingSound"), out DisableStagingSound)) {
                    DisableStagingSound = false; 
                }
            } else {
                settingsNode.AddValue("DisableStagingSound", DisableStagingSound);
            }

            if(settingsNode.HasNode("Colliders")) {
                var colNode = settingsNode.GetNode("Colliders");
                foreach(ConfigNode.Value node in colNode.values) {
                    CollidingObject colDataType = (CollidingObject)Enum.Parse(typeof(CollidingObject), node.value, true);
                    if(!CollisionData.ContainsKey(node.name)) {
                        CollisionData.Add(node.name, colDataType);
                    } else {
                        CollisionData[node.name] = colDataType;
                    }
                }
            } else {
                //Add the default Collider Collection
                var defaultColNode = settingsNode.AddNode("Colliders");
                defaultColNode.AddValue("default", CollidingObject.Concrete);
                CollisionData.Add("default", CollidingObject.Concrete);

                //runway_lev1_v2 = CollidingObject.Dirt
            }

            #region AUDIOLIMITER
            if(settingsNode.HasNode("AUDIOLIMITER")) {
                var audioLimiterNode = settingsNode.GetNode("AUDIOLIMITER");

                if(!audioLimiterNode.HasValue("EnableLimiter")) {
                    audioLimiterNode.AddValue("EnableLimiter", true);
                }

                bool.TryParse(audioLimiterNode.GetValue("EnableLimiter"), out AudioLimiter.EnableLimiter);

                if(audioLimiterNode.HasValue("Preset")) {
                    AudioLimiter.Preset = audioLimiterNode.GetValue("Preset");
                }

                foreach(var presetNode in audioLimiterNode.GetNodes()) {
                    string presetName = presetNode.name;
                    AudioLimiterPreset limiterPreset = new AudioLimiterPreset();

                    if(AudioLimiter.Preset == string.Empty) {
                        AudioLimiter.Preset = presetName;
                    }

                    if(!float.TryParse(presetNode.GetValue("Threshold"), out limiterPreset.Threshold)) {
                        limiterPreset.Threshold = AudioLimiter.Threshold;
                    }
                    if(!float.TryParse(presetNode.GetValue("Bias"), out limiterPreset.Bias)) {
                        limiterPreset.Bias = AudioLimiter.Bias;
                    }
                    if(!float.TryParse(presetNode.GetValue("Ratio"), out limiterPreset.Ratio)) {
                        limiterPreset.Ratio = AudioLimiter.Ratio;
                    }
                    if(!float.TryParse(presetNode.GetValue("Gain"), out limiterPreset.Gain)) {
                        limiterPreset.Gain = AudioLimiter.Gain;
                    }
                    if(!int.TryParse(presetNode.GetValue("TimeConstant"), out limiterPreset.TimeConstant)) {
                        limiterPreset.TimeConstant = AudioLimiter.TimeConstant;
                    }
                    if(!int.TryParse(presetNode.GetValue("LevelDetectorRMSWindow"), out limiterPreset.LevelDetectorRMSWindow)) {
                        limiterPreset.LevelDetectorRMSWindow = AudioLimiter.LevelDetectorRMSWindow;
                    }

                    if(!AudioLimiter.Presets.ContainsKey(presetName)) {
                        AudioLimiter.Presets.Add(presetName, limiterPreset);
                    } else {
                        AudioLimiter.Presets[presetName] = limiterPreset;
                    }
                }

                AudioLimiter.ApplyPreset();
            } else {
                var defaultLimiterNode = settingsNode.AddNode("AUDIOLIMITER");
                defaultLimiterNode.AddValue("EnableLimiter", AudioLimiter.EnableLimiter);
                defaultLimiterNode.AddValue("Preset", "Custom");
                AudioLimiter.Preset = "Custom";

                AudioLimiter.Default();
                var defaultPresetNode = defaultLimiterNode.AddNode("Custom");
                defaultPresetNode.AddValue("Threshold", AudioLimiter.DefaultLimiterPreset.Threshold);
                defaultPresetNode.AddValue("Bias", AudioLimiter.DefaultLimiterPreset.Bias);
                defaultPresetNode.AddValue("Ratio", AudioLimiter.DefaultLimiterPreset.Ratio);
                defaultPresetNode.AddValue("Gain", AudioLimiter.DefaultLimiterPreset.Gain);
                defaultPresetNode.AddValue("TimeConstant", AudioLimiter.DefaultLimiterPreset.TimeConstant);
                defaultPresetNode.AddValue("LevelDetectorRMSWindow", AudioLimiter.DefaultLimiterPreset.LevelDetectorRMSWindow);
            }
            #endregion
            
            #region AUDIOMUFFLER
            if(settingsNode.HasNode("AUDIOMUFFLER")) {
                var audioMufflerNode = settingsNode.GetNode("AUDIOMUFFLER");

                if(!audioMufflerNode.HasValue("EnableMuffling")) {
                    audioMufflerNode.AddValue("EnableMuffling", true);
                }

                if(audioMufflerNode.HasValue("MufflerQuality")) {
                    if(!Enum.TryParse(audioMufflerNode.GetValue("MufflerQuality"), true,out AudioMuffler.MufflerQuality)) {
                        AudioMuffler.MufflerQuality = AudioMufflerQuality.Lite;
                    }
                } else {
                    audioMufflerNode.AddValue("MufflerQuality", AudioMuffler.MufflerQuality);
                }

                if(audioMufflerNode.HasValue("AffectChatterer")) {
                    if(!bool.TryParse(audioMufflerNode.GetValue("AffectChatterer"), out AudioMuffler.AffectChatterer)) {
                        AudioMuffler.AffectChatterer = false;
                    }
                } else {
                    audioMufflerNode.AddValue("AffectChatterer", AudioMuffler.AffectChatterer);
                }

                bool.TryParse(audioMufflerNode.GetValue("EnableMuffling"), out AudioMuffler.EnableMuffling);

                if(audioMufflerNode.HasValue("Preset")) {
                    AudioMuffler.Preset = audioMufflerNode.GetValue("Preset");
                }

                if(audioMufflerNode.HasValue("AirSimMaxDistance")){
                    AudioMuffler.AirSimMaxDistance = float.Parse(audioMufflerNode.GetValue("AirSimMaxDistance"));
                }

                foreach(var presetNode in audioMufflerNode.GetNodes()) {
                    string presetName = presetNode.name;
                    LowpassFilterPreset lowpassFilterPreset = new LowpassFilterPreset();

                    if(AudioMuffler.Preset == string.Empty) {
                        AudioMuffler.Preset = presetName;
                    }

                    if(!float.TryParse(presetNode.GetValue("InteriorMuffling"), out lowpassFilterPreset.InteriorMuffling)) {
                        lowpassFilterPreset.InteriorMuffling = AudioMuffler.InteriorMuffling;
                    }
                    if(!float.TryParse(presetNode.GetValue("ExteriorMuffling"), out lowpassFilterPreset.ExteriorMuffling)) {
                        lowpassFilterPreset.ExteriorMuffling = AudioMuffler.ExteriorMuffling;
                    }

                    if(!AudioMuffler.Presets.ContainsKey(presetName)) {
                        AudioMuffler.Presets.Add(presetName, lowpassFilterPreset);
                    } else {
                        AudioMuffler.Presets[presetName] = lowpassFilterPreset;
                    }
                }

                AudioMuffler.ApplyPreset();
            } else {
                var defaultLowpassFilterNode = settingsNode.AddNode("AUDIOMUFFLER");
                defaultLowpassFilterNode.AddValue("EnableMuffling", AudioMuffler.EnableMuffling);
                defaultLowpassFilterNode.AddValue("Preset", "Custom");
                AudioMuffler.Preset = "Custom";

                AudioMuffler.Default();
                var defaultPresetNode = defaultLowpassFilterNode.AddNode("Custom");
                defaultPresetNode.AddValue("InteriorMuffling", AudioMuffler.DefaultLowpassFilterPreset.InteriorMuffling);
                defaultPresetNode.AddValue("ExteriorMuffling", AudioMuffler.DefaultLowpassFilterPreset.ExteriorMuffling);
            }
            #endregion

            _settings.Save(ModPath + "Settings.cfg");
        }

        public void Save()
        {
            var settingsNode = _settings.GetNode(SettingsName);
            settingsNode.SetValue("ExteriorVolume", ExteriorVolume, true);
            settingsNode.SetValue("InteriorVolume", InteriorVolume, true);
            settingsNode.SetValue("DisableStagingSound", DisableStagingSound, true);

            if(settingsNode.HasNode("AUDIOLIMITER")) {
                var limiterNode = settingsNode.GetNode("AUDIOLIMITER");

                limiterNode.SetValue("EnableLimiter", AudioLimiter.EnableLimiter, true);
                limiterNode.SetValue("Preset", AudioLimiter.Preset, true);

                if(AudioLimiter.Preset == "Custom") {
                    var customPreset = limiterNode.GetNode("Custom");

                    customPreset.SetValue("Threshold", AudioLimiter.Threshold, true);
                    customPreset.SetValue("Bias", AudioLimiter.Bias, true);
                    customPreset.SetValue("Ratio", AudioLimiter.Ratio, true);
                    customPreset.SetValue("Gain", AudioLimiter.Gain, true);
                    customPreset.SetValue("TimeConstant", AudioLimiter.TimeConstant, true);
                    customPreset.SetValue("LevelDetectorRMSWindow", AudioLimiter.LevelDetectorRMSWindow, true);
                }
            }

            if(settingsNode.HasNode("AUDIOMUFFLER")) {
                var lowpassFilterNode = settingsNode.GetNode("AUDIOMUFFLER");

                lowpassFilterNode.SetValue("EnableMuffling", AudioMuffler.EnableMuffling, true);
                lowpassFilterNode.SetValue("MufflerQuality", AudioMuffler.MufflerQuality.ToString(), true);
                lowpassFilterNode.SetValue("AffectChatterer", AudioMuffler.AffectChatterer, true);

                lowpassFilterNode.SetValue("Preset", AudioMuffler.Preset, true);

                if(AudioMuffler.Preset == "Custom") {
                    var customPreset = lowpassFilterNode.GetNode("Custom");

                    customPreset.SetValue("InteriorMuffling", AudioMuffler.InteriorMuffling, true);
                    customPreset.SetValue("ExteriorMuffling", AudioMuffler.ExteriorMuffling, true);
                }
            }

            _settings.Save(ModPath  + "Settings.cfg");
        }
    }
}
