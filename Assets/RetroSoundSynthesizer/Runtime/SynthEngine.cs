using System;
using UnityEngine;

namespace RetroSoundSynthesizer.Runtime
{
    public static class SynthEngine
    {
        private const int LO_RES_NOISE_PERIOD = 8;

        /// <summary>
        /// Mathematically synthesizes sound parameters into a raw float audio buffer.
        /// The buffer is generated at 44100 Hz mono and normalized between -1.0 and 1.0.
        /// </summary>
        public static float[] Synthesize(SoundParameters p)
        {
            // Seeded RNG for noise generation so synthesis remains deterministic if parameters don't change
            System.Random rand = new System.Random(1337);

            // Phase and periods
            float period = 100.0f / (p.startFrequency * p.startFrequency + 0.001f);
            float maxPeriod = 100.0f / (p.minFrequencyCutoff * p.minFrequencyCutoff + 0.001f);

            float slide = 1.0f - p.slide * p.slide * p.slide * 0.01f;
            float deltaSlide = -p.deltaSlide * p.deltaSlide * p.deltaSlide * 0.000001f;

            float squareDuty = 0.0f;
            float dutySweep = 0.0f;
            if (p.waveType == WaveType.Square)
            {
                squareDuty = 0.5f - p.dutyCycle * 0.5f;
                dutySweep = -p.dutySweep * 0.00005f;
            }

            // Arpeggiator
            float changeAmount = 1.0f;
            if (p.frequencyMult > 0.0f)
            {
                changeAmount = 1.0f - p.frequencyMult * p.frequencyMult * 0.9f;
            }
            else
            {
                changeAmount = 1.0f + p.frequencyMult * p.frequencyMult * 10.0f;
            }

            int changeTime = 0;
            bool changeReached = false;
            int changeLimit = 0;
            if (p.changeSpeed == 1.0f)
            {
                changeLimit = 0;
            }
            else
            {
                changeLimit = (int)((1.0f - p.changeSpeed) * (1.0f - p.changeSpeed) * 20000.0f + 32.0f);
            }

            // Retrigger
            int repeatTime = 0;
            int repeatLimit = 0;
            if (p.rate == 0.0f)
            {
                repeatLimit = 0;
            }
            else
            {
                repeatLimit = (int)((1.0f - p.rate) * (1.0f - p.rate) * 20000.0f) + 32;
            }

            // Low-Pass Filter
            bool filtersActive = p.lpCutoffFrequency != 1.0f || p.hpCutoffFrequency != 0.0f;
            float lpFilterCutoff = p.lpCutoffFrequency * p.lpCutoffFrequency * p.lpCutoffFrequency * 0.1f;
            float lpFilterDeltaCutoff = 1.0f + p.lpCutoffSweep * 0.0001f;
            float lpFilterDamping = 5.0f / (1.0f + p.resonance * p.resonance * 20.0f) * (0.01f + lpFilterCutoff);
            if (lpFilterDamping > 0.8f) lpFilterDamping = 0.8f;
            lpFilterDamping = 1.0f - lpFilterDamping;
            bool lpFilterOn = p.lpCutoffFrequency != 1.0f;

            float lpFilterPos = 0.0f;
            float lpFilterDeltaPos = 0.0f;

            // High-Pass Filter
            float hpFilterCutoff = p.hpCutoffFrequency * p.hpCutoffFrequency * 0.1f;
            float hpFilterDeltaCutoff = 1.0f + p.hpCutoffSweep * 0.0003f;
            float hpFilterPos = 0.0f;

            // Vibrato
            float vibratoPhase = 0.0f;
            float vibratoSpeed = p.speed * p.speed * 0.01f;
            float vibratoAmplitude = p.depth * 0.5f;

            // Envelope
            float envelopeVolume = 0.0f;
            int envelopeStage = 0;
            float envelopeTime = 0.0f;
            float envelopeLength0 = p.attackTime * p.attackTime * 100000.0f;
            float envelopeLength1 = p.sustainTime * p.sustainTime * 100000.0f;
            float envelopeLength2 = p.decayTime * p.decayTime * 100000.0f + 10.0f;
            float envelopeLength = envelopeLength0;
            uint envelopeFullLength = (uint)(envelopeLength0 + envelopeLength1 + envelopeLength2);

            float envelopeOverLength0 = 1.0f / envelopeLength0;
            float envelopeOverLength1 = 1.0f / envelopeLength1;
            float envelopeOverLength2 = 1.0f / envelopeLength2;

            // Phaser / Flanger
            bool phaserActive = p.offset != 0.0f || p.flangerSweep != 0.0f;
            float phaserOffset = p.offset * p.offset * 1020.0f;
            if (p.offset < 0.0f) phaserOffset = -phaserOffset;
            float phaserDeltaOffset = p.flangerSweep * p.flangerSweep * p.flangerSweep * 0.2f;
            int phaserPos = 0;
            float[] phaserBuffer = new float[1024];

            // Noise buffers
            float[] noiseBuffer = new float[32];
            for (int i = 0; i < 32; i++) noiseBuffer[i] = (float)(rand.NextDouble() * 2.0 - 1.0);

            // Phase tracking
            int phase = 0;
            float masterVolume = p.masterGain * p.masterGain;

            // Preallocate output buffer
            float[] buffer = new float[envelopeFullLength];
            bool finished = false;

            for (int i = 0; i < envelopeFullLength; i++)
            {
                if (finished) break;

                // Retrigger check
                if (repeatLimit != 0)
                {
                    repeatTime++;
                    if (repeatTime >= repeatLimit)
                    {
                        repeatTime = 0;
                        // Reset simple frequency, period, and duty variables
                        period = 100.0f / (p.startFrequency * p.startFrequency + 0.001f);
                        slide = 1.0f - p.slide * p.slide * p.slide * 0.01f;
                        deltaSlide = -p.deltaSlide * p.deltaSlide * p.deltaSlide * 0.000001f;
                        if (p.waveType == WaveType.Square)
                        {
                            squareDuty = 0.5f - p.dutyCycle * 0.5f;
                            dutySweep = -p.dutySweep * 0.00005f;
                        }
                        changeTime = 0;
                        changeReached = false;
                    }
                }

                // Pitch shift (Arpeggiator)
                if (!changeReached)
                {
                    changeTime++;
                    if (changeTime >= changeLimit)
                    {
                        changeReached = true;
                        period *= changeAmount;
                    }
                }

                // Apply frequency slide
                slide += deltaSlide;
                period *= slide;

                if (period > maxPeriod)
                {
                    period = maxPeriod;
                    if (p.minFrequencyCutoff > 0.0f)
                    {
                        finished = true;
                    }
                }

                float periodTemp = period;

                // Apply vibrato
                if (vibratoAmplitude > 0.0f)
                {
                    vibratoPhase += vibratoSpeed;
                    periodTemp = period * (1.0f + Mathf.Sin(vibratoPhase) * vibratoAmplitude);
                }

                int periodTempInt = (int)periodTemp;
                if (periodTempInt < 8) periodTempInt = 8;

                // Sweep square duty cycle
                if (p.waveType == WaveType.Square)
                {
                    squareDuty += dutySweep;
                    if (squareDuty < 0.0f) squareDuty = 0.0f;
                    else if (squareDuty > 0.5f) squareDuty = 0.5f;
                }

                // Envelope stage transitions
                envelopeTime++;
                if (envelopeTime > envelopeLength)
                {
                    envelopeTime = 0.0f;
                    envelopeStage++;
                    if (envelopeStage == 1) envelopeLength = envelopeLength1;
                    else if (envelopeStage == 2) envelopeLength = envelopeLength2;
                }

                // Set envelope volume
                switch (envelopeStage)
                {
                    case 0: // Attack
                        envelopeVolume = envelopeTime * envelopeOverLength0;
                        break;
                    case 1: // Sustain
                        envelopeVolume = 1.0f + (1.0f - envelopeTime * envelopeOverLength1) * 2.0f * p.sustainPunch;
                        break;
                    case 2: // Decay
                        envelopeVolume = 1.0f - envelopeTime * envelopeOverLength2;
                        break;
                    case 3: // End
                        envelopeVolume = 0.0f;
                        finished = true;
                        break;
                }

                // Phaser / Flanger sweep
                if (phaserActive)
                {
                    phaserOffset += phaserDeltaOffset;
                    int phaserInt = (int)phaserOffset;
                    if (phaserInt < 0) phaserInt = -phaserInt;
                    else if (phaserInt > 1023) phaserInt = 1023;

                    // Filter sweeps
                    if (hpFilterDeltaCutoff != 0.0f)
                    {
                        hpFilterCutoff *= hpFilterDeltaCutoff;
                        if (hpFilterCutoff < 0.00001f) hpFilterCutoff = 0.00001f;
                        else if (hpFilterCutoff > 0.1f) hpFilterCutoff = 0.1f;
                    }

                    // 8x Supersampling
                    float superSample = 0.0f;
                    for (int j = 0; j < 8; j++)
                    {
                        phase++;
                        if (phase >= periodTempInt)
                        {
                            phase = phase % periodTempInt;
                            if (p.waveType == WaveType.Noise)
                            {
                                for (int n = 0; n < 32; n++) noiseBuffer[n] = (float)(rand.NextDouble() * 2.0 - 1.0);
                            }
                        }

                        float sample = 0.0f;
                        float tempPhase = phase % periodTemp;

                        // Oscillator waveforms
                        switch (p.waveType)
                        {
                            case WaveType.Square:
                                sample = ((tempPhase / periodTemp) < squareDuty) ? 0.5f : -0.5f;
                                break;
                            case WaveType.Sawtooth:
                                sample = 1.0f - (tempPhase / periodTemp) * 2.0f;
                                break;
                            case WaveType.Sine:
                                float pos = tempPhase / periodTemp;
                                pos = pos > 0.5f ? (pos - 1.0f) * 6.28318531f : pos * 6.28318531f;
                                sample = pos < 0.0f ? 1.27323954f * pos + 0.405284735f * pos * pos : 1.27323954f * pos - 0.405284735f * pos * pos;
                                sample = sample < 0.0f ? 0.225f * (sample * -sample - sample) + sample : 0.225f * (sample * sample - sample) + sample;
                                break;
                            case WaveType.Noise:
                                int noiseIdx = (int)(tempPhase * 32.0f / periodTempInt) % 32;
                                sample = noiseBuffer[noiseIdx];
                                break;
                        }

                        // Apply Filters
                        if (filtersActive)
                        {
                            float lpFilterOldPos = lpFilterPos;
                            lpFilterCutoff *= lpFilterDeltaCutoff;
                            if (lpFilterCutoff < 0.0f) lpFilterCutoff = 0.0f;
                            else if (lpFilterCutoff > 0.1f) lpFilterCutoff = 0.1f;

                            if (lpFilterOn)
                            {
                                lpFilterDeltaPos += (sample - lpFilterPos) * lpFilterCutoff;
                                lpFilterDeltaPos *= lpFilterDamping;
                            }
                            else
                            {
                                lpFilterPos = sample;
                                lpFilterDeltaPos = 0.0f;
                            }
                            lpFilterPos += lpFilterDeltaPos;

                            hpFilterPos += lpFilterPos - lpFilterOldPos;
                            hpFilterPos *= 1.0f - hpFilterCutoff;
                            sample = hpFilterPos;
                        }

                        // Apply Flanger
                        phaserBuffer[phaserPos & 1023] = sample;
                        sample += phaserBuffer[(phaserPos - phaserInt + 1024) & 1023];
                        phaserPos = (phaserPos + 1) & 1023;

                        superSample += sample;
                    }

                    // Average and scale
                    float finalSample = masterVolume * envelopeVolume * superSample * 0.125f;

                    // Hard clip
                    if (finalSample < -1.0f) finalSample = -1.0f;
                    else if (finalSample > 1.0f) finalSample = 1.0f;

                    buffer[i] = finalSample;
                }
                else
                {
                    // Filter sweeps
                    if (hpFilterDeltaCutoff != 0.0f)
                    {
                        hpFilterCutoff *= hpFilterDeltaCutoff;
                        if (hpFilterCutoff < 0.00001f) hpFilterCutoff = 0.00001f;
                        else if (hpFilterCutoff > 0.1f) hpFilterCutoff = 0.1f;
                    }

                    // 8x Supersampling
                    float superSample = 0.0f;
                    for (int j = 0; j < 8; j++)
                    {
                        phase++;
                        if (phase >= periodTempInt)
                        {
                            phase = phase % periodTempInt;
                            if (p.waveType == WaveType.Noise)
                            {
                                for (int n = 0; n < 32; n++) noiseBuffer[n] = (float)(rand.NextDouble() * 2.0 - 1.0);
                            }
                        }

                        float sample = 0.0f;
                        float tempPhase = phase % periodTemp;

                        // Oscillator waveforms
                        switch (p.waveType)
                        {
                            case WaveType.Square:
                                sample = ((tempPhase / periodTemp) < squareDuty) ? 0.5f : -0.5f;
                                break;
                            case WaveType.Sawtooth:
                                sample = 1.0f - (tempPhase / periodTemp) * 2.0f;
                                break;
                            case WaveType.Sine:
                                float pos = tempPhase / periodTemp;
                                pos = pos > 0.5f ? (pos - 1.0f) * 6.28318531f : pos * 6.28318531f;
                                sample = pos < 0.0f ? 1.27323954f * pos + 0.405284735f * pos * pos : 1.27323954f * pos - 0.405284735f * pos * pos;
                                sample = sample < 0.0f ? 0.225f * (sample * -sample - sample) + sample : 0.225f * (sample * sample - sample) + sample;
                                break;
                            case WaveType.Noise:
                                int noiseIdx = (int)(tempPhase * 32.0f / periodTempInt) % 32;
                                sample = noiseBuffer[noiseIdx];
                                break;
                        }

                        // Apply Filters
                        if (filtersActive)
                        {
                            float lpFilterOldPos = lpFilterPos;
                            lpFilterCutoff *= lpFilterDeltaCutoff;
                            if (lpFilterCutoff < 0.0f) lpFilterCutoff = 0.0f;
                            else if (lpFilterCutoff > 0.1f) lpFilterCutoff = 0.1f;

                            if (lpFilterOn)
                            {
                                lpFilterDeltaPos += (sample - lpFilterPos) * lpFilterCutoff;
                                lpFilterDeltaPos *= lpFilterDamping;
                            }
                            else
                            {
                                lpFilterPos = sample;
                                lpFilterDeltaPos = 0.0f;
                            }
                            lpFilterPos += lpFilterDeltaPos;

                            hpFilterPos += lpFilterPos - lpFilterOldPos;
                            hpFilterPos *= 1.0f - hpFilterCutoff;
                            sample = hpFilterPos;
                        }

                        superSample += sample;
                    }

                    // Average and scale
                    float finalSample = masterVolume * envelopeVolume * superSample * 0.125f;

                    // Hard clip
                    if (finalSample < -1.0f) finalSample = -1.0f;
                    else if (finalSample > 1.0f) finalSample = 1.0f;

                    buffer[i] = finalSample;
                }
            }

            return buffer;
        }

