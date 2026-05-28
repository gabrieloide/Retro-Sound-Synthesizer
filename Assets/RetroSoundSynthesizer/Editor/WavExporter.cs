using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RetroSoundSynthesizer.Runtime;

namespace RetroSoundSynthesizer.Editor
{
    public static class WavExporter
    {
        /// <summary>
        /// Downsamples, formats, and exports synthesized audio buffer to a standard RIFF/WAVE file in the Assets directory.
        /// </summary>
        public static string ExportToWav(float[] masterBuffer, SampleRateOption rateOption, SampleSizeOption sizeOption, string fileName)
        {
            if (masterBuffer == null || masterBuffer.Length == 0)
            {
                Debug.LogError("[WavExporter] Master buffer is empty!");
                return null;
            }

            int targetSampleRate = (int)rateOption;
            int targetBitsPerSample = (int)sizeOption;

            // 1. Resample from 44100 Hz to target sample rate using linear interpolation
            float[] targetBuffer;
            if (targetSampleRate == 44100)
            {
                targetBuffer = masterBuffer;
            }
            else
            {
                float factor = 44100.0f / targetSampleRate;
                int targetLength = Mathf.FloorToInt(masterBuffer.Length / factor);
                if (targetLength <= 0) targetLength = 1;

                targetBuffer = new float[targetLength];
                for (int i = 0; i < targetLength; i++)
                {
                    float sourcePos = i * factor;
                    int idx1 = Mathf.FloorToInt(sourcePos);
                    int idx2 = Mathf.Min(idx1 + 1, masterBuffer.Length - 1);
                    float t = sourcePos - idx1;
                    targetBuffer[i] = Mathf.Lerp(masterBuffer[idx1], masterBuffer[idx2], t);
                }
            }

            // 2. Formatting PCM audio data
            byte[] formattedData;
            if (targetBitsPerSample == 16)
            {
                formattedData = new byte[targetBuffer.Length * 2];
                for (int i = 0; i < targetBuffer.Length; i++)
                {
                    // Map -1.0 to 1.0 float to Int16 (-32768 to 32767)
                    short val = (short)Mathf.Clamp(targetBuffer[i] * 32767f, -32768f, 32767f);
                    formattedData[i * 2] = (byte)(val & 0xff);
                    formattedData[i * 2 + 1] = (byte)((val >> 8) & 0xff);
                }
            }
            else
            {
                formattedData = new byte[targetBuffer.Length];
                for (int i = 0; i < targetBuffer.Length; i++)
                {
                    // Map -1.0 to 1.0 float to UInt8 (0 to 255, where 128 is center/silence)
                    byte val = (byte)Mathf.Clamp((targetBuffer[i] * 127f) + 128f, 0f, 255f);
                    formattedData[i] = val;
                }
            }

            // 3. Construct the RIFF/WAVE header (44 bytes)
            uint soundLength = (uint)formattedData.Length;
            uint fileSize = 36 + soundLength;
            uint blockAlign = (uint)(targetBitsPerSample / 8);
            uint bytesPerSec = (uint)(targetSampleRate * blockAlign);

            byte[] header = new byte[44];

            // "RIFF"
            header[0] = 0x52; header[1] = 0x49; header[2] = 0x46; header[3] = 0x46;
            // File size - 8
            header[4] = (byte)(fileSize & 0xff);
            header[5] = (byte)((fileSize >> 8) & 0xff);
            header[6] = (byte)((fileSize >> 16) & 0xff);
            header[7] = (byte)((fileSize >> 24) & 0xff);
            // "WAVE"
            header[8] = 0x57; header[9] = 0x41; header[10] = 0x56; header[11] = 0x45;
            // "fmt "
            header[12] = 0x66; header[13] = 0x6d; header[14] = 0x74; header[15] = 0x20;
            // Format chunk size (16 for PCM)
            header[16] = 16; header[17] = 0; header[18] = 0; header[19] = 0;
            // Audio Format (1 for PCM)
            header[20] = 1; header[21] = 0;
            // Num Channels (1 for Mono)
            header[22] = 1; header[23] = 0;
            // Sample Rate
            header[24] = (byte)(targetSampleRate & 0xff);
            header[25] = (byte)((targetSampleRate >> 8) & 0xff);
            header[26] = (byte)((targetSampleRate >> 16) & 0xff);
            header[27] = (byte)((targetSampleRate >> 24) & 0xff);
            // Byte Rate
            header[28] = (byte)(bytesPerSec & 0xff);
            header[29] = (byte)((bytesPerSec >> 8) & 0xff);
            header[30] = (byte)((bytesPerSec >> 16) & 0xff);
            header[31] = (byte)((bytesPerSec >> 24) & 0xff);
            // Block Align
            header[32] = (byte)(blockAlign & 0xff);
            header[33] = 0;
            // Bits Per Sample
            header[34] = (byte)(targetBitsPerSample & 0xff);
            header[35] = 0;
            // "data"
            header[36] = 0x64; header[37] = 0x61; header[38] = 0x74; header[39] = 0x61;
            // Data chunk size
            header[40] = (byte)(soundLength & 0xff);
            header[41] = (byte)((soundLength >> 8) & 0xff);
            header[42] = (byte)((soundLength >> 16) & 0xff);
            header[43] = (byte)((soundLength >> 24) & 0xff);

            // 4. Combine header and formatted audio data
            byte[] fullFile = new byte[header.Length + formattedData.Length];
            Buffer.BlockCopy(header, 0, fullFile, 0, header.Length);
            Buffer.BlockCopy(formattedData, 0, fullFile, header.Length, formattedData.Length);

            // 5. Clean name and write to the Assets folder
            string sanitizedName = string.IsNullOrEmpty(fileName) ? "retro_sound" : fileName.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitizedName = sanitizedName.Replace(c, '_');
            }

            string relativePath = $"Assets/{sanitizedName}.wav";
            string absolutePath = Path.Combine(Application.dataPath, $"{sanitizedName}.wav");

            try
            {
                File.WriteAllBytes(absolutePath, fullFile);
                AssetDatabase.Refresh();
                Debug.Log($"[WavExporter] Saved procedural audio file to: {relativePath}");
                return relativePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WavExporter] Failed to write file: {ex.Message}");
                return null;
            }
        }
    }
}
