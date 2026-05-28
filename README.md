# Retro Sound Synthesizer

An offline, local, procedural 8/16-bit sound synthesis package for Unity based on the classic sfxr and jsfxr architectures. This package allows you to visually design, mutate, and mix vintage retro sound effects directly within the Unity Editor. It is fully serializable in a clean JSON format, making it perfectly suited for manual tuning, version control, or AI-assisted sound generation.

## Features

- Waveform Oscillators: Square, Sawtooth, Sine, and Noise generators.
- Retro Synthesizer Chain: ADSR envelope, pitch modulation, vibrato, arpeggiator, flanger/phaser effect, and high-pass/low-pass filters.
- Advanced LFO Modulation: Real-time modulation of Pitch, Filter Cutoff, Duty Cycle, or Volume using Sine, Triangle, Square, or Sawtooth LFO waveforms.
- Multi-Layer Mixing: Sum a base sound and multiple sub-layers with customizable delay offsets and volume levels to create complex retro effects.
- Audition History: A local session history stack of up to 12 generated sounds, allowing you to instantly compare, restore, and play prior configurations.
- 2D Bilinear Morph Pad: Drag a cursor between 4 seeded preset corners (Laser, Coin, Explosion, Jump) to dynamically blend and discover new sounds.
- Wave Exporter: Linear downsampling from 44.1kHz and 8/16-bit PCM wave formatting, saving assets directly into the Unity project directory.
- Deep JSON Serialization: Fully serializable data model (CompositeSound) allowing simple clipboard copying, loading, and generation via external artificial intelligence tools.

## Installation

### Installation via Unity Package Manager (Recommended)

1. Open your Unity project.
2. Open the Package Manager window by going to Window -> Package Manager.
3. Click the '+' button in the top-left corner of the window.
4. Select "Add package from git URL...".
5. Enter the following URL and click Add:
   `https://github.com/gabrieloide/Retro-Sound-Synthesizer.git#upm`

### Manual Installation

1. Clone or download this repository.
2. Copy the `Assets/RetroSoundSynthesizer` directory into your project's `Assets` or `Packages` folder.

## Editor Window Guide

To open the synthesizer editor, go to Tools -> Procedural Audio Synthesizer in the top menu of the Unity Editor.

### Synthesis Controls (Left Column)

- Layering Mixer: Manage multiple layers. Use the tabs to select which layer to edit. You can add layers, remove the selected layer, and adjust the delay offset or gain of specific sub-layers.
- Manual Sliders: Manually adjust individual synthesis parameters (envelope, frequency, LFO, filters, etc.). The sliders automatically adapt to whichever layer is currently selected in the Layering Mixer.
- 2D Bilinear Mixer Pad: Visually interpolate parameters in real-time by dragging the cyan handle between the four corners representing classic seeded templates.

### Format and Export (Right Column)

- Audition Buttons: Use the Play Preview button to synthesize and listen to the composite sound. The editor will automatically compile and sum the base sound and its layers.
- Mutation Controls: Randomize settings or subtly mutate parameters of the currently selected layer to quickly discover sound variations.
- Audition History: Clicking on any entry in the scrollable history stack will restore all parameters of the recorded sound and play it immediately.
- WAV Exporter: Enter a custom asset name, choose the sample rate (8kHz, 11kHz, 22kHz, 44kHz), choose the bit resolution (8-bit or 16-bit), and click Export to save the physical WAV asset in your project.
- JSON Serialization: Copy the clean JSON string to your clipboard for external use or paste a JSON configuration to deserialize it instantly.

## JSON Data Model

Layered sounds are structured in a flat, non-recursive format to prevent Unity serialization cycle issues. This structure is highly readable and perfect for AI generation:

```json
{
  "baseSound": {
    "soundName": "sci_fi_laser",
    "waveType": 0,
    "attackTime": 0.0,
    "sustainTime": 0.12,
    "decayTime": 0.25,
    "startFrequency": 0.65,
    "slide": -0.32,
    "lfoTarget": 1,
    "lfoWaveform": 0,
    "lfoSpeed": 0.45,
    "lfoDepth": 0.5
  },
  "layers": [
    {
      "soundName": "sub_noise_impact",
      "waveType": 3,
      "attackTime": 0.0,
      "sustainTime": 0.05,
      "decayTime": 0.08,
      "startFrequency": 0.2,
      "delay": 0.0,
      "masterGain": 0.35
    }
  ]
}
```

## License

This project is licensed under the Apache License 2.0. See the LICENSE.md file for full details and credits.
