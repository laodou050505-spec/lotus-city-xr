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
        public const string TitleFeatherShaderResource = "UI/Startup/TitleEdgeFeather";
        public const string TitleFeatherShaderName = "YiNianLotusCity/TitleEdgeFeather";
        public const float TitleFeatherWidth = 0.045f;

        private SpatialInputRig _input;
        private Transform _titleRoot;
        private Collider _startCollider;
        private Collider _exitCollider;
        private Renderer _defaultRenderer;
        private Renderer _pressedRenderer;
        private Material _defaultMaterial;
        private Material _pressedMaterial;
        private Renderer _startHoverRenderer;
        private Renderer _exitHoverRenderer;
        private Material _startHoverMaterial;
        private Material _exitHoverMaterial;
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

            // Either controller may enter the title. Once gameplay starts,
            // the two hands have distinct roles described by the world guide.
            Ray ray = _input.AimRay();
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, GameDefinitions.TitleRaycastDistance, ~(1 << 2));
            Collider hitCollider = hit ? hitInfo.collider : null;
            Ray rightRay = _input.AttackRay();
            bool rightHit = Physics.Raycast(rightRay, out RaycastHit rightHitInfo, GameDefinitions.TitleRaycastDistance, ~(1 << 2));
            Collider rightHitCollider = rightHit ? rightHitInfo.collider : null;
            bool leftHoveringStart = hitCollider == _startCollider;
            bool rightHoveringStart = rightHitCollider == _startCollider;
            bool leftHoveringExit = hitCollider == _exitCollider;
            bool rightHoveringExit = rightHitCollider == _exitCollider;
            bool hoveringStart = leftHoveringStart || rightHoveringStart;
            bool hoveringExit = leftHoveringExit || rightHoveringExit;
            SetButtonFeedback(hoveringStart, hoveringExit, _pressArmed || _rightPressArmed);
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
                SetButtonFeedback(hoveringStart, hoveringExit, false);
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
                if (leftHoveringExit)
                {
                    ExitApplication();
                }
            }

            bool rightPressed = _input.IsAttackPressed;
            bool rightDown = rightPressed && !_rightTriggerWasPressed;
            bool rightReleased = !rightPressed && _rightTriggerWasPressed;
            _rightTriggerWasPressed = rightPressed;
            if (rightDown && inputReady && Time.frameCount > _inputSettledFrame)
            {
                _rightPressArmed = CanActivateStart(rightHoveringStart);
                if (rightHoveringExit)
                {
                    ExitApplication();
                }
            }

            if (released)
            {
                // Once a valid START press is armed, tolerate natural hand
                // jitter before release instead of demanding a second exact
                // collider hit on the release frame.
                bool start = inputReady && _pressArmed && !wasDrag &&
                             Time.frameCount > _pointerReadyFrame;
                _pressArmed = false;
                if (start)
                {
                    StartCoroutine(LoadGameplayAfterStart());
                }
            }

            if (rightReleased)
            {
                bool start = inputReady && _rightPressArmed &&
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
            // Keep controller meshes hidden, but show slim world-space rays
            // and hit dots so START has visible aiming feedback in PICO.
            _input.SetPointerPresentationVisible(true);
            _input.SetDesktopMixedRealityEnabled(false);
            // Keep the PICO see-through layer transparent. The title is
            // exactly one world-space Figma cover with softly feathered
            // edges: no extra backing quad, colour field, or duplicate frame.
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

            // The original Figma frame remains the visual source. These two
            // overlays occupy exactly the current button bounds and only add
            // a warm/cool translucent wash while a controller ray hovers.
            // No scale animation is used, so the button sizes never change.
            _startHoverMaterial = CreateHoverMaterial(new Color(0.96f, 0.72f, 0.29f, 0.33f));
            _startHoverRenderer = CreateTitleFeedbackOverlay(
                "START Hover Colour Feedback",
                TitleArtPosition(394f, 600.5f, coverWidth, coverHeight, -0.006f),
                new Vector3(coverWidth * 390f / 1920f, coverHeight * 87f / 1080f, 1f),
                _startHoverMaterial,
                arrowShape: true);
            _exitHoverMaterial = CreateHoverMaterial(new Color(0.20f, 0.55f, 0.48f, 0.18f));
            _exitHoverRenderer = CreateTitleFeedbackOverlay(
                "EXIT Hover Colour Feedback",
                TitleArtPosition(362f, 688f, coverWidth, coverHeight, -0.006f),
                new Vector3(coverWidth * 142f / 1920f, coverHeight * 46f / 1080f, 1f),
                _exitHoverMaterial,
                arrowShape: false);

            _startCollider = CreateTitleTarget(
                "World Space Figma START Ray Target",
                TitleArtPosition(394f, 600.5f, coverWidth, coverHeight, -0.006f),
                // A generous, still discrete target covers the source START
                // label plus its natural hover margin. It lives in front of
                // all decorative title geometry, so controller rays cannot
                // be occluded by the opaque cover.
                new Vector3(coverWidth * 390f / 1920f, coverHeight * 87f / 1080f, 0.004f));
            _exitCollider = CreateTitleTarget(
                "World Space Figma EXIT Ray Target",
                TitleArtPosition(362f, 688f, coverWidth, coverHeight, -0.006f),
                new Vector3(coverWidth * 142f / 1920f, coverHeight * 46f / 1080f, 0.004f));

            BillboardText titleFacing = _titleRoot.gameObject.AddComponent<BillboardText>();
            titleFacing.Target = _input.Camera.transform;
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
                         _startHoverRenderer != null && _exitHoverRenderer != null &&
                         _input != null && _input.Camera != null &&
                         GetComponentInChildren<SpatialTowerDefenseGame>(true) == null &&
                         !string.IsNullOrEmpty(GameplaySceneName);
            Shader featherShader = Resources.Load<Shader>(TitleFeatherShaderResource);
            bool featherMaterialsValid = featherShader != null && featherShader.isSupported &&
                                         featherShader.name == TitleFeatherShaderName &&
                                         _defaultMaterial != null && _pressedMaterial != null &&
                                         _defaultMaterial.shader == featherShader &&
                                         _pressedMaterial.shader == featherShader &&
                                         _defaultMaterial.HasProperty("_FeatherWidth") &&
                                         _pressedMaterial.HasProperty("_FeatherWidth") &&
                                         Mathf.Approximately(_defaultMaterial.GetFloat("_FeatherWidth"), TitleFeatherWidth) &&
                                         Mathf.Approximately(_pressedMaterial.GetFloat("_FeatherWidth"), TitleFeatherWidth);
            SetPreviewButtonFeedback(true, false, false);
            bool startHoverStateValid = _startHoverRenderer.enabled && !_exitHoverRenderer.enabled &&
                                        _defaultRenderer.enabled && !_pressedRenderer.enabled;
            SetPreviewButtonFeedback(false, true, false);
            bool exitHoverStateValid = !_startHoverRenderer.enabled && _exitHoverRenderer.enabled &&
                                       _defaultRenderer.enabled && !_pressedRenderer.enabled;
            SetPreviewButtonFeedback(false, false, false);
            valid &= featherMaterialsValid && startHoverStateValid && exitHoverStateValid;
            if (!valid)
            {
                error = "Title stage must contain both softly feathered Figma cover states, independent Start/Exit hover feedback and targets, but no gameplay root.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void SetPreviewButtonFeedback(bool hoveringStart, bool hoveringExit, bool pressedStart)
        {
            SetButtonFeedback(hoveringStart, hoveringExit, pressedStart);
        }

        private static Vector3 TitleArtPosition(float pixelX, float pixelY, float width, float height, float z)
        {
            return new Vector3((pixelX / 1920f - 0.5f) * width, (0.5f - pixelY / 1080f) * height, z);
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
            // Preserve the original cover pixels and soften only the outer
            // edge in the material. This keeps button art and interaction
            // geometry pixel-aligned in both the normal and pressed states.
            Shader shader = Resources.Load<Shader>(TitleFeatherShaderResource) ??
                            Shader.Find(TitleFeatherShaderName) ??
                            Shader.Find("Unlit/Transparent");
            Material material = new(shader)
            {
                color = Color.white,
                mainTexture = texture,
                renderQueue = 3000
            };
            if (material.HasProperty("_FeatherWidth"))
            {
                material.SetFloat("_FeatherWidth", TitleFeatherWidth);
            }
            material.SetInt("_Cull", (int)CullMode.Back);
            return material;
        }

        private Renderer CreateTitleFeedbackOverlay(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool arrowShape)
        {
            GameObject overlay;
            if (arrowShape)
            {
                overlay = new GameObject(name);
                overlay.layer = 2;
                overlay.transform.SetParent(_titleRoot, false);
                overlay.transform.localPosition = localPosition;
                overlay.transform.localScale = localScale;
                Mesh mesh = new()
                {
                    name = "START Hover Arrow Mesh",
                    vertices = new[]
                    {
                        new Vector3(-0.50f, -0.50f, 0f),
                        new Vector3(0.388f, -0.50f, 0f),
                        new Vector3(0.50f, 0f, 0f),
                        new Vector3(0.388f, 0.50f, 0f),
                        new Vector3(-0.50f, 0.50f, 0f)
                    },
                    triangles = new[] { 0, 1, 2, 0, 2, 4, 4, 2, 3 }
                };
                mesh.RecalculateBounds();
                MeshFilter filter = overlay.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer meshRenderer = overlay.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
            }
            else
            {
                overlay = ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Quad,
                    name,
                    _titleRoot,
                    localPosition,
                    localScale,
                    material);
            }

            Renderer renderer = overlay.GetComponent<Renderer>();
            ConfigureTitleRenderer(renderer);
            renderer.enabled = false;
            return renderer;
        }

        private static Material CreateHoverMaterial(Color color)
        {
            // Sprites/Default multiplies the supplied tint and honours its
            // alpha. Unity's legacy Unlit/Transparent ignores material tint
            // on some editor/PICO variants and rendered this as a white block.
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                color = color,
                renderQueue = 3100
            };
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
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

        private void SetButtonFeedback(bool hoveringStart, bool hoveringExit, bool pressedStart)
        {
            SetPressedVisual(pressedStart);
            if (_startHoverRenderer != null)
            {
                _startHoverRenderer.enabled = hoveringStart && !pressedStart;
            }
            if (_exitHoverRenderer != null)
            {
                _exitHoverRenderer.enabled = hoveringExit;
            }
        }

        private static void ExitApplication()
        {
            Debug.Log("[Yi Nian Lotus City] Title EXIT activated.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool CanActivateStart(bool rayHitsStart)
        {
            if (rayHitsStart)
            {
                return true;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // PICO Emulator can publish a valid controller pose whose initial
            // forward direction does not intersect the title plane, and can
            // also report trigger input during NoTrackingData. After the two
            // second launch debounce, accept either controller trigger as the
            // accessible START confirmation. Exact ray hits still provide the
            // normal hover/pressed visual; desktop remains pointer-hit only.
            return true;
#else
            return false;
#endif
        }

        private IEnumerator LoadGameplayAfterStart()
        {
            _loading = true;
            SetButtonFeedback(false, false, true);
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
