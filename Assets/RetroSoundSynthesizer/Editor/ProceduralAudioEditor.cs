using System;
using System.IO;
using System.Collections.Generic;
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

        private CompositeSound currentSound = new CompositeSound();
        private SoundPack currentPack = new SoundPack();
        private bool isSoundPackLoaded = false;
        private AudioClip previewClip;

        // Advanced features state
        private List<CompositeSound> auditionHistory = new List<CompositeSound>();
        private int activeLayerIndex = -1; // -1 = Capa Base (Principal), 0+ = Capas adicionales

        // Editor layout variables
        private int controlMode = 0; // 0 = Sliders Manuales, 1 = Preset Pad / 2D Mixer
        private Vector2 scrollPosLeft;
        private Vector2 scrollPosRight;
        private Vector2 padCoordinates = new Vector2(0.5f, 0.5f);
        private string jsonClipboardText = "";
        private float jsonTextAreaHeight = 120f;
        private Vector2 jsonTextScrollPos;
        private bool isResizingJson = false;

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
            auditionHistory.Clear();
            activeLayerIndex = -1;
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

            EditorGUI.BeginChangeCheck();

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
            scrollPosRight = EditorGUILayout.BeginScrollView(scrollPosRight);
            DrawRightColumn();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateJsonTextArea();
            }
        }

        private void DrawVerticalDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(2, position.height, GUILayout.ExpandHeight(true));
            Color oldColor = GUI.color;
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;
        }

        private SoundParameters GetActiveEditingParams()
        {
            if (currentSound == null) return null;
            if (currentSound.layers == null) currentSound.layers = new List<SoundParameters>();
            if (activeLayerIndex == -1 || activeLayerIndex >= currentSound.layers.Count)
            {
                return currentSound.baseSound;
            }
            return currentSound.layers[activeLayerIndex];
        }

        private void DrawLeftColumn()
        {
            GUILayout.Space(10);
            GUILayout.Label("🎛️ CONTROLES DE SÍNTESIS", headerStyle);

            // =========================================================================
            // GESTIÓN Y SELECCIÓN DE CAPAS (LAYER MIXER)
            // =========================================================================
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🔀 Mezclador de Capas (Sound Layers)", EditorStyles.boldLabel);
            
            if (currentSound.layers == null) currentSound.layers = new List<SoundParameters>();
            int totalLayers = currentSound.layers.Count;
            string[] layerTabs = new string[1 + totalLayers];
            layerTabs[0] = "Capa Base";
            for (int i = 0; i < totalLayers; i++)
            {
                layerTabs[i + 1] = $"Capa #{i + 1} ({currentSound.layers[i].waveType})";
            }

            int selectedTab = activeLayerIndex + 1;
            int newTab = GUILayout.Toolbar(selectedTab, layerTabs, GUILayout.Height(22));
            activeLayerIndex = newTab - 1;

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("➕ Añadir Capa", GUILayout.Height(22)))
            {
                // Clone base sound parameters to provide a beautiful starting point
                SoundParameters newLayer = currentSound.baseSound.Clone();
                newLayer.soundName = $"Capa_{currentSound.layers.Count + 1}";
                newLayer.delay = 0.15f * (currentSound.layers.Count + 1);
                newLayer.masterGain = 0.35f; // slightly quieter as a layer

                currentSound.layers.Add(newLayer);
                activeLayerIndex = currentSound.layers.Count - 1; // select the newly added layer
                UpdateJsonTextArea();
            }

            if (activeLayerIndex >= 0 && currentSound.layers != null && activeLayerIndex < currentSound.layers.Count)
            {
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑️ Eliminar Capa", GUILayout.Height(22)))
                {
                    currentSound.layers.RemoveAt(activeLayerIndex);
                    activeLayerIndex = activeLayerIndex - 1; // fallback to base or previous layer
                    UpdateJsonTextArea();
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndHorizontal();

            // Layer-specific settings slider (Delay and Layer Master Gain)
            if (activeLayerIndex >= 0 && currentSound.layers != null && activeLayerIndex < currentSound.layers.Count)
            {
                GUILayout.Space(6);
                var activeLayer = currentSound.layers[activeLayerIndex];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label($"Ajustes Específicos de Capa #{activeLayerIndex + 1}", EditorStyles.miniBoldLabel);
                activeLayer.delay = EditorGUILayout.Slider("Retardo (Delay en Seg)", activeLayer.delay, 0f, 4f);
                activeLayer.masterGain = EditorGUILayout.Slider("Ganancia de Capa", activeLayer.masterGain, 0f, 1f);
                EditorGUILayout.EndVertical();
            }
            else
            {
                GUILayout.Space(4);
                GUILayout.Label("Editando los parámetros de la Capa Base. Se sumará con las capas añadidas.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            // Tab-Switch control for Synthesis Controls
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
            SoundParameters target = GetActiveEditingParams();
            if (target == null) return;

            // Section: Waveform selection
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Waveform Type", EditorStyles.boldLabel);
            GUILayout.Space(4);
            string[] waveNames = { "Square", "Sawtooth", "Sine", "Noise" };
            target.waveType = (WaveType)GUILayout.SelectionGrid((int)target.waveType, waveNames, 4, selectionGridStyle);
            EditorGUILayout.EndVertical();

            // Section: Envelope
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("📬 Envelope (ADSR)", EditorStyles.boldLabel);
            target.attackTime = EditorGUILayout.Slider("Attack Time", target.attackTime, 0f, 1f);
            target.sustainTime = EditorGUILayout.Slider("Sustain Time", target.sustainTime, 0f, 1f);
            target.sustainPunch = EditorGUILayout.Slider("Sustain Punch", target.sustainPunch, 0f, 1f);
            target.decayTime = EditorGUILayout.Slider("Decay Time", target.decayTime, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Frequency
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🎵 Frequency Modulation", EditorStyles.boldLabel);
            target.startFrequency = EditorGUILayout.Slider("Start Frequency", target.startFrequency, 0f, 1f);
            target.minFrequencyCutoff = EditorGUILayout.Slider("Min Cutoff Frequency", target.minFrequencyCutoff, 0f, 1f);
            target.slide = EditorGUILayout.Slider("Slide Speed", target.slide, -1f, 1f);
            target.deltaSlide = EditorGUILayout.Slider("Delta Slide (Acc)", target.deltaSlide, -1f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Vibrato
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("💓 Vibrato Modulation", EditorStyles.boldLabel);
            target.depth = EditorGUILayout.Slider("Vibrato Depth", target.depth, 0f, 1f);
            target.speed = EditorGUILayout.Slider("Vibrato Speed", target.speed, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Arpeggiation
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🛹 Arpeggiation / Jumps", EditorStyles.boldLabel);
            target.frequencyMult = EditorGUILayout.Slider("Pitch Jump Amount", target.frequencyMult, -1f, 1f);
            target.changeSpeed = EditorGUILayout.Slider("Pitch Jump Speed", target.changeSpeed, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Duty Cycle (Square Wave)
            if (target.waveType == WaveType.Square)
            {
                EditorGUILayout.BeginVertical(sectionStyle);
                GUILayout.Label("🔲 Square wave Duty Cycle", EditorStyles.boldLabel);
                target.dutyCycle = EditorGUILayout.Slider("Duty Cycle", target.dutyCycle, 0f, 1f);
                target.dutySweep = EditorGUILayout.Slider("Duty Sweep Speed", target.dutySweep, -1f, 1f);
                EditorGUILayout.EndVertical();
            }

            // Section: Retrigger
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🔁 Retrigger Speed", EditorStyles.boldLabel);
            target.rate = EditorGUILayout.Slider("Retrigger Rate", target.rate, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Flanger
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🌀 Flanger / Phaser Effect", EditorStyles.boldLabel);
            target.offset = EditorGUILayout.Slider("Flanger Offset", target.offset, -1f, 1f);
            target.flangerSweep = EditorGUILayout.Slider("Flanger Sweep Speed", target.flangerSweep, -1f, 1f);
            EditorGUILayout.EndVertical();

            // Section: Low-Pass Filter
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🟢 Low-Pass Filter", EditorStyles.boldLabel);
            target.lpCutoffFrequency = EditorGUILayout.Slider("LP Cutoff Frequency", target.lpCutoffFrequency, 0f, 1f);
            target.lpCutoffSweep = EditorGUILayout.Slider("LP Cutoff Sweep", target.lpCutoffSweep, -1f, 1f);
            target.resonance = EditorGUILayout.Slider("LP Resonance", target.resonance, 0f, 1f);
            EditorGUILayout.EndVertical();

            // Section: High-Pass Filter
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🔴 High-Pass Filter", EditorStyles.boldLabel);
            target.hpCutoffFrequency = EditorGUILayout.Slider("HP Cutoff Frequency", target.hpCutoffFrequency, 0f, 1f);
            target.hpCutoffSweep = EditorGUILayout.Slider("HP Cutoff Sweep", target.hpCutoffSweep, -1f, 1f);
            EditorGUILayout.EndVertical();

            // Section: LFO Modulation (Advanced LFO)
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🌀 Modulación LFO (Filtros, Tono y Volumen)", EditorStyles.boldLabel);
            target.lfoTarget = (LfoTarget)EditorGUILayout.EnumPopup("LFO Destino (Target)", target.lfoTarget);
            if (target.lfoTarget != LfoTarget.None)
            {
                target.lfoWaveform = (LfoWaveform)EditorGUILayout.EnumPopup("LFO Forma de Onda", target.lfoWaveform);
                target.lfoSpeed = EditorGUILayout.Slider("LFO Velocidad (Speed)", target.lfoSpeed, 0f, 1f);
                target.lfoDepth = EditorGUILayout.Slider("LFO Profundidad (Depth)", target.lfoDepth, 0f, 1f);
            }
            EditorGUILayout.EndVertical();
        }

        private void Draw2DMixerPad()
        {
            SoundParameters target = GetActiveEditingParams();
            if (target == null) return;

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
            SoundParameters target = GetActiveEditingParams();
            SynthEngine.GeneratePreset(target, typeName);
            UpdateJsonTextArea();
            PlayCurrentAudio();
        }

        private void AddToAuditionHistory(CompositeSound cs)
        {
            if (cs == null) return;

            // Check if identical to the last item in the history to avoid duplicate listings
            if (auditionHistory.Count > 0)
            {
                string newJson = JsonUtility.ToJson(cs);
                string lastJson = JsonUtility.ToJson(auditionHistory[auditionHistory.Count - 1]);
                if (newJson == lastJson) return; // ignore duplicates
            }

            auditionHistory.Add(cs.Clone());

            // Limit history to 12 items max
            if (auditionHistory.Count > 12)
            {
                auditionHistory.RemoveAt(0);
            }
        }

        private void DrawRightColumn()
        {
            GUILayout.Space(10);
            GUILayout.Label("💾 FORMATO Y EXPORTACIÓN", headerStyle);

            SoundParameters target = GetActiveEditingParams();

            // Sound Name Field
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("🏷️ Sound Name", EditorStyles.boldLabel);
            currentSound.baseSound.soundName = EditorGUILayout.TextField("", currentSound.baseSound.soundName);
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
                target.Randomize();
                UpdateJsonTextArea();
                PlayCurrentAudio();
            }
            if (GUILayout.Button("🧬 Mutate Local (Mutar)"))
            {
                SynthEngine.Mutate(target, 0.06f);
                UpdateJsonTextArea();
                PlayCurrentAudio();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Audition History Section (Super convenient!)
            if (auditionHistory.Count > 0)
            {
                EditorGUILayout.BeginVertical(sectionStyle);
                GUILayout.Label("📜 Historial de Audición", EditorStyles.boldLabel);
                GUILayout.Label("Haz click para restaurar un sonido previo:", EditorStyles.miniLabel);
                GUILayout.Space(4);

                for (int h = auditionHistory.Count - 1; h >= 0; h--)
                {
                    var histItem = auditionHistory[h];
                    string label = $"{auditionHistory.Count - h}. {histItem.baseSound.soundName} ({histItem.baseSound.waveType})";
                    if (histItem.layers != null && histItem.layers.Count > 0)
                    {
                        label += $" [{histItem.layers.Count + 1} capas]";
                    }
                    if (histItem.baseSound.lfoTarget != LfoTarget.None)
                    {
                        label += " +LFO";
                    }

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(label, EditorStyles.miniButtonLeft, GUILayout.Height(20)))
                    {
                        currentSound = histItem.Clone();
                        activeLayerIndex = -1; // Reset selection to base
                        UpdateJsonTextArea();

                        // Play immediately
                        float[] buffer = SynthEngine.Synthesize(currentSound);
                        PlayPreview(buffer, currentSound.baseSound.sampleRate);
                    }
                    if (GUILayout.Button("🗑️", GUILayout.Width(25), GUILayout.Height(20)))
                    {
                        auditionHistory.RemoveAt(h);
                    }
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button("🧹 Borrar Historial", EditorStyles.miniButton, GUILayout.Height(20)))
                {
                    auditionHistory.Clear();
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }

            // Export format options
            EditorGUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("⚙️ Output WAV Settings", EditorStyles.boldLabel);
            currentSound.baseSound.sampleRate = (SampleRateOption)EditorGUILayout.EnumPopup("Sample Rate", currentSound.baseSound.sampleRate);
            currentSound.baseSound.sampleSize = (SampleSizeOption)EditorGUILayout.EnumPopup("Sample Resolution", currentSound.baseSound.sampleSize);
            currentSound.baseSound.masterGain = EditorGUILayout.Slider("Master Gain", currentSound.baseSound.masterGain, 0f, 1f);

            GUILayout.Space(10);

            if (GUILayout.Button("💾 EXPORT .WAV FILE", GUILayout.Height(35)))
            {
                float[] buffer = SynthEngine.Synthesize(currentSound);
                string savedPath = WavExporter.ExportToWav(buffer, currentSound.baseSound.sampleRate, currentSound.baseSound.sampleSize, currentSound.baseSound.soundName);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    EditorUtility.DisplayDialog("Procedural Synth", $"Saved procedural .wav sound successfully at:\n{savedPath}", "OK");
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Batch & Clipboard Operations
            EditorGUILayout.BeginVertical(sectionStyle);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("📋 JSON Serialization Data", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("↕️ Height", EditorStyles.miniLabel);
            jsonTextAreaHeight = EditorGUILayout.Slider("", jsonTextAreaHeight, 80f, 450f, GUILayout.Width(130));
            GUILayout.EndHorizontal();

            // Scrollable Text Area
            jsonTextScrollPos = EditorGUILayout.BeginScrollView(jsonTextScrollPos, GUILayout.Height(jsonTextAreaHeight));
            jsonClipboardText = EditorGUILayout.TextArea(jsonClipboardText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            // Interactive Draggable Splitter Handle Bar
            Rect splitterRect = GUILayoutUtility.GetRect(10, 8, GUILayout.ExpandWidth(true));
            GUI.Box(new Rect(splitterRect.x, splitterRect.y + 3, splitterRect.width, 2), "");
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && splitterRect.Contains(currentEvent.mousePosition))
            {
                isResizingJson = true;
            }

            if (isResizingJson)
            {
                if (currentEvent.type == EventType.MouseDrag)
                {
                    jsonTextAreaHeight += currentEvent.delta.y;
                    jsonTextAreaHeight = Mathf.Clamp(jsonTextAreaHeight, 80f, 450f);
                    Repaint();
                }
                else if (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp)
                {
                    isResizingJson = false;
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔗 Serialize (Copy JSON)"))
            {
                if (isSoundPackLoaded && currentPack != null && currentPack.sounds != null && currentPack.sounds.Count > 0)
                {
                    jsonClipboardText = JsonUtility.ToJson(currentPack, true);
                }
                else
                {
                    jsonClipboardText = JsonUtility.ToJson(currentSound, true);
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
                        string savedPath = WavExporter.ExportToWav(buffer, sound.baseSound.sampleRate, sound.baseSound.sampleSize, sound.baseSound.soundName);
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
            AddToAuditionHistory(currentSound);
            float[] buffer = SynthEngine.Synthesize(currentSound);
            PlayPreview(buffer, currentSound.baseSound.sampleRate);
        }

        private void PlayPreview(float[] samples, SampleRateOption rateOption)
        {
            StopPreview();

            // 1. Export the procedural samples to a temporary WAV asset
            string tempWavPath = WavExporter.ExportToWav(samples, rateOption, SampleSizeOption.Bit16, "~temp_preview");
            if (string.IsNullOrEmpty(tempWavPath)) return;

            // 2. Load the imported AudioClip asset (forcing an import refresh if needed)
            AssetDatabase.ImportAsset(tempWavPath);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(tempWavPath);

            if (clip != null)
            {
                previewClip = clip;

                Type audioUtilClass = typeof(UnityEditor.AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                if (audioUtilClass != null)
                {
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
                            playMethod.Invoke(null, new object[] { previewClip, 0, false });
                        }
                        else if (parameters.Length == 1)
                        {
                            playMethod.Invoke(null, new object[] { previewClip });
                        }
                        else if (parameters.Length == 2)
                        {
                            playMethod.Invoke(null, new object[] { previewClip, 0 });
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ProceduralAudioEditor] Temporary preview AudioClip could not be loaded.");
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

            previewClip = null;
        }

        private void OnDisable()
        {
            StopPreview();
            
            // Delete temporary preview file on disable/close to keep project assets clean
            string tempWavPath = "Assets/~temp_preview.wav";
            if (File.Exists(Path.Combine(Application.dataPath, "~temp_preview.wav")))
            {
                AssetDatabase.DeleteAsset(tempWavPath);
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
                    currentSound = currentPack.sounds[0].Clone();
                    jsonClipboardText = json;
                    activeLayerIndex = -1; // Reset active layer selection
                    Debug.Log($"[ProceduralAudioEditor] Decoded SoundPack with {currentPack.sounds.Count} sounds.");
                    Repaint();
                    return;
                }
            }
            catch
            {
                // Silence exception to fallback to single sound
            }

            // Attempt layered composite sound deserialization
            try
            {
                CompositeSound layered = JsonUtility.FromJson<CompositeSound>(json);
                if (layered != null && layered.baseSound != null && !string.IsNullOrEmpty(layered.baseSound.soundName))
                {
                    currentSound = layered;
                    isSoundPackLoaded = false;
                    currentPack = new SoundPack();
                    jsonClipboardText = json;
                    activeLayerIndex = -1; // Reset active layer selection
                    Debug.Log("[ProceduralAudioEditor] Decoded layered composite sound settings.");
                    Repaint();
                    return;
                }
            }
            catch
            {
                // Silence exception to fallback to legacy single sound
            }

            // Fallback to legacy single sound deserialization (Backwards Compatibility)
            try
            {
                SoundParameters single = JsonUtility.FromJson<SoundParameters>(json);
                if (single != null && !string.IsNullOrEmpty(single.soundName))
                {
                    currentSound = new CompositeSound { baseSound = single, layers = new List<SoundParameters>() };
                    isSoundPackLoaded = false;
                    currentPack = new SoundPack();
                    jsonClipboardText = json;
                    activeLayerIndex = -1; // Reset active layer selection
                    Debug.Log("[ProceduralAudioEditor] Decoded single sound settings into base layer.");
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
                jsonClipboardText = JsonUtility.ToJson(currentSound, true);
            }
        }

        private void InterpolateParameters(float x, float y)
        {
            if (cornerLaser == null) InitializePadCorners();

            SoundParameters target = GetActiveEditingParams();
            if (target == null) return;

            // Bilinear interpolation formula:
            // P(x, y) = Lerp(Lerp(TL, TR, x), Lerp(BL, BR, x), y)
            // where TL = Laser, TR = Coin, BL = Explosion, BR = Jump

            target.waveType = (WaveType)Mathf.RoundToInt(Mathf.Lerp(
                Mathf.Lerp((float)cornerLaser.waveType, (float)cornerCoin.waveType, x),
                Mathf.Lerp((float)cornerExplosion.waveType, (float)cornerJump.waveType, x),
                y
            ));

            target.attackTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.attackTime, cornerCoin.attackTime, x),
                Mathf.Lerp(cornerExplosion.attackTime, cornerJump.attackTime, x),
                y
            );

            target.sustainTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.sustainTime, cornerCoin.sustainTime, x),
                Mathf.Lerp(cornerExplosion.sustainTime, cornerJump.sustainTime, x),
                y
            );

            target.sustainPunch = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.sustainPunch, cornerCoin.sustainPunch, x),
                Mathf.Lerp(cornerExplosion.sustainPunch, cornerJump.sustainPunch, x),
                y
            );

            target.decayTime = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.decayTime, cornerCoin.decayTime, x),
                Mathf.Lerp(cornerExplosion.decayTime, cornerJump.decayTime, x),
                y
            );

            target.startFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.startFrequency, cornerCoin.startFrequency, x),
                Mathf.Lerp(cornerExplosion.startFrequency, cornerJump.startFrequency, x),
                y
            );

            target.minFrequencyCutoff = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.minFrequencyCutoff, cornerCoin.minFrequencyCutoff, x),
                Mathf.Lerp(cornerExplosion.minFrequencyCutoff, cornerJump.minFrequencyCutoff, x),
                y
            );

            target.slide = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.slide, cornerCoin.slide, x),
                Mathf.Lerp(cornerExplosion.slide, cornerJump.slide, x),
                y
            );

            target.deltaSlide = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.deltaSlide, cornerCoin.deltaSlide, x),
                Mathf.Lerp(cornerExplosion.deltaSlide, cornerJump.deltaSlide, x),
                y
            );

            target.depth = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.depth, cornerCoin.depth, x),
                Mathf.Lerp(cornerExplosion.depth, cornerJump.depth, x),
                y
            );

            target.speed = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.speed, cornerCoin.speed, x),
                Mathf.Lerp(cornerExplosion.speed, cornerJump.speed, x),
                y
            );

            target.frequencyMult = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.frequencyMult, cornerCoin.frequencyMult, x),
                Mathf.Lerp(cornerExplosion.frequencyMult, cornerJump.frequencyMult, x),
                y
            );

            target.changeSpeed = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.changeSpeed, cornerCoin.changeSpeed, x),
                Mathf.Lerp(cornerExplosion.changeSpeed, cornerJump.changeSpeed, x),
                y
            );

            target.dutyCycle = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.dutyCycle, cornerCoin.dutyCycle, x),
                Mathf.Lerp(cornerExplosion.dutyCycle, cornerJump.dutyCycle, x),
                y
            );

            target.dutySweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.dutySweep, cornerCoin.dutySweep, x),
                Mathf.Lerp(cornerExplosion.dutySweep, cornerJump.dutySweep, x),
                y
            );

            target.rate = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.rate, cornerCoin.rate, x),
                Mathf.Lerp(cornerExplosion.rate, cornerJump.rate, x),
                y
            );

            target.offset = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.offset, cornerCoin.offset, x),
                Mathf.Lerp(cornerExplosion.offset, cornerJump.offset, x),
                y
            );

            target.flangerSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.flangerSweep, cornerCoin.flangerSweep, x),
                Mathf.Lerp(cornerExplosion.flangerSweep, cornerJump.flangerSweep, x),
                y
            );

            target.lpCutoffFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.lpCutoffFrequency, cornerCoin.lpCutoffFrequency, x),
                Mathf.Lerp(cornerExplosion.lpCutoffFrequency, cornerJump.lpCutoffFrequency, x),
                y
            );

            target.lpCutoffSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.lpCutoffSweep, cornerCoin.lpCutoffSweep, x),
                Mathf.Lerp(cornerExplosion.lpCutoffSweep, cornerJump.lpCutoffSweep, x),
                y
            );

            target.resonance = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.resonance, cornerCoin.resonance, x),
                Mathf.Lerp(cornerExplosion.resonance, cornerJump.resonance, x),
                y
            );

            target.hpCutoffFrequency = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.hpCutoffFrequency, cornerCoin.hpCutoffFrequency, x),
                Mathf.Lerp(cornerExplosion.hpCutoffFrequency, cornerJump.hpCutoffFrequency, x),
                y
            );

            target.hpCutoffSweep = Mathf.Lerp(
                Mathf.Lerp(cornerLaser.hpCutoffSweep, cornerCoin.hpCutoffSweep, x),
                Mathf.Lerp(cornerExplosion.hpCutoffSweep, cornerJump.hpCutoffSweep, x),
                y
            );

            UpdateJsonTextArea();
        }
    }
}
