using System.Collections.Generic;

namespace SmartPings.Audio;

public interface IAudioDeviceController
{
    public bool IsAudioPlaybackSourceActive { get; }

    public bool AudioPlaybackIsRequested { get; set; }

    public int AudioPlaybackDeviceIndex { get; set; }

    IEnumerable<string> GetAudioPlaybackDevices();

    void ResetAllSfxVolume(float volume);
    void SetSfxVolume(int id, float leftVolume, float rightVolume);

    int PlaySfx(CachedSound sound);
}
