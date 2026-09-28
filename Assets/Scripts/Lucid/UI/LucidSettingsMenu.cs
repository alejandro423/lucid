using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lucid.Settings;

namespace Lucid.UI
{
    /// <summary>
    /// Runtime-only dark settings menu. Builds the whole UI from code with
    /// plain UGUI (no prefabs, no DOTween, no TextMeshPro): a dim background,
    /// a panel with sidebar + content, and a manual RectTransform slide
    /// animation driven in Update. Open from anywhere with Open().
    /// Palette: background #0A0E14, panel #14141F, cold accent #7AB8C4.
    /// </summary>
    public class LucidSettingsMenu : MonoBehaviour
    {
        private static readonly Color32 BgColor = new Color32(0x0A, 0x0E, 0x14, 255);
        private static readonly Color32 PanelColor = new Color32(0x14, 0x14, 0x1F, 255);
        private static readonly Color32 RowColor = new Color32(0x1E, 0x22, 0x30, 255);
        private static readonly Color32 AccentColor = new Color32(0x7A, 0xB8, 0xC4, 255);
        private static readonly Color32 TextColor = new Color32(0xE8, 0xEA, 0xF0, 255);
        private static readonly Color32 MutedColor = new Color32(0x8A, 0x93, 0xA6, 255);

        private const float SlideDuration = 0.3f;
        private const float SlideOffX = -1100f;

        private static LucidSettingsMenu _openMenu;

        private enum SlideState { Opening, Open, Closing }

        private SlideState _state = SlideState.Opening;
        private float _slideT;
        private RectTransform _panel;
        private Transform _contentRoot;
        private Transform _sidebarRoot;
        private string _selectedCategoryId;
        private Font _font;

        // ----- Public entry points -----

        /// <summary>
        /// Opens the settings menu, creating a Canvas when none exists.
        /// Safe to call in Play mode without touching any scene.
        /// </summary>
        public static LucidSettingsMenu Open()
        {
            if (_openMenu != null)
            {
                return _openMenu;
            }

            EnsureManager();
            EnsureEventSystem();

            var canvas = FindOrCreateCanvas();
            var menu = canvas.gameObject.AddComponent<LucidSettingsMenu>();
            menu.Build(canvas);
            _openMenu = menu;
            return menu;
        }

