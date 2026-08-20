using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace PicoTowerDefense
{
    /// <summary>
    /// Uses the desktop webcam as a passthrough-style background while leaving the arena in world space.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DesktopMixedRealityPresenter : MonoBehaviour
    {
        private const string BackgroundShaderName = "Hidden/PicoTowerDefense/DesktopCameraBackground";
        private const float CameraStartupTimeout = 8f;

        private readonly List<XRDisplaySubsystem> _displaySubsystems = new();
        private Camera _camera;
        private CameraClearFlags _originalClearFlags;
        private Color _originalBackgroundColor;
        private WebCamTexture _webCamTexture;
        private Material _backgroundMaterial;
        private CommandBuffer _backgroundCommands;
        private Coroutine _startupRoutine;
        private bool _cameraModeActive;
        private bool _wantsCameraMode = true;
        private bool _mirrorHorizontally;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _originalClearFlags = _camera.clearFlags;
            _originalBackgroundColor = _camera.backgroundColor;
        }

        private void Start()
        {
            if (HasRunningXrDisplay())
            {
                enabled = false;
                return;
            }

            _startupRoutine = StartCoroutine(TryStartCameraMode());
        }

        private void Update()
        {
            if (Keyboard.current?.mKey.wasPressedThisFrame ?? false)
            {
                _wantsCameraMode = !_wantsCameraMode;
                if (_wantsCameraMode)
                {
                    if (_startupRoutine == null)
                    {
                        _startupRoutine = StartCoroutine(TryStartCameraMode());
                    }
                }
                else
                {
                    StopCameraMode();
                }
            }

            if (_cameraModeActive)
            {
                UpdateBackgroundTransform();
            }
        }

        private IEnumerator TryStartCameraMode()
        {
            if (_cameraModeActive || !_wantsCameraMode)
            {
                _startupRoutine = null;
                yield break;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("[Spatial Tower Defense] Camera access was not granted. Desktop MR is using the skybox fallback.");
                _wantsCameraMode = false;
                _startupRoutine = null;
                yield break;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[Spatial Tower Defense] No desktop camera was found. Desktop MR is using the skybox fallback.");
                _wantsCameraMode = false;
                _startupRoutine = null;
                yield break;
            }

            WebCamDevice device = devices[0];
            _mirrorHorizontally = device.isFrontFacing;
            _webCamTexture = new WebCamTexture(device.name, 1280, 720, 30);
            _webCamTexture.Play();

            float deadline = Time.realtimeSinceStartup + CameraStartupTimeout;
            while (_webCamTexture != null &&
                   (!_webCamTexture.didUpdateThisFrame || _webCamTexture.width <= 16 || _webCamTexture.height <= 16) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (_webCamTexture == null || _webCamTexture.width <= 16 || _webCamTexture.height <= 16)
            {
                Debug.LogWarning("[Spatial Tower Defense] The desktop camera did not return a frame. Desktop MR is using the skybox fallback.");
                ReleaseWebCam();
                _wantsCameraMode = false;
                _startupRoutine = null;
                yield break;
            }

            Shader shader = Resources.Load<Shader>("DesktopCameraBackground") ?? Shader.Find(BackgroundShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Spatial Tower Defense] Required shader was not found: {BackgroundShaderName}");
                ReleaseWebCam();
                _wantsCameraMode = false;
                _startupRoutine = null;
                yield break;
            }

            _backgroundMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _backgroundCommands = new CommandBuffer
            {
                name = "Desktop mixed-reality camera background"
            };
            _backgroundCommands.Blit(_webCamTexture, BuiltinRenderTextureType.CameraTarget, _backgroundMaterial);
            _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _backgroundCommands);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _cameraModeActive = true;
            UpdateBackgroundTransform();
            Debug.Log($"[Spatial Tower Defense] Desktop MR camera active: {device.name}. Press M to toggle the camera background.");
            _startupRoutine = null;
        }

        private void UpdateBackgroundTransform()
        {
            if (_backgroundMaterial == null || _webCamTexture == null)
            {
                return;
            }

            int rotation = ((_webCamTexture.videoRotationAngle % 360) + 360) % 360;
            bool swapsDimensions = rotation == 90 || rotation == 270;
            float sourceWidth = swapsDimensions ? _webCamTexture.height : _webCamTexture.width;
            float sourceHeight = swapsDimensions ? _webCamTexture.width : _webCamTexture.height;
            float sourceAspect = sourceWidth / Mathf.Max(1f, sourceHeight);
            float viewAspect = _camera.pixelWidth / Mathf.Max(1f, _camera.pixelHeight);
            Vector2 cropScale = Vector2.one;
            if (sourceAspect > viewAspect)
            {
                cropScale.x = viewAspect / sourceAspect;
            }
            else
            {
                cropScale.y = sourceAspect / viewAspect;
            }

            _backgroundMaterial.SetVector("_CropScale", cropScale);
            _backgroundMaterial.SetFloat("_RotationSteps", rotation / 90f);
            _backgroundMaterial.SetFloat("_MirrorX", _mirrorHorizontally ? 1f : 0f);
            _backgroundMaterial.SetFloat("_MirrorY", _webCamTexture.videoVerticallyMirrored ? 1f : 0f);
        }

        private bool HasRunningXrDisplay()
        {
            SubsystemManager.GetSubsystems(_displaySubsystems);
            for (int i = 0; i < _displaySubsystems.Count; i++)
            {
                if (_displaySubsystems[i].running)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopCameraMode()
        {
            if (_startupRoutine != null)
            {
                StopCoroutine(_startupRoutine);
                _startupRoutine = null;
            }

            if (_backgroundCommands != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _backgroundCommands);
                _backgroundCommands.Release();
                _backgroundCommands = null;
            }

            if (_backgroundMaterial != null)
            {
                Destroy(_backgroundMaterial);
                _backgroundMaterial = null;
            }

            ReleaseWebCam();
            _camera.clearFlags = _originalClearFlags;
            _camera.backgroundColor = _originalBackgroundColor;
            _cameraModeActive = false;
        }

        private void ReleaseWebCam()
        {
            if (_webCamTexture == null)
            {
                return;
            }

            if (_webCamTexture.isPlaying)
            {
                _webCamTexture.Stop();
            }

            Destroy(_webCamTexture);
            _webCamTexture = null;
        }

        private void OnDisable()
        {
            StopCameraMode();
        }

        private void OnDestroy()
        {
            StopCameraMode();
        }
    }
}