        /// <summary>
        /// Populates standard parameters for classic presets.
        /// </summary>
        public static void GeneratePreset(SoundParameters p, string presetName, int? fixedSeed = null)
        {
            System.Random rand = fixedSeed.HasValue ? new System.Random(fixedSeed.Value) : new System.Random();
            Func<float> GetRandom = () => (float)rand.NextDouble();
            Func<bool> GetRandomBool = () => rand.Next(0, 2) == 1;

            p.waveType = WaveType.Square;
            p.attackTime = 0.0f;
            p.sustainTime = 0.3f;
            p.sustainPunch = 0.0f;
            p.decayTime = 0.4f;

            p.startFrequency = 0.3f;
            p.minFrequencyCutoff = 0.0f;
            p.slide = 0.0f;
            p.deltaSlide = 0.0f;

            p.depth = 0.0f;
            p.speed = 0.0f;

            p.frequencyMult = 0.0f;
            p.changeSpeed = 0.0f;

            p.dutyCycle = 0.0f;
            p.dutySweep = 0.0f;

            p.rate = 0.0f;

            p.offset = 0.0f;
            p.flangerSweep = 0.0f;

            p.lpCutoffFrequency = 1.0f;
            p.lpCutoffSweep = 0.0f;
            p.resonance = 0.0f;

            p.hpCutoffFrequency = 0.0f;
            p.hpCutoffSweep = 0.0f;

            switch (presetName.ToLower())
            {
                case "laser":
                    p.waveType = (WaveType)rand.Next(0, 3); // Square, Sawtooth, Sine
                    if (p.waveType == WaveType.Sine && GetRandomBool())
                    {
                        p.waveType = (WaveType)rand.Next(0, 2);
                    }
                    p.startFrequency = 0.5f + GetRandom() * 0.5f;
                    p.minFrequencyCutoff = p.startFrequency - 0.2f - GetRandom() * 0.6f;
                    if (p.minFrequencyCutoff < 0.2f) p.minFrequencyCutoff = 0.2f;
                    p.slide = -0.15f - GetRandom() * 0.2f;

                    if (GetRandom() < 0.33f)
                    {
                        p.startFrequency = 0.3f + GetRandom() * 0.6f;
                        p.minFrequencyCutoff = GetRandom() * 0.1f;
                        p.slide = -0.35f - GetRandom() * 0.3f;
                    }

                    if (GetRandomBool())
                    {
                        p.dutyCycle = GetRandom() * 0.5f;
                        p.dutySweep = GetRandom() * 0.2f;
                    }
                    else
                    {
                        p.dutyCycle = 0.4f + GetRandom() * 0.5f;
                        p.dutySweep = -GetRandom() * 0.7f;
                    }

                    p.sustainTime = 0.1f + GetRandom() * 0.2f;
                    p.decayTime = GetRandom() * 0.4f;
                    if (GetRandomBool()) p.sustainPunch = GetRandom() * 0.3f;

                    if (GetRandom() < 0.33f)
                    {
                        p.offset = GetRandom() * 0.2f;
                        p.flangerSweep = -GetRandom() * 0.2f;
                    }
                    if (GetRandomBool()) p.hpCutoffFrequency = GetRandom() * 0.3f;
                    break;

                case "coin":
                case "moneda":
                    p.waveType = WaveType.Square;
                    p.startFrequency = 0.4f + GetRandom() * 0.5f;
                    p.sustainTime = GetRandom() * 0.1f;
                    p.decayTime = 0.1f + GetRandom() * 0.4f;
                    p.sustainPunch = 0.3f + GetRandom() * 0.3f;

                    if (GetRandomBool())
                    {
                        p.changeSpeed = 0.5f + GetRandom() * 0.2f;
                        int cnum = rand.Next(1, 8);
                        int cden = cnum + rand.Next(2, 9);
                        p.frequencyMult = (float)cnum / (float)cden;
                    }
                    break;

                case "explosion":
                case "explosión":
                    p.waveType = WaveType.Noise;
                    if (GetRandomBool())
                    {
                        p.startFrequency = 0.1f + GetRandom() * 0.4f;
                        p.slide = -0.1f + GetRandom() * 0.4f;
                    }
                    else
                    {
                        p.startFrequency = 0.2f + GetRandom() * 0.7f;
                        p.slide = -0.2f - GetRandom() * 0.2f;
                    }
                    p.startFrequency *= p.startFrequency;

                    if (GetRandom() < 0.2f) p.slide = 0.0f;
                    if (GetRandom() < 0.33f) p.rate = 0.3f + GetRandom() * 0.5f;

                    p.sustainTime = 0.1f + GetRandom() * 0.3f;
                    p.decayTime = GetRandom() * 0.5f;
                    p.sustainPunch = 0.2f + GetRandom() * 0.6f;

                    if (GetRandomBool())
                    {
                        p.offset = -0.3f + GetRandom() * 0.9f;
                        p.flangerSweep = -GetRandom() * 0.3f;
                    }

                    if (GetRandom() < 0.33f)
                    {
                        p.changeSpeed = 0.6f + GetRandom() * 0.3f;
                        p.frequencyMult = 0.8f - GetRandom() * 1.6f;
                    }
                    break;

                case "jump":
                case "salto":
                    p.waveType = WaveType.Square;
                    p.dutyCycle = GetRandom() * 0.6f;
                    p.startFrequency = 0.3f + GetRandom() * 0.3f;
                    p.slide = 0.1f + GetRandom() * 0.2f;
                    p.sustainTime = 0.1f + GetRandom() * 0.3f;
                    p.decayTime = 0.1f + GetRandom() * 0.2f;

                    if (GetRandomBool()) p.hpCutoffFrequency = GetRandom() * 0.3f;
                    if (GetRandomBool()) p.lpCutoffFrequency = 1.0f - GetRandom() * 0.6f;
                    break;
            }
        }

