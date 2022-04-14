﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RocketSoundEnhancement
{
    public class RSE_Module : PartModule
    {
        public Dictionary<string, AudioSource> Sources = new Dictionary<string, AudioSource>();
        public Dictionary<string, AirSimulationFilter> AirSimFilters = new Dictionary<string, AirSimulationFilter>();
        public Dictionary<string, float> Controls = new Dictionary<string, float>();

        public Dictionary<string, List<SoundLayer>> SoundLayerGroups;
        public List<SoundLayer> SoundLayers;

        public GameObject audioParent;

        public bool initialized;
        public bool gamePaused;
        public bool UseAirSimFilters = false;
        public bool EnableCombFilter = false;
        public bool EnableLowpassFilter = false;
        public bool EnableWaveShaperFilter = false;

        public float volume = 1;

        public override void OnStart(StartState state)
        {
            GameEvents.onGamePause.Add(onGamePause);
            GameEvents.onGameUnpause.Add(onGameUnpause);

            initialized = true;
        }

        float distance = 0;
        float speedOfSound = 340.29f;
        Vector3 machOriginCameraNormal = Vector3.zero;
        public override void OnUpdate()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                var sourceKeys = Sources.Keys.ToList();

                foreach(var source in sourceKeys) {
                    // Calculate Air Simulation
                    if(UseAirSimFilters && Settings.Instance.AirSimulation) {
                        if(Sources[source].isPlaying) {
                            AirSimulationFilter airSimFilter;
                            if(!AirSimFilters.ContainsKey(source)) {
                                airSimFilter = Sources[source].gameObject.AddComponent<AirSimulationFilter>();
                                AirSimFilters.Add(source, airSimFilter);

                                airSimFilter.enabled = true;
                                airSimFilter.EnableCombFilter = EnableCombFilter;
                                airSimFilter.EnableLowpassFilter = EnableLowpassFilter;
                                airSimFilter.EnableWaveShaperFilter = EnableWaveShaperFilter;
                            } else {
                                airSimFilter = AirSimFilters[source];
                            }

                            airSimFilter.Distance = distance;
                            airSimFilter.Velocity = (float)vessel.srfSpeed;
                            airSimFilter.Angle = Vector3.Dot(machOriginCameraNormal, (transform.up + vessel.velocityD).normalized);
                            airSimFilter.VesselSize = vessel.vesselSize.magnitude;
                            airSimFilter.SpeedOfSound = speedOfSound;
                            airSimFilter.AtmosphericPressurePa = (float)vessel.staticPressurekPa * 1000f;
                            airSimFilter.ActiveInternalVessel = part.vessel == FlightGlobals.ActiveVessel && InternalCamera.Instance.isActive;

                            if(AudioMuffler.VacuumMuffling == 0 && vessel != FlightGlobals.ActiveVessel) {
                                airSimFilter.LowpassFrequency *= Mathf.Clamp01((float)vessel.atmDensity);
                            }
                        }
                    }

                    if(AirSimFilters.ContainsKey(source) && !Settings.Instance.AirSimulation) {
                        UnityEngine.Object.Destroy(AirSimFilters[source]);
                        AirSimFilters.Remove(source);
                    }

                    if(!Sources[source].isPlaying) {
                        if(AirSimFilters.ContainsKey(source)) {
                            UnityEngine.Object.Destroy(AirSimFilters[source]);
                            AirSimFilters.Remove(source);
                        }

                        // we dont want to accidentally delete the actual part
                        if(Sources[source].gameObject.name == source) {
                            UnityEngine.Object.Destroy(Sources[source].gameObject);
                        } else {
                            UnityEngine.Object.Destroy(Sources[source]);
                        }
                        
                        Sources.Remove(source);
                        Controls.Remove(source);
                    }
                }
            }
        }

        public virtual void FixedUpdate()
        {
            if(!initialized)
                return;

            if(Settings.Instance.AirSimulation) {
                distance = Vector3.Distance(CameraManager.GetCurrentCamera().transform.position, transform.position);

                if(vessel.GetComponent<ShipEffects>() != null) {
                    machOriginCameraNormal = vessel.GetComponent<ShipEffects>().MachOriginCameraNormal;
                } else {
                    machOriginCameraNormal = (CameraManager.GetCurrentCamera().transform.position - transform.position).normalized;
                }

                speedOfSound = vessel.speedOfSound > 0 ? (float)vessel.speedOfSound : 340.29f;

                CalculateDoppler();
            }
        }

        public float Doppler = 1;
        public float DopplerFactor = 0.5f;
        float dopplerRaw = 1;

        float relativeSpeed = 0;
        float lastDistance = 0;

        public void CalculateDoppler()
        {
            relativeSpeed = (lastDistance - distance) / Time.fixedDeltaTime;
            lastDistance = distance;
            dopplerRaw = Mathf.Clamp((speedOfSound + ((relativeSpeed) * DopplerFactor)) / speedOfSound, 0.5f, 1.5f);

            Doppler = Mathf.MoveTowards(Doppler, dopplerRaw, 0.5f * Time.fixedDeltaTime);
        }

        float pitchVariation = 1;
        public void PlaySoundLayer(GameObject audioGameObject, string sourceLayerName, SoundLayer soundLayer, float rawControl, float vol, bool spoolProccess = true, bool oneShot = false, bool rndOneShotVol = false)
        {
            float control = rawControl;

            if(spoolProccess) {
                if(!Controls.ContainsKey(sourceLayerName)) {
                    Controls.Add(sourceLayerName, 0);
                }

                if(soundLayer.spool) {
                    Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, soundLayer.spoolSpeed * TimeWarp.deltaTime);
                    control = Controls[sourceLayerName];
                } else {
                    //fix for audiosource clicks
                    Controls[sourceLayerName] = Mathf.MoveTowards(Controls[sourceLayerName], control, AudioUtility.SmoothControl.Evaluate(control) * (60 * Time.deltaTime));
                    control = Controls[sourceLayerName];
                }
            }

            control = Mathf.Round(control * 1000.0f) * 0.001f;

            //For Looped sounds cleanup
            if(control < float.Epsilon) {
                if(Sources.ContainsKey(sourceLayerName)) {
                    Sources[sourceLayerName].Stop();
                }
                return;
            }

            AudioSource source;
            if(!Sources.ContainsKey(sourceLayerName)) {
                GameObject sourceGameObject = new GameObject(sourceLayerName);
                sourceGameObject.transform.parent = audioGameObject.transform;
                sourceGameObject.transform.position = audioGameObject.transform.position;
                sourceGameObject.transform.rotation = audioGameObject.transform.rotation;

                source = AudioUtility.CreateSource(sourceGameObject, soundLayer);

                Sources.Add(sourceLayerName, source);

                if(soundLayer.pitchVariation) {
                    pitchVariation = UnityEngine.Random.Range(0.95f, 1.05f);
                }

            } else {
                source = Sources[sourceLayerName];
            }

            if(soundLayer.useFloatCurve) {
                source.volume = soundLayer.volumeFC.Evaluate(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitchFC.Evaluate(control);
            } else {
                source.volume = soundLayer.volume.Value(control) * GameSettings.SHIP_VOLUME * vol;
                source.pitch = soundLayer.pitch.Value(control);
            }

            if(soundLayer.massToVolume != null) {
                source.volume *= soundLayer.massToVolume.Value((float)part.physicsMass);
            }

            if(soundLayer.massToPitch != null) {
                source.pitch *= soundLayer.massToPitch.Value((float)part.physicsMass);
            }

            if(soundLayer.pitchVariation && !soundLayer.loopAtRandom) {
                source.pitch *= pitchVariation;
            }

            if(Settings.Instance.AirSimulation) {
                source.pitch *= Doppler;
            }


            if(oneShot) {
                int index = 0;
                if(soundLayer.audioClips.Length > 1) {
                    index = UnityEngine.Random.Range(0, soundLayer.audioClips.Length);
                }
                AudioClip clip = GameDatabase.Instance.GetAudioClip(soundLayer.audioClips[index]);
                float volumeScale = rndOneShotVol ? UnityEngine.Random.Range(0.9f, 1.0f) : 1;

                AudioUtility.PlayAtChannel(source, soundLayer.channel, false, false,true, volumeScale, clip);

            } else {
                AudioUtility.PlayAtChannel(source, soundLayer.channel, soundLayer.loop, soundLayer.loopAtRandom);
            }
            
        }

        public void onGamePause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Pause();
                }
            }
            gamePaused = true;
        }

        public void onGameUnpause()
        {
            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.UnPause();
                }
            }
            gamePaused = false;
        }

        public void OnDestroy()
        {
            if(!initialized)
                return;

            if(Sources.Count > 0) {
                foreach(var source in Sources.Values) {
                    source.Stop();
                    UnityEngine.Object.Destroy(source);
                }
            }

            GameEvents.onGamePause.Remove(onGamePause);
            GameEvents.onGameUnpause.Remove(onGameUnpause);
        }
    }
}
