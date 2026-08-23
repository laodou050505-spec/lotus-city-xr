using UnityEngine;

#if ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK
using ByteDance.PICO.XR;
#endif

namespace PicoTowerDefense
{
    /// <summary>
    /// Switches the world-space arena to PICO video-see-through on device.
    /// Desktop and Unity Editor keep the authored skybox so the same scene stays testable without a headset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PicoMixedRealityPresenter : MonoBehaviour
    {
        private Camera _camera;
        private bool _passthroughEnabled = true;
        private Color _opaqueBackground = new(0.70f, 0.71f, 0.67f, 1f);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
            ApplyPresentation();
#endif
        }

        public void ConfigurePassthrough(bool enabled, Color opaqueBackground)
        {
            _passthroughEnabled = enabled;
            _opaqueBackground = new Color(opaqueBackground.r, opaqueBackground.g, opaqueBackground.b, 1f);
#if UNITY_ANDROID && !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
            ApplyPresentation();
#endif
        }

        private void OnApplicationPause(bool paused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
            if (!paused)
            {
                ApplyPresentation();
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
            PXR_Manager.EnableVideoSeeThrough = false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && (ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK)
        private void ApplyPresentation()
        {
            if (_camera != null)
            {
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = _passthroughEnabled
                    ? new Color(0f, 0f, 0f, 0f)
                    : _opaqueBackground;
            }

            // The manager lives beside the XR camera so its eye-camera discovery sees this stereo camera.
            if (GetComponent<PXR_Manager>() == null)
            {
                gameObject.AddComponent<PXR_Manager>();
            }

            PXR_Manager.EnableVideoSeeThrough = _passthroughEnabled;
        }
#endif
    }
}
