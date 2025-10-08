using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SmartPings.Log;

namespace SmartPings.Audio;

public sealed class AudioDeviceController : IAudioDeviceController, IDisposable
{
    public bool IsAudioPlaybackSourceActive => AudioPlaybackIsRequested;

    public bool AudioPlaybackIsRequested
    {
        get => this.audioPlaybackIsRequested;
        set
        {
            this.audioPlaybackIsRequested = value;
            UpdateSourceStates();
        }
    }
    private bool audioPlaybackIsRequested;

    public int AudioPlaybackDeviceIndex
    {
        get => this.audioPlaybackDeviceIndex;
        set
        {
            if (this.audioPlaybackDeviceIndex != value)
            {
                this.audioPlaybackDeviceIndex = value;
                this.configuration.SelectedAudioOutputDeviceIndex = value;
                this.configuration.Save();

                DisposeAudioPlaybackSource();
                UpdateSourceStates();
            }
        }
    }
    private int audioPlaybackDeviceIndex;

    private WaveOutEvent? audioPlaybackSource;
    private bool playingBack;
    private int lastSfxId;

    private const int SampleRate = 48000;
    private const int WaveOutDesiredLatency = 100;
    private const int WaveOutNumberOfBuffers = 5;

    private readonly WaveFormat waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
    private readonly MixingSampleProvider outputSampleProvider;
    private readonly SortedList<int, MonoToStereoSampleProvider> sfxProviders = [];
    private readonly Configuration configuration;
    private readonly ILogger logger;

    public AudioDeviceController(Configuration configuration, ILogger logger)
    {
        this.configuration = configuration;
        this.logger = logger;

        this.audioPlaybackDeviceIndex = configuration.SelectedAudioOutputDeviceIndex;

        this.outputSampleProvider = new(this.waveFormat)
        {
            ReadFully = true,
        };
        this.outputSampleProvider.MixerInputEnded += OnMixerInputEnded;
    }

    public void Dispose()
    {
        DisposeAudioPlaybackSource();
        this.outputSampleProvider.MixerInputEnded -= OnMixerInputEnded;
    }

    public IEnumerable<string> GetAudioPlaybackDevices()
    {
        for (int n = -1; n < WaveOut.DeviceCount; n++)
        {
            var caps = WaveOut.GetCapabilities(n);
            if (n == -1)
            {
                yield return "Default";
            }
            else
            {
                yield return caps.ProductName;
            }
        }
    }

    public void ResetAllSfxVolume(float volume)
    {
        foreach(var provider in this.sfxProviders.Values)
        {
            provider.LeftVolume = volume;
            provider.RightVolume = volume;
        }
    }

    public void SetSfxVolume(int id, float leftVolume, float rightVolume)
    {
        if (this.sfxProviders.TryGetValue(id, out var provider))
        {
            provider.LeftVolume = leftVolume;
            provider.RightVolume = rightVolume;
        }
    }

    /// <summary>
    /// Returns an ID associated with the sound
    /// </summary>
    public int PlaySfx(CachedSound sound)
    {
        while (sfxProviders.Count > 0 && sfxProviders.Count >= this.configuration.MaxConcurrentSfx)
        {
            var first = this.sfxProviders.Values[0];
            this.outputSampleProvider.RemoveMixerInput(first);
            this.sfxProviders.RemoveAt(0);
        }
        var sfxProvider = new MonoToStereoSampleProvider(new CachedSoundSampleProvider(sound));
        this.outputSampleProvider.AddMixerInput(sfxProvider);
        this.lastSfxId++;
        // Leave 0 as unassigned
        if (this.lastSfxId == default) { lastSfxId++; }
        sfxProviders.Add(this.lastSfxId, sfxProvider);
        return this.lastSfxId;
    }

    private WaveOutEvent? GetAudioPlaybackSource(bool createIfNull)
    {
        if (this.audioPlaybackSource == null && createIfNull)
        {
            this.audioPlaybackSource = new WaveOutEvent
            {
                DeviceNumber = this.AudioPlaybackDeviceIndex,
                DesiredLatency = WaveOutDesiredLatency,
                NumberOfBuffers = WaveOutNumberOfBuffers,
            };
            this.audioPlaybackSource.PlaybackStopped += (sender, e) =>
            {
                this.playingBack = false;
            };
            this.audioPlaybackSource.Init(this.outputSampleProvider);

            this.playingBack = false;
        }
        return this.audioPlaybackSource;
    }

    private void DisposeAudioPlaybackSource()
    {
        if (this.audioPlaybackSource != null)
        {
            this.audioPlaybackSource.Dispose();
            this.audioPlaybackSource = null;
        }
    }

    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
    {
        for (var i = 0; i < this.sfxProviders.Count; i++)
        {
            if (this.sfxProviders.Values[i] == e.SampleProvider)
            {
                this.sfxProviders.RemoveAt(i);
            }
        }
    }

    private void UpdateSourceStates()
    {
        if (this.IsAudioPlaybackSourceActive)
        {
            if (!this.playingBack)
            {
                this.logger.Debug("Starting audio playback source from device {0}", GetAudioPlaybackSource(true)!.DeviceNumber);
                GetAudioPlaybackSource(true)!.Play();
                this.playingBack = true;
            }
        }
        else
        {
            var playbackSource = GetAudioPlaybackSource(false);
            if (playbackSource != null)
            {
                this.logger.Debug("Stopping audio playback source from device {0}", playbackSource.DeviceNumber);
                playbackSource.Stop();
            }
            this.outputSampleProvider.RemoveAllMixerInputs();
            this.sfxProviders.Clear();
            this.playingBack = false;
        }
    }
}