        public static void Close()
        {
            if (_openMenu != null)
            {
                _openMenu.BeginClose();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Edit-mode preview entry point. Same layout as Open() but without
        /// DontDestroyOnLoad (editor scripts cannot use it). The caller owns
        /// the canvas and is expected to save it into a preview scene.
        /// </summary>
        public static LucidSettingsMenu BuildPreview()
        {
            if (_openMenu != null)
            {
                return _openMenu;
            }

            EnsureManager();
            EnsureEventSystem();

            var go = new GameObject("LucidSettingsCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            var menu = go.AddComponent<LucidSettingsMenu>();
            menu.Build(canvas);
            var panel = go.transform.Find("Panel") as RectTransform;
            if (panel != null)
            {
                panel.anchoredPosition = Vector2.zero;
            }
            _openMenu = menu;
            return menu;
        }
#endif

        // ----- Bootstrap -----

        private static void EnsureManager()
        {
            if (LucidSettingManager.Instance == null)
            {
                var go = new GameObject("LucidSettingManager");
                go.AddComponent<LucidSettingManager>();
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

            // Prefer the Input System UI module when the package is present,
            // fall back to the standalone module otherwise. Resolved by name
            // so this file compiles with or without the InputSystem package.
            var inputSystemModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                go.AddComponent(inputSystemModule);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
        }

        private static Canvas FindOrCreateCanvas()
        {
            var existing = GameObject.Find("LucidSettingsCanvas");
            if (existing != null)
            {
                var canvas = existing.GetComponent<Canvas>();
                if (canvas != null)
                {
                    existing.SetActive(true);
                    return canvas;
                }
            }

            var go = new GameObject("LucidSettingsCanvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(go);
            return c;
        }

        // ----- Build -----

        private void Build(Canvas canvas)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                Debug.LogWarning("[LucidSettingsMenu] Built-in font not found, labels may not render.");
            }

            // Dim background covering the whole screen.
            var dim = CreateRect("Dim", canvas.transform, true);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(BgColor.r, BgColor.g, BgColor.b, 0.86f);

            // Centered panel (fixed size, slides in from the left).
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            _panel = (RectTransform)panelGo.transform;
            _panel.SetParent(canvas.transform, false);
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(900f, 560f);
            _panel.anchoredPosition = new Vector2(SlideOffX, 0f);
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = PanelColor;

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildHeader(panelGo.transform);
            BuildBody(panelGo.transform);
            BuildFooter(panelGo.transform);

            var manager = LucidSettingManager.Instance;
            if (manager != null && manager.Database != null && manager.Database.categories.Count > 0)
            {
                _selectedCategoryId = manager.Database.categories[0].categoryId;
            }

            RefreshSidebar();
            RefreshContent();
        }

        private void BuildHeader(Transform parent)
        {
            var header = CreateRect("Header", parent, false);
            AddLayout(header, 0f, 36f, 1f, 0f);

            var title = CreateLabel("Title", header, "SETTINGS", 22, TextColor, TextAnchor.MiddleLeft);
            Stretch(title, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-80f, 0f));

            var closeBtn = CreateButton("CloseButton", header, "X");
            AnchorRight(closeBtn, 64f, 36f);
            closeBtn.GetComponent<Button>().onClick.AddListener(BeginClose);
        }

        private void BuildBody(Transform parent)
        {
            var body = CreateRect("Body", parent, false);
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 12;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = false;
            bodyLayout.childForceExpandHeight = true;
            AddLayout(body, 0f, 0f, 1f, 1f);

            // Sidebar with one button per category.
            var sidebar = CreateRect("Sidebar", body, false);
            AddLayout(sidebar, 220f, 0f, 0f, 1f);
            var sideLayout = sidebar.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.spacing = 8;
            sideLayout.childControlWidth = true;
            sideLayout.childControlHeight = false;
            sideLayout.childForceExpandWidth = true;
            _sidebarRoot = sidebar;

            // Content area with scrolling.
            var scrollGo = new GameObject("ContentScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(body, false);
            AddLayout((RectTransform)scrollGo.transform, 0f, 0f, 1f, 1f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", scrollGo.transform, true);
            viewport.gameObject.AddComponent<Image>().color = BgColor;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10;
            contentLayout.padding = new RectOffset(4, 12, 4, 12);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            _contentRoot = content;
        }

        private void BuildFooter(Transform parent)
        {
            var footer = CreateRect("Footer", parent, false);
            AddLayout(footer, 0f, 44f, 1f, 0f);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12;
            footerLayout.childAlignment = TextAnchor.MiddleRight;
            footerLayout.childControlWidth = false;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = true;

            var applyBtn = CreateButton("ApplyButton", footer, "Apply");
            AddLayout(applyBtn, 140f, 0f, 0f, 0f);
            applyBtn.GetComponent<Button>().onClick.AddListener(OnApplyClicked);

            var closeBtn = CreateButton("CloseButton2", footer, "Close");
            AddLayout(closeBtn, 140f, 0f, 0f, 0f);
            closeBtn.GetComponent<Button>().onClick.AddListener(BeginClose);
        }

        // ----- Sidebar + content -----

        private void RefreshSidebar()
        {
            ClearChildren(_sidebarRoot);
            var manager = LucidSettingManager.Instance;
            if (manager == null || manager.Database == null)
            {
                return;
            }

            foreach (var category in manager.Database.categories)
            {
                string id = category.categoryId;
                var btn = CreateButton("Cat_" + id, _sidebarRoot, category.displayName);
                AddLayout(btn, 0f, 44f, 0f, 0f);
                var image = btn.GetComponent<Image>();
                image.color = id == _selectedCategoryId ? AccentColor : RowColor;
                var label = btn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = id == _selectedCategoryId ? BgColor : TextColor;
                }

                btn.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _selectedCategoryId = id;
                    RefreshSidebar();
                    RefreshContent();
                });
            }
        }

        private void RefreshContent()
        {
            ClearChildren(_contentRoot);
            var manager = LucidSettingManager.Instance;
            if (manager == null || manager.Database == null)
            {
                return;
            }

            var category = manager.Database.GetCategory(_selectedCategoryId);
            if (category == null)
            {
                return;
            }

            var header = CreateLabel("CatHeader", _contentRoot, category.displayName.ToUpperInvariant(), 18, AccentColor, TextAnchor.MiddleLeft);
            AddLayout(header, 0f, 30f, 0f, 0f);

            foreach (var setting in category.settings)
            {
                switch (setting.type)
                {
                    case LucidSettingType.Slider:
                        BuildSliderRow(category.categoryId, setting);
                        break;
                    case LucidSettingType.Toggle:
                        BuildToggleRow(category.categoryId, setting);
                        break;
                    case LucidSettingType.Dropdown:
                        BuildDropdownRow(category.categoryId, setting);
                        break;
                }
            }
        }

        private RectTransform BuildRowBase(string settingId)
        {
            var row = CreateRect("Row_" + settingId, _contentRoot, false);
            AddLayout(row, 0f, 64f, 0f, 0f);
            row.gameObject.AddComponent<Image>().color = RowColor;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row;
        }

        private void BuildSliderRow(string categoryId, LucidSetting setting)
        {
            var row = BuildRowBase(setting.id);
            var label = CreateLabel("Label", row, setting.displayName, 16, TextColor, TextAnchor.MiddleLeft);
            AddLayout(label, 200f, 0f, 0f, 0f);

            var slider = CreateSlider("Slider", row, setting.minValue, setting.maxValue, setting.floatValue);
            AddLayout(slider, 0f, 0f, 1f, 0f);

            var value = CreateLabel("Value", row, FormatSliderValue(setting.floatValue), 16, AccentColor, TextAnchor.MiddleRight);
            AddLayout(value, 60f, 0f, 0f, 0f);

            slider.GetComponent<Slider>().onValueChanged.AddListener(v =>
            {
                LucidSettingManager.Instance.SetFloat(categoryId, setting.id, v);
                value.GetComponent<Text>().text = FormatSliderValue(v);
            });
        }

        private void BuildToggleRow(string categoryId, LucidSetting setting)
        {
            var row = BuildRowBase(setting.id);
            var toggle = CreateToggle("Toggle", row, setting.displayName, setting.boolValue);
            AddLayout(toggle, 0f, 0f, 1f, 0f);

            toggle.GetComponent<Toggle>().onValueChanged.AddListener(v =>
            {
                LucidSettingManager.Instance.SetBool(categoryId, setting.id, v);
            });
        }

        private void BuildDropdownRow(string categoryId, LucidSetting setting)
        {
            var row = BuildRowBase(setting.id);
            var label = CreateLabel("Label", row, setting.displayName, 16, TextColor, TextAnchor.MiddleLeft);
            AddLayout(label, 200f, 0f, 0f, 0f);

            var dropdown = CreateDropdown("Dropdown", row, setting);
            AddLayout(dropdown, 0f, 0f, 1f, 0f);

            dropdown.GetComponent<Dropdown>().onValueChanged.AddListener(v =>
            {
                LucidSettingManager.Instance.SetInt(categoryId, setting.id, v);
            });
        }

        private static string FormatSliderValue(float v)
        {
            return Mathf.RoundToInt(v).ToString();
        }

        // ----- Buttons -----

        private void OnApplyClicked()
        {
            if (LucidSettingManager.Instance != null)
            {
                LucidSettingManager.Instance.Save();
            }
        }

        private void BeginClose()
        {
            if (_state == SlideState.Closing)
            {
                return;
            }

            _state = SlideState.Closing;
            _slideT = 0f;
        }

        // ----- Manual slide animation (no tween library) -----

        private void Update()
        {
            if (_panel == null)
            {
                return;
            }

            _slideT += Time.unscaledDeltaTime / SlideDuration;
            float t = Mathf.Clamp01(_slideT);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            if (_state == SlideState.Opening)
            {
                _panel.anchoredPosition = new Vector2(Mathf.Lerp(SlideOffX, 0f, eased), 0f);
                if (t >= 1f)
                {
                    _state = SlideState.Open;
                }
            }
            else if (_state == SlideState.Closing)
            {
                _panel.anchoredPosition = new Vector2(Mathf.Lerp(0f, SlideOffX, eased), 0f);
                if (t >= 1f)
                {
                    if (LucidSettingManager.Instance != null)
                    {
                        LucidSettingManager.Instance.Save();
                    }

                    _openMenu = null;
                    Destroy(gameObject);
                    var canvas = _panel != null ? _panel.transform.parent.gameObject : null;
                    if (canvas != null && canvas.transform.childCount <= 1)
                    {
                        Destroy(canvas);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_openMenu == this)
            {
                _openMenu = null;
            }
        }

        // ----- UGUI factory helpers -----

        private RectTransform CreateRect(string name, Transform parent, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return rect;
        }

        private static void AddLayout(RectTransform rect, float minWidth, float minHeight, float flexWidth, float flexHeight)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (minWidth > 0f)
            {
                element.minWidth = minWidth;
                element.preferredWidth = minWidth;
            }

            if (minHeight > 0f)
            {
                element.minHeight = minHeight;
                element.preferredHeight = minHeight;
            }

            element.flexibleWidth = flexWidth;
            element.flexibleHeight = flexHeight;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        private RectTransform CreateLabel(string name, Transform parent, string text, int size, Color color, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent, false);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateButton(string name, Transform parent, string text)
        {
            var rect = CreateRect(name, parent, false);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = RowColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = AccentColor;
            colors.pressedColor = AccentColor;
            button.colors = colors;

            var label = CreateLabel("Text", rect, text, 16, TextColor, TextAnchor.MiddleCenter);
            Stretch(label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void AnchorRight(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private RectTransform CreateSlider(string name, Transform parent, float min, float max, float value)
        {
            var rect = CreateRect(name, parent, false);

            var bg = new GameObject("Background", typeof(RectTransform));
            SetupFullRect(bg, rect, RowColor);

            var fillArea = CreateRect("FillArea", rect, true);
            fillArea.offsetMin = new Vector2(4f, 10f);
            fillArea.offsetMax = new Vector2(-14f, -10f);

            var fill = new GameObject("Fill", typeof(RectTransform));
            var fillRect = SetupFullRect(fill, fillArea, AccentColor);

            var handleArea = CreateRect("HandleArea", rect, true);
            handleArea.offsetMin = new Vector2(4f, 2f);
            handleArea.offsetMax = new Vector2(-4f, -2f);

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(handleArea, false);
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20f, 28f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = TextColor;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return rect;
        }

        private static RectTransform SetupFullRect(GameObject go, Transform parent, Color color)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            return rect;
        }

        private RectTransform CreateToggle(string name, Transform parent, string text, bool isOn)
        {
            var rect = CreateRect(name, parent, false);

            var bgGo = new GameObject("Background", typeof(RectTransform));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(rect, false);
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.sizeDelta = new Vector2(28f, 28f);
            bgRect.anchoredPosition = new Vector2(0f, 0f);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = BgColor;

            var checkGo = new GameObject("Checkmark", typeof(RectTransform));
            var checkRect = (RectTransform)checkGo.transform;
            checkRect.SetParent(bgRect, false);
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);
            checkRect.anchoredPosition = Vector2.zero;
            var checkImage = checkGo.AddComponent<Image>();
            checkImage.color = AccentColor;

            var label = CreateLabel("Label", rect, text, 16, TextColor, TextAnchor.MiddleLeft);
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 1f);
            label.offsetMin = new Vector2(40f, 0f);
            label.offsetMax = Vector2.zero;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = isOn;
            return rect;
        }

        private RectTransform CreateDropdown(string name, Transform parent, LucidSetting setting)
        {
            var rect = CreateRect(name, parent, false);
            var bgImage = rect.gameObject.AddComponent<Image>();
            bgImage.color = BgColor;

            var caption = CreateLabel("Label", rect, string.Empty, 16, TextColor, TextAnchor.MiddleLeft);
            Stretch(caption, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-36f, 0f));

            var arrow = CreateLabel("Arrow", rect, ">", 18, AccentColor, TextAnchor.MiddleCenter);
            AnchorRight(arrow, 28f, 28f);

            // Popup template: a child Toggle item whose parent is a
            // RectTransform, with the item text inside it (UGUI requirement).
            var template = CreateRect("Template", rect, false);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.offsetMin = new Vector2(0f, -6f);
            template.sizeDelta = new Vector2(0f, 150f);
            template.gameObject.AddComponent<Image>().color = PanelColor;
            template.gameObject.SetActive(false);

            var item = CreateRect("Item", template, true);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            var itemBg = item.gameObject.AddComponent<Image>();
            itemBg.color = RowColor;
            itemToggle.targetGraphic = itemBg;

            var itemCheck = new GameObject("Item Checkmark", typeof(RectTransform));
            var itemCheckRect = (RectTransform)itemCheck.transform;
            itemCheckRect.SetParent(item, false);
            itemCheckRect.anchorMin = new Vector2(0f, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.pivot = new Vector2(0f, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(18f, 18f);
            itemCheckRect.anchoredPosition = new Vector2(6f, 0f);
            var itemCheckImage = itemCheck.AddComponent<Image>();
            itemCheckImage.color = AccentColor;
            itemToggle.graphic = itemCheckImage;

            var itemLabel = CreateLabel("Item Label", item, string.Empty, 16, TextColor, TextAnchor.MiddleLeft);
            Stretch(itemLabel, Vector2.zero, Vector2.one, new Vector2(32f, 0f), new Vector2(-8f, 0f));

            var dropdown = rect.gameObject.AddComponent<Dropdown>();
            dropdown.template = template;
            dropdown.captionText = caption.GetComponent<Text>();
            dropdown.itemText = itemLabel.GetComponent<Text>();
            dropdown.options = BuildDropdownOptions(setting);
            dropdown.value = Mathf.Clamp(setting.intValue, 0, Mathf.Max(0, dropdown.options.Count - 1));
            dropdown.RefreshShownValue();
            return rect;
        }

        private static List<Dropdown.OptionData> BuildDropdownOptions(LucidSetting setting)
        {
            var options = new List<Dropdown.OptionData>();
            if (setting.options != null)
            {
                foreach (string option in setting.options)
                {
                    options.Add(new Dropdown.OptionData(option));
                }
            }

            if (options.Count == 0)
            {
                options.Add(new Dropdown.OptionData("None"));
            }

            return options;
        }
    }
}
