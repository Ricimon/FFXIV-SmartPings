using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Dalamud.Plugin.Services;
using SmartPings.Log;

namespace SmartPings.Audio;

public class Spatializer : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly Configuration configuration;
    private readonly IAudioDeviceController audioDeviceController;
    private readonly GroundPingPresenter groundPingPresenter;
    private readonly ILogger logger;

    private readonly PeriodicTimer updateTimer = new(TimeSpan.FromMilliseconds(100));
    private readonly SemaphoreSlim frameworkThreadSemaphore = new(1, 1);

    private bool isDisposed;

    public Spatializer(IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        Configuration configuration,
        IAudioDeviceController audioDeviceController,
        GroundPingPresenter groundPingPresenter,
        ILogger logger)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.configuration = configuration;
        this.audioDeviceController = audioDeviceController;
        this.groundPingPresenter = groundPingPresenter;
        this.logger = logger;
    }

    public void StartUpdateLoop()
    {
        Task.Run(async delegate
        {
            while (await this.updateTimer.WaitForNextTickAsync())
            {
                await frameworkThreadSemaphore.WaitAsync();
                if (isDisposed)
                {
                    return;
                }
                this.framework.RunOnFrameworkThread(UpdatePingVolumes).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
            }
        }).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
    }

    public void UpdatePingVolume(GroundPing ping)
    {
        CalculateSpatialValues(ping.WorldPosition,
            out var leftVolume, out var rightVolume, out var distance, out var volume);
        leftVolume *= this.configuration.MasterVolume;
        rightVolume *= this.configuration.MasterVolume;
        this.audioDeviceController.SetSfxVolume(ping.SfxId, leftVolume, rightVolume);
    }

    public void Dispose()
    {
        isDisposed = true;
        this.updateTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private void UpdatePingVolumes()
    {
        try
        {
            foreach (var ping in this.groundPingPresenter.GroundPings)
            {
                UpdatePingVolume(ping);
            }
        }
        finally
        {
            this.frameworkThreadSemaphore.Release();
        }
    }

    private void CalculateSpatialValues(
        Vector3 position,
        out float leftVolume,
        out float rightVolume,
        out float distance,
        out float volume)
    {
        Vector3 toTarget;
        if (this.clientState.LocalPlayer != null)
        {
            toTarget = position - this.clientState.LocalPlayer.Position;
        }
        else
        {
            toTarget = Vector3.Zero;
        }
        distance = toTarget.Length();

        volume = leftVolume = rightVolume = CalculateVolume(distance);

        if (this.configuration.EnableSpatialization)
        {
            SpatializeVolume(volume, toTarget, out leftVolume, out rightVolume);
        }
    }

    private float CalculateVolume(float distance)
    {
        return 1;
        //var minDistance = this.configuration.FalloffModel.MinimumDistance;
        //var maxDistance = this.configuration.FalloffModel.MaximumDistance;
        //var falloffFactor = this.configuration.FalloffModel.FalloffFactor;
        //float volume;
        //try
        //{
        //    float scale;
        //    switch (this.configuration.FalloffModel.Type)
        //    {
        //        case AudioFalloffModel.FalloffType.None:
        //            volume = 1.0f;
        //            break;
        //        case AudioFalloffModel.FalloffType.InverseDistance:
        //            distance = Math.Clamp(distance, minDistance, maxDistance);
        //            scale = MathF.Pow((maxDistance - distance) / (maxDistance - minDistance), distance / maxDistance);
        //            volume = minDistance / (minDistance + falloffFactor * (distance - minDistance)) * scale;
        //            break;
        //        case AudioFalloffModel.FalloffType.ExponentialDistance:
        //            distance = Math.Clamp(distance, minDistance, maxDistance);
        //            scale = MathF.Pow((maxDistance - distance) / (maxDistance - minDistance), distance / maxDistance);
        //            volume = MathF.Pow(distance / minDistance, -falloffFactor) * scale;
        //            break;
        //        case AudioFalloffModel.FalloffType.LinearDistance:
        //            distance = Math.Clamp(distance, minDistance, maxDistance);
        //            volume = 1 - falloffFactor * (distance - minDistance) / (maxDistance - minDistance);
        //            break;
        //        default:
        //            volume = 1.0f;
        //            break;
        //    }
        //}
        //catch (Exception e) when (e is DivideByZeroException or ArgumentException)
        //{
        //    volume = 1.0f;
        //}
        //volume = Math.Clamp(volume, 0.0f, 1.0f);
        //return volume;
    }

    private void SpatializeVolume(float volume, Vector3 toTarget, out float leftVolume, out float rightVolume)
    {
        leftVolume = rightVolume = volume;
        var distance = toTarget.Length();
        if (volume == 0 || distance == 0)
        {
            return;
        }

        var lookAtVector = Vector3.Zero;
        unsafe
        {
            // https://github.com/NotNite/Linkpearl/blob/main/Linkpearl/Plugin.cs
            var renderingCamera = *FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera;
            lookAtVector = new Vector3(renderingCamera.ViewMatrix.M13, renderingCamera.ViewMatrix.M23, renderingCamera.ViewMatrix.M33);
        }

        if (lookAtVector.LengthSquared() == 0)
        {
            return;
        }

        var toTargetHorizontal = Vector3.Normalize(new Vector3(toTarget.X, 0, toTarget.Z));
        var cameraForwardHorizontal = Vector3.Normalize(new Vector3(lookAtVector.X, 0, lookAtVector.Z));
        var dot = Vector3.Dot(toTargetHorizontal, cameraForwardHorizontal);
        var cross = Vector3.Dot(Vector3.Cross(toTargetHorizontal, cameraForwardHorizontal), Vector3.UnitY);
        var cosine = -dot;
        var sine = cross;
        var angle = MathF.Acos(cosine);
        if (float.IsNaN(angle)) { angle = 0f; }
        if (sine < 0) { angle = -angle; }

        var minDistance = this.configuration.SpatializationMinimumDistance;
        float pan = 1;
        if (minDistance > 0 && distance < minDistance)
        {
            // Linear pan dropoff
            pan = distance / minDistance;
            // Arc pan dropoff
            //var d = distance / minDistance;
            //pan = 1 - MathF.Sqrt(1 - d * d);
        }
        pan = Math.Abs(pan * MathF.Sin(angle));
        if (angle > 0)
        {
            // Left of player
            rightVolume *= 1 - pan;
        }
        else
        {
            // Right of player
            leftVolume *= 1 - pan;
        }
    }
}
