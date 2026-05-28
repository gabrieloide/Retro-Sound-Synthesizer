using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroSoundSynthesizer.Runtime
{
    public enum WaveType
    {
        Square = 0,
        Sawtooth = 1,
        Sine = 2,
        Noise = 3
    }

    public enum SampleRateOption
    {
        Rate44k = 44100,
        Rate22k = 22050,
        Rate11k = 11025,
        Rate8k = 8000
    }

    public enum SampleSizeOption
    {
        Bit16 = 16,
        Bit8 = 8
    }

    [Serializable]
    public class SoundParameters
    {
        // General
        public string soundName = "retro_sound";
        public WaveType waveType = WaveType.Square;

        // Envelope
        [Range(0f, 1f)] public float attackTime = 0.0f;
        [Range(0f, 1f)] public float sustainTime = 0.3f;
        [Range(0f, 1f)] public float sustainPunch = 0.0f;
        [Range(0f, 1f)] public float decayTime = 0.4f;

        // Frequency
        [Range(0f, 1f)] public float startFrequency = 0.3f;
        [Range(0f, 1f)] public float minFrequencyCutoff = 0.0f;
        [Range(-1f, 1f)] public float slide = 0.0f;
        [Range(-1f, 1f)] public float deltaSlide = 0.0f;

        // Vibrato
        [Range(0f, 1f)] public float depth = 0.0f;
        [Range(0f, 1f)] public float speed = 0.0f;

        // Arpeggiation
        [Range(-1f, 1f)] public float frequencyMult = 0.0f; // sfxr changeAmount
        [Range(0f, 1f)] public float changeSpeed = 0.0f;     // sfxr changeSpeed

        // Duty Cycle (for Square wave)
        [Range(0f, 1f)] public float dutyCycle = 0.0f;       // sfxr squareDuty
        [Range(-1f, 1f)] public float dutySweep = 0.0f;       // sfxr dutySweep (named to avoid duplicate 'sweep' field)

        // Retrigger
        [Range(0f, 1f)] public float rate = 0.0f;            // sfxr repeatSpeed

        // Flanger
        [Range(-1f, 1f)] public float offset = 0.0f;          // sfxr phaserOffset
        [Range(-1f, 1f)] public float flangerSweep = 0.0f;   // sfxr phaserSweep (named to avoid duplicate 'sweep' field)

        // Low-Pass Filter
        [Range(0f, 1f)] public float lpCutoffFrequency = 1.0f; // sfxr lpFilterCutoff (default 1.0f, named to avoid 'cutoffFrequency' duplicate)
        [Range(-1f, 1f)] public float lpCutoffSweep = 0.0f;    // sfxr lpFilterCutoffSweep
        [Range(0f, 1f)] public float resonance = 0.0f;         // sfxr lpFilterResonance

        // High-Pass Filter
        [Range(0f, 1f)] public float hpCutoffFrequency = 0.0f; // sfxr hpFilterCutoff (default 0.0f, named to avoid 'cutoffFrequency' duplicate)
        [Range(-1f, 1f)] public float hpCutoffSweep = 0.0f;    // sfxr hpFilterCutoffSweep

        // Output configuration
        public SampleRateOption sampleRate = SampleRateOption.Rate44k;
        public SampleSizeOption sampleSize = SampleSizeOption.Bit16;
        [Range(0f, 1f)] public float masterGain = 0.5f;

        public SoundParameters Clone()
        {
            return new SoundParameters
            {
                soundName = this.soundName,
                waveType = this.waveType,
                attackTime = this.attackTime,
                sustainTime = this.sustainTime,
                sustainPunch = this.sustainPunch,
                decayTime = this.decayTime,
                startFrequency = this.startFrequency,
                minFrequencyCutoff = this.minFrequencyCutoff,
                slide = this.slide,
                deltaSlide = this.deltaSlide,
                depth = this.depth,
                speed = this.speed,
                frequencyMult = this.frequencyMult,
                changeSpeed = this.changeSpeed,
                dutyCycle = this.dutyCycle,
                dutySweep = this.dutySweep,
                rate = this.rate,
                offset = this.offset,
                flangerSweep = this.flangerSweep,
                lpCutoffFrequency = this.lpCutoffFrequency,
                lpCutoffSweep = this.lpCutoffSweep,
                resonance = this.resonance,
                hpCutoffFrequency = this.hpCutoffFrequency,
                hpCutoffSweep = this.hpCutoffSweep,
                sampleRate = this.sampleRate,
                sampleSize = this.sampleSize,
                masterGain = this.masterGain
            };
        }

        public void Randomize()
        {
            System.Random rand = new System.Random();
            Func<float> GetRandom = () => (float)rand.NextDouble();
            Func<bool> GetRandomBool = () => rand.Next(0, 2) == 1;

            waveType = (WaveType)rand.Next(0, 4);

            attackTime = Mathf.Pow(GetRandom() * 2f - 1f, 4);
            sustainTime = Mathf.Pow(GetRandom() * 2f - 1f, 2);
            sustainPunch = Mathf.Pow(GetRandom() * 0.8f, 2);
            decayTime = GetRandom();

            startFrequency = (GetRandomBool()) ? Mathf.Pow(GetRandom() * 2f - 1f, 2) : (Mathf.Pow(GetRandom() * 0.5f, 3) + 0.5f);
            minFrequencyCutoff = 0.0f;

            slide = Mathf.Pow(GetRandom() * 2f - 1f, 3);
            deltaSlide = Mathf.Pow(GetRandom() * 2f - 1f, 3);

            depth = Mathf.Pow(GetRandom() * 2f - 1f, 3);
            speed = GetRandom() * 2f - 1f;

            frequencyMult = GetRandom() * 2f - 1f;
            changeSpeed = GetRandom() * 2f - 1f;

            dutyCycle = GetRandom() * 2f - 1f;
            dutySweep = Mathf.Pow(GetRandom() * 2f - 1f, 3);

            rate = GetRandom() * 2f - 1f;

            offset = Mathf.Pow(GetRandom() * 2f - 1f, 3);
            flangerSweep = Mathf.Pow(GetRandom() * 2f - 1f, 3);

            lpCutoffFrequency = 1f - Mathf.Pow(GetRandom(), 3);
            lpCutoffSweep = Mathf.Pow(GetRandom() * 2f - 1f, 3);
            resonance = GetRandom() * 2f - 1f;

            hpCutoffFrequency = Mathf.Pow(GetRandom(), 5);
            hpCutoffSweep = Mathf.Pow(GetRandom() * 2f - 1f, 5);

            if (attackTime + sustainTime + decayTime < 0.2f)
            {
                sustainTime = 0.2f + GetRandom() * 0.3f;
                decayTime = 0.2f + GetRandom() * 0.3f;
            }

            if ((startFrequency > 0.7f && slide > 0.2f) || (startFrequency < 0.2f && slide < -0.05f))
            {
                slide = -slide;
            }

            if (lpCutoffFrequency < 0.1f && lpCutoffSweep < -0.05f)
            {
                lpCutoffSweep = -lpCutoffSweep;
            }
        }
    }


    [Serializable]
    public class SoundPack
    {
        public List<SoundParameters> sounds = new List<SoundParameters>();
    }
}
