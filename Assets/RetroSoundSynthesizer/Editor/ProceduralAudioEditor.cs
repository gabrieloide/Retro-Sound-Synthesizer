using System;
using UnityEditor;
using UnityEngine;
using RetroSoundSynthesizer.Runtime;

namespace RetroSoundSynthesizer.Editor
{
    public class ProceduralAudioEditor : EditorWindow
    {
        [MenuItem("Tools/Procedural Audio Synthesizer")]
        public static void ShowWindow()
        {
            ProceduralAudioEditor window = GetWindow<ProceduralAudioEditor>("Retro sound Synthesizer");
            window.minSize = new Vector2(850, 600);
            window.Show();
        }

        private SoundParameters currentParams = new SoundParameters();
        private SoundPack currentPack = new SoundPack();
        private bool isSoundPackLoaded = false;

        // Editor layout variables
        private int controlMode = 0; // 0 = Sliders Manuales, 1 = Preset Pad / 2D Mixer
        private Vector2 scrollPosLeft;
        private Vector2 padCoordinates = new Vector2(0.5f, 0.5f);
        private string jsonClipboardText = "";

        // Seeded corners for 2D mixer
        private SoundParameters cornerLaser;
        private SoundParameters cornerCoin;
        private SoundParameters cornerExplosion;
        private SoundParameters cornerJump;

        // Custom styles
        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private GUIStyle selectionGridStyle;

        private void OnEnable()
        {
            InitializePadCorners();
            UpdateJsonTextArea();
        }

        private void InitializePadCorners()
        {
            // Seeded RNG seeds for stable deterministic presets in the corners
            cornerLaser = new SoundParameters { soundName = "laser_preset" };
            SynthEngine.GeneratePreset(cornerLaser, "laser", 42);

            cornerCoin = new SoundParameters { soundName = "coin_preset" };
            SynthEngine.GeneratePreset(cornerCoin, "coin", 42);

            cornerExplosion = new SoundParameters { soundName = "explosion_preset" };
            SynthEngine.GeneratePreset(cornerExplosion, "explosion", 42);

            cornerJump = new SoundParameters { soundName = "jump_preset" };
            SynthEngine.GeneratePreset(cornerJump, "jump", 42);
        }

        private void OnGUI()
        {
            // Initialize custom styles
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 5, 5),
                normal = { textColor = new Color(0.2f, 0.7f, 1.0f) }
            };

            sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 5, 5)
            };

            selectionGridStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 25
            };

            EditorGUILayout.BeginHorizontal();

            // =========================================================================
            // COLUMNA IZQUIERDA (Panel Dinámico con Switch)
            // =========================================================================
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.65f));
            DrawLeftColumn();
            EditorGUILayout.EndVertical();

            // Vertical divider line
            DrawVerticalDivider();

            // =========================================================================
            // COLUMNA DERECHA (Panel Fijo)
            // =========================================================================
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.33f));
            DrawRightColumn();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawVerticalDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(2, position.height, GUILayout.ExpandHeight(true));
            Color oldColor = GUI.color;
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawLeftColumn()
        {
            GUILayout.Space(10);
            GUILayout.Label("🎛️ CONTROLES DE SÍNTESIS", headerStyle);

            // Tab-Switch control
            string[] modes = { "🎚️ Sliders Manuales (sfxr)", "🎯 Preset Pad / Mezclador 2D" };
            int newMode = GUILayout.Toolbar(controlMode, modes, GUILayout.Height(30));
            if (newMode != controlMode)
            {
                controlMode = newMode;
                if (controlMode == 1)
                {
                    // Interpolate instantly based on current coordinates
                    InterpolateParameters(padCoordinates.x, padCoordinates.y);
                }
            }

            GUILayout.Space(8);

            scrollPosLeft = EditorGUILayout.BeginScrollView(scrollPosLeft);

            if (controlMode == 0)
            {
                DrawManualSliders();
            }
            else
            {
                Draw2DMixerPad();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawManualSliders()
        {
            // Section: Waveform selection
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Waveform Type", EditorStyles.boldLabel);
            GUILayout.Space(4);
            string[] waveNames = { "Square", "Sawtooth", "Sine", "Noise" };
            currentParams.waveType = (WaveType)GUILayout.SelectionGrid((int)currentParams.waveType, waveNames, 4, selectionGridStyle);
            EditorGUILayout.EndVertical();

            // Section: Envelope
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("📬 Envelope (ADSR)", EditorStyles.boldLabel);
            currentParams.attackTime = EditorGUILayout.Slider("Attack Time", currentParams.attackTime, 0f, 1f);
            currentParams.sustainTime = EditorGUILayout.Slider("Sustain Time", currentParams.sustainTime, 0f, 1f);
            currentParams.sustainPunch = EditorGUILayout.Slider("Sustain Punch", currentParams.sustainPunch, 0f, 1f);
            currentParams.decayTime = EditorGUILayout.Slider("Decay Time", currentParams.decayTime, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Frequency
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🎵 Frequency Modulation", EditorStyles.boldLabel);
            currentParams.startFrequency = EditorGUILayout.Slider("Start Frequency", currentParams.startFrequency, 0f, 1f);
            currentParams.minFrequencyCutoff = EditorGUILayout.Slider("Min Cutoff Frequency", currentParams.minFrequencyCutoff, 0f, 1f);
            currentParams.slide = EditorGUILayout.Slider("Slide Speed", currentParams.slide, -1f, 1f);
            currentParams.deltaSlide = EditorGUILayout.Slider("Delta Slide (Acc)", currentParams.deltaSlide, -1f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Vibrato
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("💓 Vibrato Modulation", EditorStyles.boldLabel);
            currentParams.depth = EditorGUILayout.Slider("Vibrato Depth", currentParams.depth, 0f, 1f);
            currentParams.speed = EditorGUILayout.Slider("Vibrato Speed", currentParams.speed, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Arpeggiation
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🛹 Arpeggiation / Jumps", EditorStyles.boldLabel);
            currentParams.frequencyMult = EditorGUILayout.Slider("Pitch Jump Amount", currentParams.frequencyMult, -1f, 1f);
            currentParams.changeSpeed = EditorGUILayout.Slider("Pitch Jump Speed", currentParams.changeSpeed, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Duty Cycle (Square Wave)
            if (currentParams.waveType == WaveType.Square)
            {
                EditorGUILayout.BeginVertical(sectionStyle);
                GUILayout.Label("🔲 Square wave Duty Cycle", EditorStyles.boldLabel);
                currentParams.dutyCycle = EditorGUILayout.Slider("Duty Cycle", currentParams.dutyCycle, 0f, 1f);
                currentParams.dutySweep = EditorGUILayout.Slider("Duty Sweep Speed", currentParams.dutySweep, -1f, 1f);
                EditorGUILayout.EndVertical();
            }

            // Section: Retrigger
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🔁 Retrigger Speed", EditorStyles.boldLabel);
            currentParams.rate = EditorGUILayout.Slider("Retrigger Rate", currentParams.rate, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Flanger
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🌀 Flanger / Phaser Effect", EditorStyles.boldLabel);
            currentParams.offset = EditorGUILayout.Slider("Flanger Offset", currentParams.offset, -1f, 1f);
            currentParams.flangerSweep = EditorGUILayout.Slider("Flanger Sweep Speed", currentParams.flangerSweep, -1f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Low-Pass Filter
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🟢 Low-Pass Filter", EditorStyles.boldLabel);
            currentParams.lpCutoffFrequency = EditorGUILayout.Slider("LP Cutoff Frequency", currentParams.lpCutoffFrequency, 0f, 1f);
            currentParams.lpCutoffSweep = EditorGUILayout.Slider("LP Cutoff Sweep", currentParams.lpCutoffSweep, -1f, 1f);
            currentParams.resonance = EditorGUILayout.Slider("LP Resonance", currentParams.resonance, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: High-Pass Filter
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🔴 High-Pass Filter", EditorStyles.boldLabel);
            currentParams.hpCutoffFrequency = EditorGUILayout.Slider("HP Cutoff Frequency", currentParams.hpCutoffFrequency, 0f, 1f);
            currentParams.hpCutoffSweep = EditorGUILayout.Slider("HP Cutoff Sweep", currentParams.hpCutoffSweep, -1f, 1f);
            EditorGUILayout.EndVertical();
        }

        private void Draw2DMixerPad()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🎯 2D Bilinear Mixer Pad", EditorStyles.boldLabel);
            GUILayout.Label("Drag the cyan cursor to smoothly interpolate procedural parameters in real-time.", EditorStyles.miniLabel);
            GUILayout.Space(8);

            // Fetch a centered 280x280 Rect
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect padRect = GUILayoutUtility.GetRect(280, 280);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Draw visual background frame
            GUI.Box(padRect, "");

            // Corner labels styling
            GUIStyle cornerLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            // Corner labels
            GUI.Label(new Rect(padRect.x + 5, padRect.y + 5, 80, 20), "🟢 LASER", cornerLabelStyle);
            GUI.Label(new Rect(padRect.xMax - 85, padRect.y + 5, 80, 20), "🟡 COIN", cornerLabelStyle);
            GUI.Label(new Rect(padRect.x + 5, padRect.yMax - 25, 80, 20), "🔴 EXPLOSION", cornerLabelStyle);
            GUI.Label(new Rect(padRect.xMax - 85, padRect.yMax - 25, 80, 20), "🔵 JUMP", cornerLabelStyle);

            // Draw center grid guide lines
            Handles.color = new Color(1, 1, 1, 0.15f);
            Handles.DrawLine(new Vector2(padRect.x + padRect.width * 0.5f, padRect.y), new Vector2(padRect.x + padRect.width * 0.5f, padRect.yMax));
            Handles.DrawLine(new Vector2(padRect.x, padRect.y + padRect.height * 0.5f), new Vector2(padRect.xMax, padRect.y + padRect.height * 0.5f));

            // Catch input
            Event ev = Event.current;
            if (padRect.Contains(ev.mousePosition))
            {
                if (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag)
                {
                    padCoordinates.x = Mathf.Clamp01((ev.mousePosition.x - padRect.x) / padRect.width);
                    padCoordinates.y = Mathf.Clamp01((ev.mousePosition.y - padRect.y) / padRect.height);

                    // Interpolate
                    InterpolateParameters(padCoordinates.x, padCoordinates.y);
                    Repaint();
                }
            }

            // Draw cursor crosshair / dot
            Vector2 drawPos = new Vector2(
                padRect.x + padCoordinates.x * padRect.width,
                padRect.y + padCoordinates.y * padRect.height
            );

            Color prevGUIColor = GUI.color;
            GUI.color = Color.cyan;
            // Draw crosshair lines
            GUI.color = new Color(0f, 0.9f, 0.9f, 0.6f);
            GUI.DrawTexture(new Rect(padRect.x, drawPos.y - 0.5f, padRect.width, 1), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(drawPos.x - 0.5f, padRect.y, 1, padRect.height), EditorGUIUtility.whiteTexture);

            // Draw circular cyan handle
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(drawPos.x - 6, drawPos.y - 6, 12, 12), EditorGUIUtility.whiteTexture);
            GUI.color = prevGUIColor;

            GUILayout.Space(12);
            EditorGUILayout.LabelField("Blend Position", $"X: {padCoordinates.x:F3} | Y: {padCoordinates.y:F3}", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();

            // Preset fast generation helpers
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Instant Presets (Seeded)", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔫 Laser")) GenerateFullPreset("laser");
            if (GUILayout.Button("🪙 Coin")) GenerateFullPreset("coin");
            if (GUILayout.Button("💥 Explosion")) GenerateFullPreset("explosion");
            if (GUILayout.Button("🦘 Jump")) GenerateFullPreset("jump");
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void GenerateFullPreset(string typeName)
        {
            SynthEngine.GeneratePreset(currentParams, typeName);
            UpdateJsonTextArea();
            PlayCurrentAudio();
        }

        private void DrawRightColumn()
        {
            GUILayout.Space(10);
            GUILayout.Label("💾 FORMATO Y EXPORTACIÓN", headerStyle);

            // Sound Name Field
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🏷️ Sound Name", EditorStyles.boldLabel);
            currentParams.soundName = EditorGUILayout.TextField("", currentParams.soundName);
            EditorGUILayout.EndVertical();

            // Big Preview Play Button
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("▶️ PLAY PREVIEW", GUILayout.Height(50)))
            {
                PlayCurrentAudio();
            }
            GUI.backgroundColor = originalColor;

            GUILayout.Space(5);

            // Preset local mutator buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🎲 Randomize Settings"))
            {
                currentParams.Randomize();
                UpdateJsonTextArea();
                PlayCurrentAudio();
            }
            if (GUILayout.Button("🧬 Mutate Local (Mutar)"))
            {
                SynthEngine.Mutate(currentParams, 0.06f);
                UpdateJsonTextArea();
                PlayCurrentAudio();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Export format options
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("⚙️ Output WAV Settings", EditorStyles.boldLabel);
            currentParams.sampleRate = (SampleRateOption)EditorGUILayout.EnumPopup("Sample Rate", currentParams.sampleRate);
            currentParams.sampleSize = (SampleSizeOption)EditorGUILayout.EnumPopup("Sample Resolution", currentParams.sampleSize);
            currentParams.masterGain = EditorGUILayout.Slider("Master Gain", currentParams.masterGain, 0f, 1f);

            GUILayout.Space(10);

            if (GUILayout.Button("💾 EXPORT .WAV FILE", GUILayout.Height(35)))
            {
                float[] buffer = SynthEngine.Synthesize(currentParams);
                string savedPath = WavExporter.ExportToWav(buffer, currentParams.sampleRate, currentParams.sampleSize, currentParams.soundName);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    EditorUtility.DisplayDialog("Procedural Synth", $"Saved procedural .wav sound successfully at:\n{savedPath}", "OK");
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Batch & Clipboard Operations
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("📋 JSON Serialization Data", EditorStyles.boldLabel);
            jsonClipboardText = EditorGUILayout.TextArea(jsonClipboardText, GUILayout.Height(100));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔗 Serialize (Copy JSON)"))
            {
                if (isSoundPackLoaded && currentPack != null && currentPack.sounds != null && currentPack.sounds.Count > 0)
                {
                    jsonClipboardText = JsonUtility.ToJson(currentPack, true);
                }
                else
                {
                    jsonClipboardText = JsonUtility.ToJson(currentParams, true);
                }
                GUIUtility.systemCopyBuffer = jsonClipboardText;
                Debug.Log("[ProceduralAudioEditor] Copied sound JSON configuration to clipboard.");
            }
            if (GUILayout.Button("📥 Deserialize (Load JSON)"))
            {
                DeserializeConfig(GUIUtility.systemCopyBuffer);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Bulk export button
            if (isSoundPackLoaded && currentPack != null && currentPack.sounds != null && currentPack.sounds.Count > 0)
            {
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
                if (GUILayout.Button("📦 Export All in Batch (Lote)", GUILayout.Height(40)))
                {
                    int successCount = 0;
                    for (int i = 0; i < currentPack.sounds.Count; i++)
                    {
                        var sound = currentPack.sounds[i];
                        float[] buffer = SynthEngine.Synthesize(sound);
                        string savedPath = WavExporter.ExportToWav(buffer, sound.sampleRate, sound.sampleSize, sound.soundName);
                        if (!string.IsNullOrEmpty(savedPath))
                        {
                            successCount++;
                        }
                    }
                    EditorUtility.DisplayDialog("Batch Export", $"Exported {successCount} / {currentPack.sounds.Count} sounds successfully!", "Awesome");
                }
                GUI.backgroundColor = prevColor;
                GUILayout.Label($"Batch mode active: {currentPack.sounds.Count} sounds loaded in the pack.", EditorStyles.miniBoldLabel);
            }
            else
            {
                GUILayout.Label("Single sound mode active.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void PlayCurrentAudio()
        {
            float[] buffer = SynthEngine.Synthesize(currentParams);
            PlayPreview(buffer, currentParams.sampleRate);
        }

        private void PlayPreview(float[] samples, SampleRateOption rateOption)
        {
            StopPreview();

            AudioClip clip = AudioClip.Create("SynthPreview", samples.Length, 1, (int)rateOption, false);
            clip.SetData(samples, 0);

            Type audioUtilClass = typeof(UnityEditor.AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                // Try finding PlayPreviewClip first, fallback to PlayClip with parameter matching
                System.Reflection.MethodInfo playMethod = audioUtilClass.GetMethod("PlayPreviewClip",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                    null, new Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

                if (playMethod == null)
                {
                    playMethod = audioUtilClass.GetMethod("PlayClip",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                        null, new Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                }

                if (playMethod == null)
                {
                    playMethod = audioUtilClass.GetMethod("PlayPreviewClip",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                }

                if (playMethod == null)
                {
                    playMethod = audioUtilClass.GetMethod("PlayClip",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                }

                if (playMethod != null)
                {
                    var parameters = playMethod.GetParameters();
                    if (parameters.Length == 3)
                    {
                        playMethod.Invoke(null, new object[] { clip, 0, false });
                    }
                    else if (parameters.Length == 1)
                    {
                        playMethod.Invoke(null, new object[] { clip });
                    }
                    else if (parameters.Length == 2)
                    {
                        playMethod.Invoke(null, new object[] { clip, 0 });
                    }
                }
                else
                {
                    Debug.LogWarning("[ProceduralAudioEditor] Could not find editor audio play method in AudioUtil.");
                }
            }
            else
            {
                Debug.LogWarning("[ProceduralAudioEditor] AudioUtil class not found in UnityEditor.");
            }
        }

        private void StopPreview()
        {
            Type audioUtilClass = typeof(UnityEditor.AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                System.Reflection.MethodInfo stopMethod = audioUtilClass.GetMethod("StopAllPreviewClips",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (stopMethod == null)
                {
                    stopMethod = audioUtilClass.GetMethod("StopAllClips",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                }

                if (stopMethod != null)
                {
                    stopMethod.Invoke(null, null);
                }
            }
        }

        private void DeserializeConfig(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[ProceduralAudioEditor] systemCopyBuffer / JSON text is empty.");
                return;
            }

            // Attempt bulk package deserialization first
            try
            {
                SoundPack pack = JsonUtility.FromJson<SoundPack>(json);
                if (pack != null && pack.sounds != null && pack.sounds.Count > 0)
                {
                    currentPack = pack;
                    isSoundPackLoaded = true;
                    // Load first element into sliders
                    currentParams = currentPack.sounds[0].Clone();
                    jsonClipboardText = json;
                    Debug.Log($"[ProceduralAudioEditor] Decoded SoundPack with {currentPack.sounds.Count} sounds.");
                    Repaint();
                    return;
                }
            }
            catch
            {
                // Silence exception to fallback to single sound
            }

            // Fallback to single sound deserialization
            try
            {
                SoundParameters single = JsonUtility.FromJson<SoundParameters>(json);
                if (single != null && !string.IsNullOrEmpty(single.soundName))
                {
                    currentParams = single;
                    isSoundPackLoaded = false;
                    currentPack = new SoundPack();
                    jsonClipboardText = json;
                    Debug.Log("[ProceduralAudioEditor] Decoded single sound settings.");
                    Repaint();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProceduralAudioEditor] Failed to deserialize sound data: {ex.Message}");
            }
        }

        private void UpdateJsonTextArea()
        {
            if (isSoundPackLoaded && currentPack != null && currentPack.sounds != null && currentPack.sounds.Count > 0)
            {
                jsonClipboardText = JsonUtility.ToJson(currentPack, true);
            }
            else
            {
                jsonClipboardText = JsonUtility.ToJson(currentParams, true);
            }
        }

        private void InterpolateParameters(float x, float y)
        {
            if (cornerLaser == null) InitializePadCorners();

            // Bilinear interpolation formula:
            // P(x, y) = Lerp(Lerp(TL, TR, x), Lerp(BL, BR, x), y)
            // where TL = Laser, TR = Coin, BL = Explosion, BR = Jump

            currentParams.waveType = (WaveType)Mathf.RoundToInt(Mathf.Lerp(
                Mathf.Lerp((float)cornerLaser.waveType, (float)cornerCoin.waveType, x),
                Mathf.Lerp((float)cornerExplosion.waveType, (float)cornerJump.waveType, x),
                y
            ));

            currentParams.attackTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.attackTime, cornerCoin.attackTime, x),
                Mathf.Lerp(cornerExplosion.attackTime, cornerJump.attackTime, x),
                y
            );

            currentParams.sustainTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.sustainTime, cornerCoin.sustainTime, x),
                Mathf.Lerp(cornerExplosion.sustainTime, cornerJump.sustainTime, x),
                y
            );

            currentParams.sustainPunch = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.sustainPunch, cornerCoin.sustainPunch, x),
                Mathf.Lerp(cornerExplosion.sustainPunch, cornerJump.sustainPunch, x),
                y
            );

            currentParams.decayTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.decayTime, cornerCoin.decayTime, x),
                Mathf.Lerp(cornerExplosion.decayTime, cornerJump.decayTime, x),
                y
            );

            currentParams.startFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.startFrequency, cornerCoin.startFrequency, x),
                Mathf.Lerp(cornerExplosion.startFrequency, cornerJump.startFrequency, x),
                y
            );

            currentParams.minFrequencyCutoff = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.minFrequencyCutoff, cornerCoin.minFrequencyCutoff, x),
                Mathf.Lerp(cornerExplosion.minFrequencyCutoff, cornerJump.minFrequencyCutoff, x),
                y
            );

            currentParams.slide = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.slide, cornerCoin.slide, x),
                Mathf.Lerp(cornerExplosion.slide, cornerJump.slide, x),
                y
            );

            currentParams.deltaSlide = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.deltaSlide, cornerCoin.deltaSlide, x),
                Mathf.Lerp(cornerExplosion.deltaSlide, cornerJump.deltaSlide, x),
                y
            );

            currentParams.depth = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.depth, cornerCoin.depth, x),
                Mathf.Lerp(cornerExplosion.depth, cornerJump.depth, x),
                y
            );

            currentParams.speed = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.speed, cornerCoin.speed, x),
                Mathf.Lerp(cornerExplosion.speed, cornerJump.speed, x),
                y
            );

            currentParams.frequencyMult = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.frequencyMult, cornerCoin.frequencyMult, x),
                Mathf.Lerp(cornerExplosion.frequencyMult, cornerJump.frequencyMult, x),
                y
            );

            currentParams.changeSpeed = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.changeSpeed, cornerCoin.changeSpeed, x),
                Mathf.Lerp(cornerExplosion.changeSpeed, cornerJump.changeSpeed, x),
                y
            );

            currentParams.dutyCycle = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.dutyCycle, cornerCoin.dutyCycle, x),
                Mathf.Lerp(cornerExplosion.dutyCycle, cornerJump.dutyCycle, x),
                y
            );

            currentParams.dutySweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.dutySweep, cornerCoin.dutySweep, x),
                Mathf.Lerp(cornerExplosion.dutySweep, cornerJump.dutySweep, x),
                y
            );

            currentParams.rate = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.rate, cornerCoin.rate, x),
                Mathf.Lerp(cornerExplosion.rate, cornerJump.rate, x),
                y
            );

            currentParams.offset = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.offset, cornerCoin.offset, x),
                Mathf.Lerp(cornerExplosion.offset, cornerJump.offset, x),
                y
            );

            currentParams.flangerSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.flangerSweep, cornerCoin.flangerSweep, x),
                Mathf.Lerp(cornerExplosion.flangerSweep, cornerJump.flangerSweep, x),
                y
            );

            currentParams.lpCutoffFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.lpCutoffFrequency, cornerCoin.lpCutoffFrequency, x),
                Mathf.Lerp(cornerExplosion.lpCutoffFrequency, cornerJump.lpCutoffFrequency, x),
                y
            );

            currentParams.lpCutoffSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.lpCutoffSweep, cornerCoin.lpCutoffSweep, x),
                Mathf.Lerp(cornerExplosion.lpCutoffSweep, cornerJump.lpCutoffSweep, x),
                y
            );

            currentParams.resonance = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.resonance, cornerCoin.resonance, x),
                Mathf.Lerp(cornerExplosion.resonance, cornerJump.resonance, x),
                y
            );

            currentParams.hpCutoffFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.hpCutoffFrequency, cornerCoin.hpCutoffFrequency, x),
                Mathf.Lerp(cornerExplosion.hpCutoffFrequency, cornerJump.hpCutoffFrequency, x),
                y
            );

            currentParams.hpCutoffSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.hpCutoffSweep, cornerCoin.hpCutoffSweep, x),
                Mathf.Lerp(cornerExplosion.hpCutoffSweep, cornerJump.hpCutoffSweep, x),
                y
            );

            UpdateJsonTextArea();
        }
    }
}
