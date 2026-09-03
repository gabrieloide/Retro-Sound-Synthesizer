using System;
using UnityEngine;

namespace RetroSoundSynthesizer.Runtime
{
    /// <summary>
    /// Runtime player component that enables direct procedural sound playback in games
    /// without baking or loading WAV files from disk.
    /// </summary>
    public static class ProceduralAudioPlayer
    {
        private static GameObject runtimeHost;

        /// <summary>
        /// Converts a SoundParameters configuration directly into a Unity AudioClip in memory.
        /// </summary>
        public static AudioClip CreateAudioClip(SoundParameters parameters, string clipName = null)
        {
            if (parameters == null) return null;

            float[] samples = SynthEngine.Synthesize(parameters);
            if (samples == null || samples.Length == 0) return null;

            int sampleRate = (int)parameters.sampleRate;
            string name = string.IsNullOrEmpty(clipName) ? parameters.soundName : clipName;

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Converts a layered CompositeSound configuration into a Unity AudioClip in memory.
        /// </summary>
        public static AudioClip CreateAudioClip(CompositeSound compositeSound, string clipName = null)
        {
            if (compositeSound == null) return null;

            float[] samples = SynthEngine.Synthesize(compositeSound);
            if (samples == null || samples.Length == 0) return null;

            int sampleRate = (int)compositeSound.baseSound.sampleRate;
            string name = string.IsNullOrEmpty(clipName) ? compositeSound.baseSound.soundName : clipName;

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Instantly synthesizes and plays a sound at runtime with optional micro-variation to avoid audio fatigue.
        /// </summary>
        public static AudioSource Play(SoundParameters parameters, float variation = 0f, Vector3? position = null)
        {
            if (parameters == null) return null;

            SoundParameters soundToPlay = variation > 0f ? parameters.CreateVariation(variation) : parameters;
            AudioClip clip = CreateAudioClip(soundToPlay);
            if (clip == null) return null;

            return PlayClip(clip, position);
        }

        /// <summary>
        /// Instantly synthesizes and plays a composite sound at runtime with optional micro-variation.
        /// </summary>
        public static AudioSource Play(CompositeSound compositeSound, float variation = 0f, Vector3? position = null)
        {
            if (compositeSound == null) return null;

            CompositeSound soundToPlay = variation > 0f ? compositeSound.CreateVariation(variation) : compositeSound;
            AudioClip clip = CreateAudioClip(soundToPlay);
            if (clip == null) return null;

            return PlayClip(clip, position);
        }

        private static AudioSource PlayClip(AudioClip clip, Vector3? position)
        {
            if (runtimeHost == null)
            {
                runtimeHost = new GameObject("[ProceduralAudioPlayer_Host]");
                UnityEngine.Object.DontDestroyOnLoad(runtimeHost);
            }

            GameObject soundObj = new GameObject($"SFX_{clip.name}");
            soundObj.transform.parent = runtimeHost.transform;
            if (position.HasValue)
            {
                soundObj.transform.position = position.Value;
            }

            AudioSource source = soundObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = position.HasValue ? 1.0f : 0.0f;
            source.Play();

            UnityEngine.Object.Destroy(soundObj, clip.length + 0.1f);
            return source;
        }
    }
}