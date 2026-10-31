#if false // Disabled because official FMOD Unity Integration (Assets/Plugins/FMOD) is installed in the project.
using System;
using UnityEngine;

namespace FMODUnity
{
    [Serializable]
    public struct EventReference
    {
        [SerializeField] public string Path;
        [SerializeField] public string Guid;

        public bool IsNull => string.IsNullOrEmpty(Path) && string.IsNullOrEmpty(Guid);

        public override string ToString() => string.IsNullOrEmpty(Path) ? "[None]" : Path;
    }

    public static class RuntimeManager
    {
        public static FMOD.Studio.StudioSystem StudioSystem => new FMOD.Studio.StudioSystem();

        public static void PlayOneShot(EventReference eventRef, Vector3 position = default)
        {
            // Fallback stub: no-op when FMOD package is not yet imported
        }

        public static FMOD.Studio.EventInstance CreateInstance(EventReference eventRef)
        {
            return new FMOD.Studio.EventInstance();
        }
    }

    public static class RuntimeUtils
    {
        public static FMOD.ATTRIBUTES_3D To3DAttributes(Vector3 position)
        {
            return new FMOD.ATTRIBUTES_3D
            {
                position = new FMOD.VECTOR { x = position.x, y = position.y, z = position.z },
                forward = new FMOD.VECTOR { x = 0, y = 0, z = 1 },
                up = new FMOD.VECTOR { x = 0, y = 1, z = 0 }
            };
        }

        public static FMOD.ATTRIBUTES_3D To3DAttributes(Transform transform)
        {
            Vector3 pos = transform != null ? transform.position : Vector3.zero;
            Vector3 fwd = transform != null ? transform.forward : Vector3.forward;
            Vector3 up = transform != null ? transform.up : Vector3.up;
            return new FMOD.ATTRIBUTES_3D
            {
                position = new FMOD.VECTOR { x = pos.x, y = pos.y, z = pos.z },
                forward = new FMOD.VECTOR { x = fwd.x, y = fwd.y, z = fwd.z },
                up = new FMOD.VECTOR { x = up.x, y = up.y, z = up.z }
            };
        }
    }
}

namespace FMOD.Studio
{
    public struct StudioSystem
    {
        public void setParameterByName(string name, float value, bool ignoreseekspeed = false) {}
    }

    public struct EventInstance
    {
        private bool _active;

        public bool isValid() => _active;
        public void start() { _active = true; }
        public void stop(STOP_MODE mode) { _active = false; }
        public void release() { _active = false; }
        public void setParameterByName(string name, float value, bool ignoreseekspeed = false) {}
        public void set3DAttributes(FMOD.ATTRIBUTES_3D attributes) {}
    }

    public enum STOP_MODE
    {
        ALLOWFADEOUT,
        IMMEDIATE
    }
}

namespace FMOD
{
    public struct ATTRIBUTES_3D
    {
        public VECTOR position;
        public VECTOR velocity;
        public VECTOR forward;
        public VECTOR up;
    }

    public struct VECTOR
    {
        public float x;
        public float y;
        public float z;
    }
}
#endif
