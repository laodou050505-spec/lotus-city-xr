using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PicoTowerDefense
{
    // A real, separate boot scene. It never references, loads additively, or
    // instantiates the tabletop game. The only world object is a fully opaque
    // Figma title shrine and its world-space Start/Exit ray targets.
    public sealed class LotusTitleStage : MonoBehaviour
    {
        public const string GameplaySceneName = "SpatialDefense";

        private SpatialInputRig _input;
        private Transform _titleRoot;
        private Collider _startCollider;
        private Collider _exitCollider;
        private Renderer _defaultRenderer;
        private Renderer _pressedRenderer;
        private Material _defaultMaterial;
        private Material _pressedMaterial;
        private bool _pressArmed;
        private bool _rightPressArmed;
        private bool _rightTriggerWasPressed;
        private bool _loading;
        private float _inputEnabledAt;
        private bool _inputHasSettled;
        private int _inputSettledFrame;
        private int _pointerReadyFrame;

        public Transform TitleRoot => _titleRoot;
        public Camera TitleCamera => _input != null ? _input.Camera : null;

        private void Awake()
        {
            BuildTitleStage();
        }

        private void Update()
        {
            if (_input == null || _loading)
            {
                return;
            }

            _input.Tick();
            bool recenterDown = _input.ConsumeRecenterDown();
            if (_input.HasTrackedHead)
            {
                if (recenterDown)
                {
                    ResetXrToTitleStart();
                }
            }
            else if (recenterDown)
            {
                _input.ResetDesktopView();
            }

            // The left controller remains the primary interaction ray used by
            // gameplay, but a title screen must not strand a player whose
            // active virtual/physical controller is the right hand.  Both
            // controller rays use the same explicit world-space START target.
            Ray ray = _input.AimRay();
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, GameDefinitions.TitleRaycastDistance, ~(1 << 2));
            Collider hitCollider = hit ? hitInfo.collider : null;
            Ray rightRay = _input.AttackRay();
            bool rightHit = Physics.Raycast(rightRay, out RaycastHit rightHitInfo, GameDefinitions.TitleRaycastDistance, ~(1 << 2));
            Collider rightHitCollider = rightHit ? rightHitInfo.collider : null;
            bool leftHoveringStart = hitCollider == _startCollider;
            bool rightHoveringStart = rightHitCollider == _startCollider;
            bool hoveringStart = leftHoveringStart || rightHoveringStart;
            // Keep the authored pressed Figma frame visible for either hand
            // from the instant its trigger is armed, rather than only for the
            // left ray. This is vital in the PICO emulator where one virtual
            // controller can briefly lose its pose while the other is active.
            SetPressedVisual(_pressArmed || _rightPressArmed || hoveringStart);
            _input.SetAimVisual(hit ? hitInfo.point : ray.origin + ray.direction * 5f, hit);
            _input.SetAttackVisual(rightHit ? rightHitInfo.point : rightRay.origin + rightRay.direction * 5f, rightHit);

            bool released = _input.ConsumeConfirmUp(out bool wasDrag);
            if (!_inputHasSettled)
            {
                // Do not accept an OS focus click carried into a newly opened
                // desktop player. The player must first be at rest, then make
                // a fresh press/release gesture on the world-space target.
                _input.ConsumeConfirmDown();
                if (!_input.IsInteractionConfirmHeld)
                {
                    _inputHasSettled = true;
                    _inputSettledFrame = Time.frameCount;
                }
                _pressArmed = false;
                _rightPressArmed = false;
                _rightTriggerWasPressed = _input.IsAttackPressed;
                SetPressedVisual(hoveringStart);
                return;
            }

            bool inputReady = Time.unscaledTime >= _inputEnabledAt;
            // Desktop fallback deliberately uses the same pointer-ray
            // press/release contract as XR. A bare Enter/Space event may be
            // inherited from the launcher or window focus, which must never
            // silently leave this independent Title scene.
            // A fresh press must occur after the title has settled. This
            // blocks the focus click/trigger-release that some desktop and
            // simulator launchers leave queued on frame zero.
            bool confirmDown = _input.ConsumeConfirmDown();
            if (confirmDown && inputReady && Time.frameCount > _inputSettledFrame)
            {
                _pressArmed = CanActivateStart(leftHoveringStart);
            }

            // On the title only, either hand may activate START.  Keep the
            // press/release requirement so an app-focus click or trigger held
            // during launch cannot skip the independent title scene.
            bool rightPressed = _input.IsAttackPressed;
            bool rightDown = rightPressed && !_rightTriggerWasPressed;
            bool rightReleased = !rightPressed && _rightTriggerWasPressed;
            _rightTriggerWasPressed = rightPressed;
            if (rightDown && inputReady && Time.frameCount > _inputSettledFrame)
            {
                _rightPressArmed = CanActivateStart(rightHoveringStart);
            }

            if (released)
            {
                bool start = inputReady && _pressArmed && !wasDrag && hitCollider == _startCollider &&
                             Time.frameCount > _pointerReadyFrame;
                _pressArmed = false;
                if (start)
                {
                    StartCoroutine(LoadGameplayAfterStart());
                }
            }

            if (rightReleased)
            {
                bool start = inputReady && _rightPressArmed && rightHitCollider == _startCollider &&
                             Time.frameCount > _pointerReadyFrame;
                _rightPressArmed = false;
                if (start)
                {
                    StartCoroutine(LoadGameplayAfterStart());
                }
            }
        }

        public void BuildTitleStage()
        {
            if (_titleRoot != null)
            {
                return;
            }

            var rig = new GameObject("Title Stage XR Rig");
            rig.transform.SetParent(transform, false);
            _input = rig.AddComponent<SpatialInputRig>();
            _input.Initialize();
            _input.SetControllerPresentationVisible(false);
            _input.SetDesktopMixedRealityEnabled(false);
            // Keep the PICO see-through layer transparent.  The title is
            // exactly one world-space Figma cover: no extra backing quad,
            // no colour field and no duplicate frame behind it.
            _input.SetPicoPassthroughEnabled(true, Color.black);

            _titleRoot = new GameObject("Pure World Space Figma Title Root").transform;
            _titleRoot.SetParent(transform, false);
            _titleRoot.localPosition = GameDefinitions.TitlePanelLocalPosition;
            // A Unity Quad faces -Z. The player is located at -Z in the
            // shared design pose, so zero yaw keeps the cover's readable
            // front facing the player.
            // This keeps Title and gameplay at the same landmark and avoids
            // relying on a two-sided material to mask a reversed panel.
            _titleRoot.localRotation = Quaternion.Euler(0f, GameDefinitions.TitlePanelFacingYaw, 0f);

            // Fits the entire native 16:9 Figma frame in the desktop and XR
            // design view. START and EXIT can never be cropped away.
            // Keep the native 16:9 Figma source whole and readable.  The
            // opaque title environment fills the rest of the wider PICO
            // field, so it never becomes a black screen behind the cover.
            const float coverHeight = 2.70f;
            float coverWidth = coverHeight * (16f / 9f);

            _defaultMaterial = CreateTitleMaterial("UI/Startup/LotusCityStartCover");
            GameObject defaultFrame = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Quad,
                "Figma Native 1920x1080 Title Frame",
                _titleRoot,
                Vector3.zero,
                new Vector3(coverWidth, coverHeight, 1f),
                _defaultMaterial);
            _defaultRenderer = defaultFrame.GetComponent<Renderer>();
            ConfigureTitleRenderer(_defaultRenderer);

            _pressedMaterial = CreateTitleMaterial("UI/Startup/LotusCityStartCoverPressed");
            GameObject pressedFrame = ProceduralFactory.VisualPrimitive(
                PrimitiveType.Quad,
                "Figma Start Source Frame Pending Confirmation",
                _titleRoot,
                new Vector3(0f, 0f, -0.004f),
                new Vector3(coverWidth, coverHeight, 1f),
                _pressedMaterial);
            _pressedRenderer = pressedFrame.GetComponent<Renderer>();
            ConfigureTitleRenderer(_pressedRenderer);
            _pressedRenderer.enabled = false;

            _startCollider = CreateTitleTarget(
                "World Space Figma START Ray Target",
                new Vector3(-coverWidth * 0.28f, -coverHeight * 0.074f, -0.08f),
                // A generous, still discrete target covers the source START
                // label plus its natural hover margin. It lives in front of
                // all decorative title geometry, so controller rays cannot
                // be occluded by the opaque cover.
                new Vector3(coverWidth * 0.26f, coverHeight * 0.16f, 0.04f));
            _exitCollider = CreateTitleTarget(
                "World Space Figma EXIT Ray Target",
                new Vector3(-coverWidth * 0.28f, -coverHeight * 0.178f, -0.08f),
                new Vector3(coverWidth * 0.20f, coverHeight * 0.09f, 0.035f));

            ConfigureDesktopTitleStart();
            _inputEnabledAt = Time.unscaledTime + 2f;
            _inputHasSettled = false;
            _inputSettledFrame = int.MaxValue;
            // A fresh player launch is not guaranteed to receive an
            // OnApplicationFocus(true) callback on PICO. Starting at
            // int.MaxValue therefore made START permanently unclickable in
            // the emulator. Keep only a short launch debounce; a later focus
            // callback can still extend it to filter its own OS click.
            _pointerReadyFrame = Time.frameCount + 12;
            _rightTriggerWasPressed = false;
            Debug.Log("[Yi Nian Lotus City] Title scene ready: only the opaque Figma title shrine is active; gameplay scene is not loaded.");
        }

        public bool RunTitleStageSmokeTest(out string error)
        {
            BuildTitleStage();
            bool valid = _titleRoot != null && _titleRoot.parent == transform &&
                         _startCollider != null && _exitCollider != null &&
                         _defaultRenderer != null && _pressedRenderer != null &&
                         _input != null && _input.Camera != null &&
                         GetComponentInChildren<SpatialTowerDefenseGame>(true) == null &&
                         !string.IsNullOrEmpty(GameplaySceneName);
            if (!valid)
            {
                error = "Title stage must contain the opaque Figma world-space frame and Start/Exit targets, but no gameplay root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Collider CreateTitleTarget(string targetName, Vector3 localPosition, Vector3 localScale)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = targetName;
            target.transform.SetParent(_titleRoot, false);
            target.transform.localPosition = localPosition;
            target.transform.localScale = localScale;
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            return target.GetComponent<Collider>();
        }

        private static Material CreateTitleMaterial(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            // The source pixels themselves are fully opaque.  Keep the
            // stable transparent pass used by the PICO build: it is safe for
            // the two eye views while the camera now owns an opaque, nonblack
            // environment behind this world-space cover.
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material material = new(shader)
            {
                color = Color.white,
                mainTexture = texture,
                renderQueue = 3000
            };
            material.SetInt("_Cull", (int)CullMode.Back);
            return material;
        }

        private static void ConfigureTitleRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void ConfigureDesktopTitleStart()
        {
            _input.ConfigureDesktopView(
                GameDefinitions.TitlePanelLocalPosition,
                1f,
                GameDefinitions.DesignPlayerYaw,
                GameDefinitions.TitlePanelViewPitch,
                GameDefinitions.TitlePanelViewingDistance);
            ResetXrToTitleStart();
        }

        private void ResetXrToTitleStart()
        {
            _input.ResetXrRigToDesignStart(
                GameDefinitions.DesignPlayerEyeLocal - Vector3.up * GameDefinitions.DesignPlayerEyeHeight,
                Quaternion.Euler(0f, GameDefinitions.DesignPlayerYaw, 0f));
        }

        private void SetPressedVisual(bool pressed)
        {
            if (_defaultRenderer != null)
            {
                _defaultRenderer.enabled = !pressed;
            }
            if (_pressedRenderer != null)
            {
                _pressedRenderer.enabled = pressed;
            }
        }

        private bool CanActivateStart(bool rayHitsStart)
        {
            if (rayHitsStart)
            {
                return true;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // A real tracked controller must always point at the physical
            // world-space target. The official emulator, however, sometimes
            // provides a trigger while reporting no controller poses at all
            // (its known `NoTrackingData` condition). In that case there is
            // no ray to test, so a post-debounce trigger is the documented
            // controller fallback for this otherwise isolated title stage.
            return !_input.HasTrackedControllerPose;
#else
            return false;
#endif
        }

        private IEnumerator LoadGameplayAfterStart()
        {
            _loading = true;
            SetPressedVisual(true);
            yield return new WaitForSecondsRealtime(0.12f);
            Debug.Log("[Yi Nian Lotus City] Title START activated: unloading title scene and loading the gameplay scene.");
            AsyncOperation load = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || _input == null)
            {
                return;
            }

            // Native players frequently queue the mouse-down that focuses
            // their window. Delay the first title press well past that OS
            // gesture; an intentional XR trigger or mouse ray click still
            // works immediately afterwards.
            _pointerReadyFrame = Time.frameCount + 12;
            _pressArmed = false;
            _rightPressArmed = false;
            _rightTriggerWasPressed = _input.IsAttackPressed;
        }
    }
}
