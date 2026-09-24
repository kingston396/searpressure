// Compile-check only: the desktop reference assemblies leave out this mobile-only class.
#if UNITY_ANDROID
namespace UnityEngine { public static class Handheld { public static void Vibrate() { } } }
#endif