        /// <summary>
        /// Subtly mutates the current sound parameters of p.
        /// </summary>
        public static void Mutate(SoundParameters p, float amount = 0.05f)
        {
            System.Random rand = new System.Random();
            Func<bool> GetRandomBool = () => rand.Next(0, 2) == 1;
            Func<float> GetMutVal = () => (float)(rand.NextDouble() * amount * 2.0 - amount);

            if (GetRandomBool()) p.startFrequency = Mathf.Clamp01(p.startFrequency + GetMutVal());
            if (GetRandomBool()) p.minFrequencyCutoff = Mathf.Clamp01(p.minFrequencyCutoff + GetMutVal());
            if (GetRandomBool()) p.slide = Mathf.Clamp(p.slide + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.deltaSlide = Mathf.Clamp(p.deltaSlide + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.dutyCycle = Mathf.Clamp01(p.dutyCycle + GetMutVal());
            if (GetRandomBool()) p.dutySweep = Mathf.Clamp(p.dutySweep + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.depth = Mathf.Clamp01(p.depth + GetMutVal());
            if (GetRandomBool()) p.speed = Mathf.Clamp01(p.speed + GetMutVal());
            if (GetRandomBool()) p.attackTime = Mathf.Clamp01(p.attackTime + GetMutVal());
            if (GetRandomBool()) p.sustainTime = Mathf.Clamp01(p.sustainTime + GetMutVal());
            if (GetRandomBool()) p.decayTime = Mathf.Clamp01(p.decayTime + GetMutVal());
            if (GetRandomBool()) p.sustainPunch = Mathf.Clamp01(p.sustainPunch + GetMutVal());
            if (GetRandomBool()) p.lpCutoffFrequency = Mathf.Clamp01(p.lpCutoffFrequency + GetMutVal());
            if (GetRandomBool()) p.lpCutoffSweep = Mathf.Clamp(p.lpCutoffSweep + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.resonance = Mathf.Clamp01(p.resonance + GetMutVal());
            if (GetRandomBool()) p.hpCutoffFrequency = Mathf.Clamp01(p.hpCutoffFrequency + GetMutVal());
            if (GetRandomBool()) p.hpCutoffSweep = Mathf.Clamp(p.hpCutoffSweep + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.offset = Mathf.Clamp(p.offset + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.flangerSweep = Mathf.Clamp(p.flangerSweep + GetMutVal(), -1f, 1f);
            if (GetRandomBool()) p.rate = Mathf.Clamp01(p.rate + GetMutVal());
            if (GetRandomBool()) p.changeSpeed = Mathf.Clamp01(p.changeSpeed + GetMutVal());
            if (GetRandomBool()) p.frequencyMult = Mathf.Clamp(p.frequencyMult + GetMutVal(), -1f, 1f);
        }
    }
}
