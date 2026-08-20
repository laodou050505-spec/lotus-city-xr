using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace PicoTowerDefense
{
    public sealed class SpatialInputRig : MonoBehaviour
    {
        private const float DefaultDesktopYaw = 0f;
        private const float DefaultDesktopPitch = 25f;
        private const float DefaultDesktopDistance = 2.65f;
        private const float DesktopDragThreshold = 6f;
        private const float TriggerPressThreshold = 0.55f;
        private const float TriggerReleaseThreshold = 0.35f;

        private readonly List<XRNodeState> _nodeStates = new();
        private readonly List<XRInputSubsystem> _inputSubsystems = new();
        private Camera _camera;
        private Transform _leftControllerVisual;
        private Transform _rightControllerVisual;
        private LineRenderer _interactionLine;
        private LineRenderer _attackLine;
        private bool _leftTracked;
        private bool _rightTracked;
        private bool _floorOriginSet;
        private bool _interactionTriggerWasPressed;
        private bool _attackPressed;
        private bool _confirmDown;
        private bool _confirmUp;
        private bool _confirmReleaseWasDrag;
        private bool _interactionDragActive;
        private bool _recenterWasPressed;
        private bool _recenterDown;
        private bool _cycleWasPressed;
        private bool _cycleDown;
        private bool _startWaveWasPressed;
        private bool _startWaveDown;
        private bool _desktopPrimaryWasPressed;
        private bool _desktopPrimaryDragged;
        private Vector3 _desktopPressPosition;
        private Vector3 _lastMousePosition;
        private float _desktopYaw = DefaultDesktopYaw;
        private float _desktopPitch = DefaultDesktopPitch;
        private float _desktopDistance = DefaultDesktopDistance;
        private float _desktopViewScale = 1f;
        private Vector3 _desktopTarget = new(0f, GameDefinitions.TableHeight, 0f);

        public Camera Camera => _camera;
        public bool HasTrackedHead { get; private set; }
        public Vector3 HeadPosition { get; private set; }
        public Quaternion HeadRotation { get; private set; }

        public void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Mild supersampling keeps small world-space labels crisp on PICO
            // without the much larger cost of doubling the eye texture.
            XRSettings.eyeTextureResolutionScale = Mathf.Max(XRSettings.eyeTextureResolutionScale, 1.15f);
#endif
            var cameraObject = new GameObject("XR Head Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 50f;
            _camera.fieldOfView = 55f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
            _camera.allowDynamicResolution = false;
            _camera.allowMSAA = true;
            _camera.allowHDR = true;
            _camera.useOcclusionCulling = true;
            _camera.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraObject.AddComponent<AudioListener>();
#if UNITY_ANDROID && !UNITY_EDITOR
            cameraObject.AddComponent<PicoMixedRealityPresenter>();
#else
            cameraObject.AddComponent<DesktopMixedRealityPresenter>();
#endif

            Material controllerMaterial = ProceduralFactory.CreateMaterial(new Color(0.15f, 0.22f, 0.34f), 0.35f, 0.45f);
            _leftControllerVisual = new GameObject("Left PICO Controller").transform;
            _leftControllerVisual.gameObject.layer = 2;
            _leftControllerVisual.SetParent(transform, false);
            if (!TryAttachOfficialControllerModel(_leftControllerVisual, "Prefabs/LeftControllerModel"))
            {
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Cylinder,
                    "Left Controller Fallback",
                    _leftControllerVisual,
                    new Vector3(0f, 0f, -0.035f),
                    new Vector3(0.018f, 0.055f, 0.018f),
                    controllerMaterial,
                    Quaternion.Euler(90f, 0f, 0f));
            }

            _interactionLine = _leftControllerVisual.gameObject.AddComponent<LineRenderer>();
            ConfigureControllerLine(_interactionLine, new Color(0.35f, 0.88f, 1f, 0.9f));

            _rightControllerVisual = new GameObject("Right Controller").transform;
            _rightControllerVisual.gameObject.layer = 2;
            _rightControllerVisual.SetParent(transform, false);
            if (!TryAttachOfficialControllerModel(_rightControllerVisual, "Prefabs/RightControllerModel"))
            {
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Cylinder,
                    "Right Controller Fallback",
                    _rightControllerVisual,
                    new Vector3(0f, 0f, -0.035f),
                    new Vector3(0.018f, 0.055f, 0.018f),
                    controllerMaterial,
                    Quaternion.Euler(90f, 0f, 0f));
            }
            Material weaponMaterial = ProceduralFactory.CreateMaterial(new Color(0.93f, 0.69f, 0.24f), 0.45f, 0.72f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Keeper Purification Wand",
                _rightControllerVisual,
                new Vector3(0f, 0f, 0.035f),
                new Vector3(0.024f, 0.085f, 0.024f),
                weaponMaterial,
                Quaternion.Euler(90f, 0f, 0f));

            _attackLine = _rightControllerVisual.gameObject.AddComponent<LineRenderer>();
            ConfigureControllerLine(_attackLine, new Color(1f, 0.72f, 0.24f, 0.95f));

            _lastMousePosition = DesktopMousePosition();
            UpdateDesktopCamera();
        }

        public void Tick()
        {
            RequestFloorTrackingOrigin();
            ReadTrackedPoses();
            ReadButtons();
            if (!HasTrackedHead)
            {
                UpdateDesktopOrbit();
            }
        }

        public Ray AimRay()
        {
            if (_leftTracked)
            {
                return new Ray(_leftControllerVisual.position, _leftControllerVisual.forward);
            }

            return _camera.ScreenPointToRay(DesktopMousePosition());
        }

        public Ray AttackRay()
        {
            if (_rightTracked)
            {
                return new Ray(_rightControllerVisual.position, _rightControllerVisual.forward);
            }

            return _camera.ScreenPointToRay(DesktopMousePosition());
        }

        public bool IsAttackPressed => _attackPressed;

        public bool ConsumeConfirmDown()
        {
            bool value = _confirmDown;
            _confirmDown = false;
            return value;
        }

        public bool ConsumeConfirmUp(out bool wasPointerDrag)
        {
            bool value = _confirmUp;
            wasPointerDrag = value && _confirmReleaseWasDrag;
            _confirmUp = false;
            _confirmReleaseWasDrag = false;
            return value;
        }

        public void SetInteractionDragActive(bool active)
        {
            _interactionDragActive = active;
        }

        public bool ConsumeRecenterDown()
        {
            bool value = _recenterDown;
            _recenterDown = false;
            return value;
        }

        public bool ConsumeCycleTowerDown()
        {
            bool value = _cycleDown;
            _cycleDown = false;
            return value;
        }

        public bool ConsumeStartWaveDown()
        {
            bool value = _startWaveDown;
            _startWaveDown = false;
            return value;
        }

        public void ResetDesktopView()
        {
            _desktopYaw = DefaultDesktopYaw;
            _desktopPitch = DefaultDesktopPitch;
            _desktopDistance = DefaultDesktopDistance * _desktopViewScale;
            _lastMousePosition = DesktopMousePosition();
            UpdateDesktopCamera();
        }

        public void ConfigureDesktopView(Vector3 worldTarget, float worldScale)
        {
            _desktopViewScale = Mathf.Max(0.0001f, worldScale);
            _desktopTarget = worldTarget;
            _desktopDistance = DefaultDesktopDistance * _desktopViewScale;
            _camera.farClipPlane = Mathf.Max(50f, 12f * _desktopViewScale);
            _lastMousePosition = DesktopMousePosition();
            UpdateDesktopCamera();
        }

        public void SetAimVisual(Vector3 hitPoint, bool hasHit)
        {
            _interactionLine.enabled = _leftTracked;
            if (!_leftTracked)
            {
                return;
            }

            _interactionLine.SetPosition(0, _leftControllerVisual.position);
            _interactionLine.SetPosition(1, hasHit
                ? hitPoint
                : _leftControllerVisual.position + _leftControllerVisual.forward * (4f * _desktopViewScale));
        }

        public void SetAttackVisual(Vector3 hitPoint, bool hasTarget)
        {
            _attackLine.enabled = _rightTracked;
            if (!_rightTracked)
            {
                return;
            }

            Color start = hasTarget ? new Color(1f, 0.94f, 0.55f, 1f) : new Color(1f, 0.72f, 0.24f, 0.85f);
            _attackLine.startColor = start;
            _attackLine.endColor = new Color(start.r, start.g, start.b, 0.10f);
            _attackLine.SetPosition(0, _rightControllerVisual.position);
            _attackLine.SetPosition(1, hitPoint);
        }

        public void Pulse(float amplitude = 0.35f, float duration = 0.06f)
        {
            SendHaptic(XRNode.LeftHand, amplitude, duration);
        }

        public void PulseAttack(float amplitude = 0.35f, float duration = 0.06f)
        {
            SendHaptic(XRNode.RightHand, amplitude, duration);
        }

        private static void SendHaptic(XRNode node, float amplitude, float duration)
        {
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
            }
        }

        private void ReadTrackedPoses()
        {
            HasTrackedHead = false;
            _leftTracked = false;
            _rightTracked = false;
            InputTracking.GetNodeStates(_nodeStates);
            for (int i = 0; i < _nodeStates.Count; i++)
            {
                XRNodeState state = _nodeStates[i];
                bool hasPosition = state.TryGetPosition(out Vector3 position);
                bool hasRotation = state.TryGetRotation(out Quaternion rotation);
                if (!hasPosition || !hasRotation)
                {
                    continue;
                }

                if (state.nodeType == XRNode.Head)
                {
                    HasTrackedHead = true;
                    HeadPosition = transform.TransformPoint(position);
                    HeadRotation = transform.rotation * rotation;
                    _camera.transform.localPosition = position;
                    _camera.transform.localRotation = rotation;
                }
                else if (state.nodeType == XRNode.RightHand)
                {
                    _rightTracked = true;
                    _rightControllerVisual.localPosition = position;
                    _rightControllerVisual.localRotation = rotation;
                }
                else if (state.nodeType == XRNode.LeftHand)
                {
                    _leftTracked = true;
                    _leftControllerVisual.localPosition = position;
                    _leftControllerVisual.localRotation = rotation;
                }
            }

            _leftControllerVisual.gameObject.SetActive(_leftTracked);
            _rightControllerVisual.gameObject.SetActive(_rightTracked);
        }

        private void RequestFloorTrackingOrigin()
        {
            if (_floorOriginSet)
            {
                return;
            }

            SubsystemManager.GetSubsystems(_inputSubsystems);
            for (int i = 0; i < _inputSubsystems.Count; i++)
            {
                XRInputSubsystem subsystem = _inputSubsystems[i];
                if (subsystem.running && (subsystem.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                {
                    _floorOriginSet = subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                }
            }
        }

        private void ReadButtons()
        {
            bool recenterPressed;
            if (_leftTracked || _rightTracked)
            {
                XRInputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                bool attackPressed = ReadTriggerPressed(right, _attackPressed);
                right.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondaryPressed);
                right.TryGetFeatureValue(XRCommonUsages.grip, out float rightGrip);
                XRInputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                bool interactionTriggerPressed = ReadTriggerPressed(left, _interactionTriggerWasPressed);
                left.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool cyclePressed);
                left.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool startWavePressed);
                left.TryGetFeatureValue(XRCommonUsages.grip, out float leftGrip);
                recenterPressed = secondaryPressed || (rightGrip > 0.8f && leftGrip > 0.8f);

                _confirmDown |= interactionTriggerPressed && !_interactionTriggerWasPressed;
                _confirmUp |= !interactionTriggerPressed && _interactionTriggerWasPressed;
                if (!interactionTriggerPressed && _interactionTriggerWasPressed)
                {
                    _confirmReleaseWasDrag = false;
                }
                _cycleDown |= cyclePressed && !_cycleWasPressed;
                _startWaveDown |= startWavePressed && !_startWaveWasPressed;
                _interactionTriggerWasPressed = interactionTriggerPressed;
                _attackPressed = attackPressed;
                _cycleWasPressed = cyclePressed;
                _startWaveWasPressed = startWavePressed;
                _desktopPrimaryWasPressed = false;
                _desktopPrimaryDragged = false;
            }
            else
            {
                recenterPressed = Keyboard.current?.rKey.isPressed ?? false;
                ReadDesktopPointer();
                _attackPressed = (Keyboard.current?.fKey.isPressed ?? false) ||
                                 (Keyboard.current?.leftShiftKey.isPressed ?? false);
                if (_interactionTriggerWasPressed)
                {
                    _confirmUp = true;
                    _confirmReleaseWasDrag = false;
                }
                _interactionTriggerWasPressed = false;
                _cycleWasPressed = false;
                _startWaveWasPressed = false;
            }

            _recenterDown |= recenterPressed && !_recenterWasPressed;
            _recenterWasPressed = recenterPressed;
        }

        private static bool ReadTriggerPressed(XRInputDevice device, bool wasPressed)
        {
            bool digitalPressed = device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton) && triggerButton;
            bool hasAnalogValue = device.TryGetFeatureValue(XRCommonUsages.trigger, out float triggerValue);
            float threshold = wasPressed ? TriggerReleaseThreshold : TriggerPressThreshold;
            return digitalPressed || (hasAnalogValue && triggerValue >= threshold);
        }

        private void ReadDesktopPointer()
        {
            Mouse currentMouse = Mouse.current;
            bool primaryPressed = currentMouse?.leftButton.isPressed ?? false;
            Vector3 mouse = DesktopMousePosition();
            if (primaryPressed && !_desktopPrimaryWasPressed)
            {
                _desktopPressPosition = mouse;
                _desktopPrimaryDragged = false;
                _confirmDown = true;
            }
            else if (primaryPressed && !_desktopPrimaryDragged)
            {
                float thresholdSquared = DesktopDragThreshold * DesktopDragThreshold;
                _desktopPrimaryDragged = (mouse - _desktopPressPosition).sqrMagnitude >= thresholdSquared;
            }
            else if (!primaryPressed && _desktopPrimaryWasPressed)
            {
                _confirmUp = true;
                _confirmReleaseWasDrag = _desktopPrimaryDragged;
            }

            _desktopPrimaryWasPressed = primaryPressed;
        }

        private void UpdateDesktopOrbit()
        {
            Mouse currentMouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            Vector3 mouse = DesktopMousePosition();
            bool pointerOrbit = ((currentMouse?.leftButton.isPressed ?? false) && _desktopPrimaryDragged && !_interactionDragActive) ||
                                (currentMouse?.rightButton.isPressed ?? false) ||
                                (currentMouse?.middleButton.isPressed ?? false);
            if (pointerOrbit)
            {
                Vector3 delta = mouse - _lastMousePosition;
                _desktopYaw += delta.x * 0.18f;
                _desktopPitch = Mathf.Clamp(_desktopPitch - delta.y * 0.14f, 12f, 68f);
            }

            float deltaTime = Time.unscaledDeltaTime;
            float orbitAxis = 0f;
            if ((keyboard?.aKey.isPressed ?? false) || (keyboard?.leftArrowKey.isPressed ?? false))
            {
                orbitAxis -= 1f;
            }
            if ((keyboard?.dKey.isPressed ?? false) || (keyboard?.rightArrowKey.isPressed ?? false))
            {
                orbitAxis += 1f;
            }
            _desktopYaw += orbitAxis * 65f * deltaTime;

            float distanceAxis = 0f;
            if ((keyboard?.wKey.isPressed ?? false) || (keyboard?.upArrowKey.isPressed ?? false))
            {
                distanceAxis -= 1f;
            }
            if ((keyboard?.sKey.isPressed ?? false) || (keyboard?.downArrowKey.isPressed ?? false))
            {
                distanceAxis += 1f;
            }
            _desktopDistance += distanceAxis * 0.85f * _desktopViewScale * deltaTime;

            float heightAxis = 0f;
            if (keyboard?.qKey.isPressed ?? false)
            {
                heightAxis -= 1f;
            }
            if (keyboard?.eKey.isPressed ?? false)
            {
                heightAxis += 1f;
            }
            _desktopPitch += heightAxis * 38f * deltaTime;

            float scroll = currentMouse?.scroll.ReadValue().y ?? 0f;
            float scrollStep = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
            _desktopDistance = Mathf.Clamp(
                _desktopDistance - scrollStep * 0.08f * _desktopViewScale,
                0.85f * _desktopViewScale,
                2.8f * _desktopViewScale);
            _desktopPitch = Mathf.Clamp(_desktopPitch, 12f, 68f);
            _lastMousePosition = mouse;
            UpdateDesktopCamera();
        }

        private static Vector3 DesktopMousePosition()
        {
            Mouse currentMouse = Mouse.current;
            if (currentMouse == null)
            {
                return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            }

            Vector2 position = currentMouse.position.ReadValue();
            return new Vector3(position.x, position.y, 0f);
        }

        private void UpdateDesktopCamera()
        {
            Quaternion orbit = Quaternion.Euler(_desktopPitch, _desktopYaw, 0f);
            Vector3 offset = orbit * new Vector3(0f, 0f, -_desktopDistance);
            _camera.transform.position = _desktopTarget + offset;
            _camera.transform.LookAt(_desktopTarget);
        }

        private static void ConfigureControllerLine(LineRenderer line, Color color)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.004f;
            line.endWidth = 0.001f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.08f);
            line.enabled = false;
        }

        private static bool TryAttachOfficialControllerModel(Transform parent, string resourcePath)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name += " (Official PICO SDK)";
            instance.layer = 2;
            return true;
        }
    }
}
