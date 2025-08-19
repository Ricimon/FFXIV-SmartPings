using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace SmartPings.Audio;

public class PlaybackChannel
{
    public int Id;
    public required MonoToStereoSampleProvider MonoToStereoSampleProvider { get; set; }
}
