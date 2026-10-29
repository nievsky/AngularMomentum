using FMODUnity;
using FMOD.Studio;
using UnityEngine;

public static class PauseAudioManager
{
    private static EventInstance pausedSnapshot;
    private static bool isActive = false;

    public static void StartSnapshot()
    {
        if (isActive) return;

        pausedSnapshot = RuntimeManager.CreateInstance("snapshot:/Paused");
        pausedSnapshot.start();
        isActive = true;
    }

    public static void StopSnapshot()
    {
        if (!isActive) return;

        if (pausedSnapshot.isValid())
        {
            pausedSnapshot.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            pausedSnapshot.release();
        }

        isActive = false;
    }
}
