using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SlotGame.Audio;
using SlotGame.Core;
using SlotGame.Reels;
using SlotGame.UI;
using Object = UnityEngine.Object;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// One-click generator: Slot Game > Build Everything creates the data assets, animation clips,
    /// prefabs and the playable scene, fully wired. Generating it from code keeps the setup
    /// reproducible and reviewable (no hand-clicked, undocumented scene state).
    /// </summary>
    public static class SlotSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SlotGame.unity";
        private const string DataPath = "Assets/Data";
        private const string PrefabPath = "Assets/Prefabs";
        private const string AnimPath = "Assets/Animations";

        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        // Reel window geometry measured from the supplied 816x624 machine artwork
        // (UI units, origin at the image centre, +y up).
        private static readonly Vector2 MachineSize = new Vector2(816f, 624f);
        private static readonly Vector2 ReelWindowSize = new Vector2(108f, 210f);
        private static readonly float[] ReelX = { -125f, 5f, 135f };
        private const float ReelY = -39f;

        [InitializeOnLoadMethod]
        private static void OfferBuildOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || File.Exists(ScenePath)) return;
                if (EditorUtility.DisplayDialog("Slot Game",
                        "The game scene has not been generated yet. Build it now?\n(You can also use the menu: Slot Game > Build Everything)",
                        "Build", "Later"))
                    BuildEverything();
            };
        }

        [MenuItem("Slot Game/Build Everything")]
        public static void BuildEverything()
        {
            AssetDatabase.Refresh();
            foreach (var folder in new[] { DataPath, PrefabPath, AnimPath, "Assets/Scenes" })
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));

            var sprites = new Sprites();
            if (!sprites.IsComplete(out string missing))
            {
                EditorUtility.DisplayDialog("Slot Game", "Some sprites could not be loaded as Sprites:\n" + missing +
                    "\nTry Assets > Reimport All, then run the builder again.", "OK");
                return;
            }

            SlotConfig config = CreateData(sprites);
            RuntimeAnimatorController paylineController = CreateAnimations();
            GameObject cellPrefab = CreateCellPrefab();
            GameObject reelPrefab = CreateReelPrefab(cellPrefab);
            GameObject popupPrefab = CreatePopupPrefab(sprites);
            CreateScene(sprites, config, paylineController, reelPrefab, popupPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[Slots] Build Everything finished. Press Play in scene " + ScenePath);
        }

        [MenuItem("Slot Game/Build WebGL")]
        public static void BuildWebGl()
        {
            if (!File.Exists(ScenePath)) BuildEverything();
            // Uncompressed output works on any static host (GitHub Pages, itch.io, local server).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.productName = "Slot Machine";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Slots] WebGL build result: " + report.summary.result + " -> Build/WebGL");
        }

        // ------------------------------------------------------------------ data

        private static SlotConfig CreateData(Sprites s)
        {
            // weight / payout multiplier chosen for ~10% hit rate and ~95.6% RTP (see README).
            SymbolDefinition seven = CreateSymbol("Symbol_Seven", "Seven", s.Seven, 1, 100, true);
            SymbolDefinition bar = CreateSymbol("Symbol_Bar", "Bar", s.Bar, 2, 30, false);
            SymbolDefinition bell = CreateSymbol("Symbol_Bell", "Bell", s.Bell, 3, 10, false);
            SymbolDefinition cherry = CreateSymbol("Symbol_Cherry", "Cherry", s.Cherry, 4, 5, false);

            string path = DataPath + "/SlotConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<SlotConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SlotConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            config.Configure(new[] { seven, bar, bell, cherry }, 1000, new[] { 5, 10, 25, 50, 100 }, 1);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static SymbolDefinition CreateSymbol(string file, string name, Sprite sprite, int weight, int multiplier, bool lucky)
        {
            string path = DataPath + "/" + file + ".asset";
            var symbol = AssetDatabase.LoadAssetAtPath<SymbolDefinition>(path);
            if (symbol == null)
            {
                symbol = ScriptableObject.CreateInstance<SymbolDefinition>();
                AssetDatabase.CreateAsset(symbol, path);
            }
            symbol.Configure(name, sprite, weight, multiplier, lucky);
            EditorUtility.SetDirty(symbol);
            return symbol;
        }

        // ------------------------------------------------------------------ animations

        /// <summary>Payline glow: an Animator with an idle clip and a looping pulse, toggled by a bool.</summary>
        private static RuntimeAnimatorController CreateAnimations()
        {
            AnimationClip idle = CreateAlphaClip("Payline_Idle", new AnimationCurve(new Keyframe(0f, 0f)), false);
            AnimationClip win = CreateAlphaClip("Payline_Win", new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.4f, 1f), new Keyframe(0.8f, 0.35f)), true);

            string controllerPath = AnimPath + "/Payline.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Active", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            AnimatorState winState = machine.AddState("Win");
            idleState.motion = idle;
            winState.motion = win;
            machine.defaultState = idleState;

            AnimatorStateTransition toWin = idleState.AddTransition(winState);
            toWin.AddCondition(AnimatorConditionMode.If, 0, "Active");
            toWin.hasExitTime = false;
            toWin.duration = 0.05f;

            AnimatorStateTransition toIdle = winState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Active");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.05f;

            return controller;
        }

        private static AnimationClip CreateAlphaClip(string name, AnimationCurve curve, bool loop)
        {
            string path = AnimPath + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { name = name };
            clip.SetCurve("", typeof(Image), "m_Color.a", curve);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject CreateCellPrefab()
        {
            var go = NewUi("SymbolCell", null);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            Place(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(96f, 96f));
            return SavePrefab(go, "SymbolCell");
        }

        private static GameObject CreateReelPrefab(GameObject cellPrefab)
        {
            var root = NewUi("Reel", null);
            Place(root.GetComponent<RectTransform>(), Vector2.zero, ReelWindowSize);
            root.AddComponent<RectMask2D>(); // clips symbols to the reel window

            var strip = NewUi("Strip", root.transform);
            Place(strip.GetComponent<RectTransform>(), Vector2.zero, ReelWindowSize);

            var reel = root.AddComponent<ReelController>();
            Wire(reel, "stripRoot", strip.GetComponent<RectTransform>());
            Wire(reel, "symbolCellPrefab", cellPrefab.GetComponent<Image>());
            return SavePrefab(root, "Reel");
        }

        private static GameObject CreatePopupPrefab(Sprites s)
        {
            var root = NewUi("ResultPopup", null);
            Stretch(root.GetComponent<RectTransform>());
            var group = root.AddComponent<CanvasGroup>();

            // Dim backdrop: also catches clicks so the player can dismiss win banners.
            var backdrop = NewUi("Backdrop", root.transform);
            Stretch(backdrop.GetComponent<RectTransform>());
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.65f);
            var backdropButton = backdrop.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.targetGraphic = backdropImage;

            var panel = NewUi("Panel", root.transform);
            Place(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(780f, 420f));
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = s.PopupFrame;
            panelImage.type = Image.Type.Sliced;

            Text title = AddText(panel.transform, "Title", "YOU WIN!", 68, TextAnchor.MiddleCenter, Gold,
                new Vector2(0f, 120f), new Vector2(700f, 90f));
            Text body = AddText(panel.transform, "Body", "", 44, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0f, 10f), new Vector2(700f, 140f));

            Button close = AddSpriteButton(panel.transform, "CloseButton", s.Close, new Vector2(335f, 165f), new Vector2(64f, 64f));
            Button yes = AddSpriteButton(panel.transform, "YesButton", s.Yes, new Vector2(-150f, -125f), new Vector2(240f, 92f));
            Button no = AddSpriteButton(panel.transform, "NoButton", s.No, new Vector2(150f, -125f), new Vector2(240f, 92f));

            var popup = root.AddComponent<ResultPopup>();
            Wire(popup, "canvasGroup", group);
            Wire(popup, "panel", panel.GetComponent<RectTransform>());
            Wire(popup, "titleText", title);
            Wire(popup, "bodyText", body);
            Wire(popup, "closeButton", close);
            Wire(popup, "yesButton", yes);
            Wire(popup, "noButton", no);
            Wire(popup, "backdropButton", backdropButton);
            return SavePrefab(root, "ResultPopup");
        }

        private static GameObject SavePrefab(GameObject instance, string name)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath + "/" + name + ".prefab");
            Object.DestroyImmediate(instance);
            return asset;
        }

        // ------------------------------------------------------------------ scene

        private static void CreateScene(Sprites s, SlotConfig config, RuntimeAnimatorController paylineController,
                                        GameObject reelPrefab, GameObject popupPrefab)
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.03f, 0.15f);
            }

            // --- Canvas + event system
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Transform canvasT = canvasGo.transform;

            // --- Background
            var bg = NewUi("Background", canvasT);
            Stretch(bg.GetComponent<RectTransform>());
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = s.Background;
            bgImage.raycastTarget = false;

            // --- Machine (layers share the same 816x624 canvas, so they line up exactly)
            var machine = NewUi("SlotMachine", canvasT);
            Place(machine.GetComponent<RectTransform>(), new Vector2(0f, 120f), MachineSize);
            machine.transform.localScale = Vector3.one * 1.15f;

            AddFullLayer(machine.transform, "ReelWindows", s.ReelWindows);

            var reels = new ReelController[SlotConfig.ReelCount];
            for (int i = 0; i < reels.Length; i++)
            {
                var reelGo = (GameObject)PrefabUtility.InstantiatePrefab(reelPrefab, machine.transform);
                reelGo.name = "Reel_" + i;
                Place(reelGo.GetComponent<RectTransform>(), new Vector2(ReelX[i], ReelY), ReelWindowSize);
                reels[i] = reelGo.GetComponent<ReelController>();
            }

            // Payline glow (centre row), animated by the Animator controller
            var paylineGo = NewUi("Payline", machine.transform);
            Place(paylineGo.GetComponent<RectTransform>(), new Vector2(5f, ReelY), new Vector2(380f, 6f));
            var paylineImage = paylineGo.AddComponent<Image>();
            paylineImage.color = new Color(1f, 0.25f, 0.25f, 0f);
            paylineImage.raycastTarget = false;
            var animator = paylineGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = paylineController;
            var payline = paylineGo.AddComponent<PaylineHighlight>();
            Wire(payline, "animator", animator);
            Wire(payline, "line", paylineImage);

            AddFullLayer(machine.transform, "Frame", s.Frame);

            // Lever
            var leverGo = NewUi("Lever", machine.transform);
            Place(leverGo.GetComponent<RectTransform>(), Vector2.zero, MachineSize);
            Image leverUp = AddFullLayer(leverGo.transform, "LeverUp", s.LeverUp);
            Image leverDown = AddFullLayer(leverGo.transform, "LeverDown", s.LeverDown);
            var hit = NewUi("LeverHitArea", leverGo.transform);
            Place(hit.GetComponent<RectTransform>(), new Vector2(311f, -43f), new Vector2(110f, 140f));
            var hitImage = hit.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f); // invisible but still receives clicks
            var hitButton = hit.AddComponent<Button>();
            hitButton.transition = Selectable.Transition.None;
            hitButton.targetGraphic = hitImage;
            var lever = leverGo.AddComponent<LeverController>();
            Wire(lever, "leverUp", leverUp);
            Wire(lever, "leverDown", leverDown);
            Wire(lever, "hitArea", hitButton);

            // --- Paytable (right)
            var paytable = NewUi("Paytable", canvasT);
            Place(paytable.GetComponent<RectTransform>(), new Vector2(700f, 130f), new Vector2(440f, 480f));
            var paytableImage = paytable.AddComponent<Image>();
            paytableImage.sprite = s.PopupFrame;
            paytableImage.type = Image.Type.Sliced;
            paytableImage.raycastTarget = false;
            AddText(paytable.transform, "Title", "PAYTABLE", 40, TextAnchor.MiddleCenter, Gold, new Vector2(0f, 195f), new Vector2(380f, 60f));
            var rows = NewUi("Rows", paytable.transform);
            Place(rows.GetComponent<RectTransform>(), new Vector2(0f, -30f), new Vector2(380f, 340f));
            var vlg = rows.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var paytableView = paytable.AddComponent<PaytableView>();
            Wire(paytableView, "config", config);
            Wire(paytableView, "rowContainer", rows.GetComponent<RectTransform>());

            // --- Left side: auto-play + sound toggles
            Button autoButton = AddPanelButton(canvasT, "AutoButton", s.MiddleBox, new Vector2(-700f, 130f), new Vector2(260f, 110f), "AUTO: OFF", out Text autoLabel);
            Button muteButton = AddPanelButton(canvasT, "MuteButton", s.MiddleBox, new Vector2(-700f, -10f), new Vector2(260f, 110f), "SOUND: ON", out Text muteLabel);
            AddText(canvasT, "Hint", "Click the lever or press SPACE", 26, TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 1f),
                new Vector2(-700f, -120f), new Vector2(300f, 80f));

            // --- HUD panel (bottom centre)
            var hudGo = NewUi("Hud", canvasT);
            Place(hudGo.GetComponent<RectTransform>(), new Vector2(0f, -402f), new Vector2(600f, 253f));
            var hudImage = hudGo.AddComponent<Image>();
            hudImage.sprite = s.MiddleBox;
            hudImage.raycastTarget = false;

            Transform h = hudGo.transform;
            AddText(h, "CreditsLabel", "CREDITS", 24, TextAnchor.MiddleCenter, Gold, new Vector2(-200f, 78f), new Vector2(190f, 32f));
            AddText(h, "BetLabel", "BET", 24, TextAnchor.MiddleCenter, Gold, new Vector2(0f, 78f), new Vector2(190f, 32f));
            AddText(h, "WinLabel", "LAST WIN", 24, TextAnchor.MiddleCenter, Gold, new Vector2(200f, 78f), new Vector2(190f, 32f));
            Text balanceText = AddText(h, "Balance", "1,000", 44, TextAnchor.MiddleCenter, Color.white, new Vector2(-200f, 34f), new Vector2(190f, 56f));
            Text betText = AddText(h, "Bet", "10", 44, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 34f), new Vector2(90f, 56f));
            Text winText = AddText(h, "Win", "0", 44, TextAnchor.MiddleCenter, Color.white, new Vector2(200f, 34f), new Vector2(190f, 56f));
            Button betDown = AddSpriteButton(h, "BetDown", s.Left, new Vector2(-68f, 34f), new Vector2(50f, 50f));
            Button betUp = AddSpriteButton(h, "BetUp", s.Right, new Vector2(68f, 34f), new Vector2(50f, 50f));
            Text freeSpinText = AddText(h, "FreeSpins", "", 28, TextAnchor.MiddleCenter, new Color(0.5f, 0.95f, 1f), new Vector2(0f, -22f), new Vector2(560f, 36f));
            Text statusText = AddText(h, "Status", "", 30, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, -74f), new Vector2(570f, 44f));

            var hud = hudGo.AddComponent<HudView>();
            Wire(hud, "balanceText", balanceText);
            Wire(hud, "betText", betText);
            Wire(hud, "winText", winText);
            Wire(hud, "freeSpinText", freeSpinText);
            Wire(hud, "statusText", statusText);
            Wire(hud, "betDownButton", betDown);
            Wire(hud, "betUpButton", betUp);
            Wire(hud, "autoButton", autoButton);
            Wire(hud, "autoLabel", autoLabel);
            Wire(hud, "muteButton", muteButton);
            Wire(hud, "muteLabel", muteLabel);

            // --- Effects layer (above the machine, below the popup)
            var fx = NewUi("Effects", canvasT);
            Stretch(fx.GetComponent<RectTransform>());
            var confetti = fx.AddComponent<ConfettiBurst>();
            Wire(confetti, "container", fx.GetComponent<RectTransform>());
            Wire(confetti, "sprites", new Object[] { s.Seven, s.Cherry, s.Bell, s.Bar });
            var shake = fx.AddComponent<ScreenShake>();
            Wire(shake, "target", machine.GetComponent<RectTransform>());

            // --- Popup (last sibling = drawn on top)
            var popupGo = (GameObject)PrefabUtility.InstantiatePrefab(popupPrefab, canvasT);
            var popup = popupGo.GetComponent<ResultPopup>();

            // --- Audio
            var audioGo = new GameObject("AudioManager");
            var audio = audioGo.AddComponent<AudioManager>();
            WireClip(audio, "spinLoop", "spin_loop");
            WireClip(audio, "reelStop", "reel_stop");
            WireClip(audio, "lever", "lever");
            WireClip(audio, "click", "click");
            WireClip(audio, "win", "win");
            WireClip(audio, "jackpot", "jackpot");
            WireClip(audio, "bonus", "bonus");
            WireClip(audio, "gameOver", "gameover");

            // --- Game manager
            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<SlotGameManager>();
            Wire(manager, "config", config);
            Wire(manager, "reels", reels);
            Wire(manager, "hud", hud);
            Wire(manager, "popup", popup);
            Wire(manager, "lever", lever);
            Wire(manager, "payline", payline);
            Wire(manager, "audioManager", audio);
            Wire(manager, "confetti", confetti);
            Wire(manager, "screenShake", shake);

            EditorSceneManager.SaveScene(SceneManager_GetActive(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static UnityEngine.SceneManagement.Scene SceneManager_GetActive()
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ------------------------------------------------------------------ UI helpers

        private static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Centre-anchored placement with explicit position and size.</summary>
        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image AddFullLayer(Transform parent, string name, Sprite sprite)
        {
            var go = NewUi(name, parent);
            Place(go.GetComponent<RectTransform>(), Vector2.zero, MachineSize);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(Transform parent, string name, string content, int size, TextAnchor anchor,
                                    Color color, Vector2 position, Vector2 dimensions)
        {
            var go = NewUi(name, parent);
            Place(go.GetComponent<RectTransform>(), position, dimensions);
            var text = go.AddComponent<Text>();
            text.font = UiFont.Default;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Button AddSpriteButton(Transform parent, string name, ButtonSprites sprites, Vector2 position, Vector2 size)
        {
            var go = NewUi(name, parent);
            Place(go.GetComponent<RectTransform>(), position, size);
            var image = go.AddComponent<Image>();
            image.sprite = sprites.Normal;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = sprites.Hover,
                pressedSprite = sprites.Pressed,
                selectedSprite = sprites.Normal,
                disabledSprite = sprites.Disabled != null ? sprites.Disabled : sprites.Normal
            };
            return button;
        }

        private static Button AddPanelButton(Transform parent, string name, Sprite background, Vector2 position,
                                             Vector2 size, string label, out Text labelText)
        {
            var go = NewUi(name, parent);
            Place(go.GetComponent<RectTransform>(), position, size);
            var image = go.AddComponent<Image>();
            image.sprite = background;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image; // default colour-tint feedback
            labelText = AddText(go.transform, "Label", label, 30, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
            return button;
        }

        // ------------------------------------------------------------------ wiring helpers

        /// <summary>Sets a [SerializeField] private field through SerializedObject (works in edit mode, supports undo-free batch setup).</summary>
        private static void Wire(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[Slots] Field '{field}' not found on {target.GetType().Name}"); return; }

            if (value is Object[] array)
            {
                prop.arraySize = array.Length;
                for (int i = 0; i < array.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
            }
            else if (value is Object unityObject) prop.objectReferenceValue = unityObject;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;
            else if (value is bool b) prop.boolValue = b;
            else Debug.LogError($"[Slots] Unsupported value for '{field}'");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireClip(AudioManager audio, string field, string fileName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/" + fileName + ".wav");
            if (clip == null) Debug.LogWarning("[Slots] Missing audio clip: " + fileName);
            Wire(audio, field, clip);
        }

        // ------------------------------------------------------------------ sprite loading

        private sealed class ButtonSprites
        {
            public Sprite Normal, Hover, Pressed, Disabled;
        }

        private sealed class Sprites
        {
            private readonly List<string> missing = new List<string>();

            public readonly Sprite Seven, Cherry, Bell, Bar;
            public readonly Sprite ReelWindows, Frame, LeverUp, LeverDown, MiddleBox, Background, PopupFrame;
            public readonly ButtonSprites Close, Left, Right, Yes, No;

            public Sprites()
            {
                Seven = Load("Symbols/Symbol_Seven");
                Cherry = Load("Symbols/Symbol_Cherry");
                Bell = Load("Symbols/Symbol_Bell");
                Bar = Load("Symbols/Symbol_Bar");
                ReelWindows = Load("Machine/Reel_Windows");
                Frame = Load("Machine/Machine_Frame");
                LeverUp = Load("Machine/Lever_Up");
                LeverDown = Load("Machine/Lever_Down");
                MiddleBox = Load("Machine/Panel_Middle_Box");
                Background = Load("Background/Background_Gradient");
                PopupFrame = Load("Popup/Popup_Frame");
                Close = LoadButton("Close", true);
                Left = LoadButton("Left", true);
                Right = LoadButton("Right", true);
                Yes = LoadButton("Yes", false);
                No = LoadButton("No", false);
            }

            public bool IsComplete(out string report)
            {
                report = string.Join("\n", missing);
                return missing.Count == 0;
            }

            private Sprite Load(string relativePath)
            {
                string path = "Assets/UI/" + relativePath + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) missing.Add(path);
                return sprite;
            }

            private ButtonSprites LoadButton(string name, bool hasDisabled)
            {
                return new ButtonSprites
                {
                    Normal = Load("Buttons/Btn_" + name + "_Normal"),
                    Hover = Load("Buttons/Btn_" + name + "_Hover"),
                    Pressed = Load("Buttons/Btn_" + name + "_Pressed"),
                    Disabled = hasDisabled ? Load("Buttons/Btn_" + name + "_Disabled") : null
                };
            }
        }
    }
}
