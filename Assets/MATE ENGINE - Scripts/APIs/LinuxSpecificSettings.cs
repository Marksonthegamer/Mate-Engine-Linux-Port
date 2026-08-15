#pragma warning disable 0162
using Gtk;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Application = Gtk.Application;

public class LinuxSpecificSettings : MonoBehaviour
{
    private StringTable stringTable; 
    
    private Window window;

    public GameObject background;
    
    public Texture2D icon;
    
    private bool useLegacyMoveResizeCalls;
    private bool enableAutoMemoryTrim;
    private bool forceKWin;
    private bool allowHyprlandMonitorSitting;
    private WindowType windowType;
    
    private Vector2 scrollPos;

    private Rect windowRect;
    private bool showWindow;

    private bool inEditor;

    private Label title;
    private Label intro;
    private CheckButton legacyMoveToggle;
    private Label legacyMoveDesc;
    private CheckButton memTrimToggle;
    private Label memTrimDesc;
    private Label winTypeLabel;
    private ComboBox winTypeCombo;
    private Label winTypeDesc;
    private CheckButton forceKWinToggle;
    private Label forceKWinDesc;
    private CheckButton allowHyprlandMonitorSittingToggle;
    private Label allowHyprlandMonitorSittingDesc;
    private Button cancelBtn;
    private Button saveBtn;
    
    public void Start()
    {
        stringTable = LocalizationSettings.StringDatabase.GetTable("Languages (UI)");
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
#if UNITY_EDITOR
        inEditor = true;
        return;
#endif
        window = new Window(UnityEngine.Application.productName)
        {
            Resizable = false,
            WindowPosition = WindowPosition.Center,
            TransientFor = GtkX11Helper.Instance.DummyParent
        };
        window.SetDefaultSize(783, 554);
        window.Destroyed += (s, e) =>
        {
            ShowWindow(false);
        };
        
        SetupGtkWindow(window);
    }

    public void ShowWindow(bool show = true)
    {
        showWindow = show;
        if (inEditor || SaveLoadHandler.Instance.safeMode)
        {
            useLegacyMoveResizeCalls = SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls;
            enableAutoMemoryTrim = SaveLoadHandler.Instance.data.enableAutoMemoryTrim;
            forceKWin = SaveLoadHandler.Instance.data.useKWinApi;
            allowHyprlandMonitorSitting = SaveLoadHandler.Instance.data.allowHyprlandMonitorSitting;
            windowType = SaveLoadHandler.Instance.data.windowType;
            background.SetActive(true);
            if (!show)
            {
                background.SetActive(false);
            }
            return;
        }
        if (show)
        {
            WindowManager.Instance.SetTopmost(false);
            window.ShowAll();
            Application.Run();
            return;
        }
        WindowManager.Instance.SetTopmost(SaveLoadHandler.Instance.data.isTopmost);
        window.Hide();
        Application.Quit();
    }
    
    private void UpdateTexts()
    {
        if (!stringTable) return;

        title.Markup = $"<span size=\"x-large\" weight=\"bold\">{stringTable.GetEntry("LINUX_SPECIFIC").GetLocalizedString()}</span>";
        intro.Text = stringTable.GetEntry("LINUX_SPECIFIC_TIP").GetLocalizedString();

        ((Label)legacyMoveToggle.Child).Text = stringTable.GetEntry("LSS_XMOVE").GetLocalizedString();
        legacyMoveDesc.Text = stringTable.GetEntry("LSS_XMOVE_TIP").GetLocalizedString();

        ((Label)memTrimToggle.Child).Text = stringTable.GetEntry("LSS_PMO").GetLocalizedString();
        memTrimDesc.Text = stringTable.GetEntry("LSS_PMO_TIP").GetLocalizedString();

        var model = new ListStore(typeof(string));
        model.AppendValues(stringTable.GetEntry("LSS_WINTYPE_NORMAL").GetLocalizedString());
        model.AppendValues(stringTable.GetEntry("LSS_WINTYPE_DOCK").GetLocalizedString());
        model.AppendValues(stringTable.GetEntry("LSS_WINTYPE_DESKTOP").GetLocalizedString());
        model.AppendValues(stringTable.GetEntry("LSS_WINTYPE_BORDERED").GetLocalizedString());
        winTypeCombo.Model = model;
        winTypeCombo.Active = (int)SaveLoadHandler.Instance.data.windowType;
        
        winTypeLabel.Text = stringTable.GetEntry("LSS_WINTYPE").GetLocalizedString();
        winTypeDesc.Text = stringTable.GetEntry("LSS_WINTYPE_TIP").GetLocalizedString();
        
        ((Label)forceKWinToggle.Child).Text = stringTable.GetEntry("LSS_KWIN").GetLocalizedString();
        forceKWinDesc.Text = stringTable.GetEntry("LSS_KWIN_TIP").GetLocalizedString();

        ((Label)allowHyprlandMonitorSittingToggle.Child).Text = stringTable.GetEntry("LSS_HYPR_MS").GetLocalizedString();
        allowHyprlandMonitorSittingDesc.Text = stringTable.GetEntry("LSS_HYPR_MS_TIP").GetLocalizedString();

        cancelBtn.Label = stringTable.GetEntry("CANCEL").GetLocalizedString();
        saveBtn.Label = stringTable.GetEntry("SAVE").GetLocalizedString();
    }
    
    private void OnLocaleChanged(Locale locale)
    {
        stringTable = LocalizationSettings.StringDatabase.GetTable("Languages (UI)");
        if (inEditor | SaveLoadHandler.Instance.safeMode) return;
        GLib.Idle.Add(() =>
        {
            UpdateTexts();
            return false;
        });
    }

    void SetupGtkWindow(Window window)
    {
        var mainBox = new Box(Orientation.Horizontal, 0);
        window.Add(mainBox);

        var iconBox = new Box(Orientation.Vertical, 0);
        mainBox.PackStart(iconBox, false, false, 0);

        var image = new Image(new Gdk.Pixbuf(icon.EncodeToPNG()));
        iconBox.PackStart(image, true, true, 40);
        
        var contentBox = new Box(Orientation.Vertical, 28)
        {
            MarginTop = 20,
            MarginEnd = 50,
            MarginBottom = 20
        };
        mainBox.PackStart(contentBox, true, true, 0);
        
        title = new Label(null)
        {
            Markup = $"<span size=\"x-large\" weight=\"bold\">{stringTable.GetEntry("LINUX_SPECIFIC").GetLocalizedString()}</span>"
        };
        title.Xalign = 0.0f;
        title.Yalign = 0.5f;
        contentBox.PackStart(title, false, false, 0);
        
        var card = new Frame { Name = "card", ShadowType = ShadowType.None };
        contentBox.PackStart(card, true, true, 0);
        
        var scrolledWindow = new ScrolledWindow();
        scrolledWindow.SetPolicy(PolicyType.Never, PolicyType.Automatic);
        card.Add(scrolledWindow);
        
        var cardBox = new Box(Orientation.Vertical, 20);
        cardBox.BorderWidth = 5;
        scrolledWindow.Add(cardBox);

        intro = new Label(stringTable.GetEntry("LINUX_SPECIFIC_TIP").GetLocalizedString())
        {
            LineWrap = true
        };
        intro.Xalign = 0.0f;
        intro.Yalign = 0.5f;
        cardBox.PackStart(intro, false, false, 0); 
        
        legacyMoveToggle = new CheckButton(stringTable.GetEntry("LSS_XMOVE").GetLocalizedString()) {Active = SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls, UseUnderline = false};
        //legacyMoveToggle.MarginTop = 40;
        ((Label)legacyMoveToggle.Child).Xalign = 0.0f;
        ((Label)legacyMoveToggle.Child).Yalign = 0.5f;
        cardBox.PackStart(legacyMoveToggle, false, false, 0);

        legacyMoveDesc = CreateDescriptionLabel(stringTable.GetEntry("LSS_XMOVE_TIP").GetLocalizedString());
        cardBox.PackStart(legacyMoveDesc, false, false, 0);
        
        memTrimToggle = new CheckButton(stringTable.GetEntry("LSS_PMO").GetLocalizedString()) {Active = SaveLoadHandler.Instance.data.enableAutoMemoryTrim, UseUnderline = false};
        ((Label)memTrimToggle.Child).Xalign = 0.0f;
        ((Label)memTrimToggle.Child).Yalign = 0.5f;
        cardBox.PackStart(memTrimToggle, false, false, 0);

        memTrimDesc = CreateDescriptionLabel(stringTable.GetEntry("LSS_PMO_TIP").GetLocalizedString());
        cardBox.PackStart(memTrimDesc, false, false, 0);

        var hbox = new Box(Orientation.Horizontal, 5);
        cardBox.PackStart(hbox, false, false, 0);

        winTypeLabel = new Label(stringTable.GetEntry("LSS_WINTYPE").GetLocalizedString());
        winTypeLabel.Xalign = 0.0f;
        winTypeLabel.Yalign = 0.5f;
        hbox.PackStart(winTypeLabel, false, false, 0);

        winTypeCombo = new ComboBox(new[] { stringTable.GetEntry("LSS_WINTYPE_NORMAL").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_DOCK").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_DESKTOP").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_BORDERED").GetLocalizedString() }){Active = (int)SaveLoadHandler.Instance.data.windowType};
        hbox.PackStart(winTypeCombo, false, false, 0);
        
        winTypeDesc = CreateDescriptionLabel(stringTable.GetEntry("LSS_WINTYPE_TIP").GetLocalizedString());
        cardBox.PackStart(winTypeDesc, false, false, 0);
        
        forceKWinToggle = new CheckButton(stringTable.GetEntry("LSS_KWIN").GetLocalizedString()) {Active = SaveLoadHandler.Instance.data.useKWinApi, UseUnderline = false};
        ((Label)forceKWinToggle.Child).Xalign = 0.0f;
        ((Label)forceKWinToggle.Child).Yalign = 0.5f;
        cardBox.PackStart(forceKWinToggle, false, false, 0);
        
        forceKWinDesc = CreateDescriptionLabel(stringTable.GetEntry("LSS_KWIN_TIP").GetLocalizedString());
        cardBox.PackStart(forceKWinDesc, false, false, 0);

        allowHyprlandMonitorSittingToggle = new CheckButton(stringTable.GetEntry("LSS_HYPR_MS").GetLocalizedString()) {Active = SaveLoadHandler.Instance.data.allowHyprlandMonitorSitting, UseUnderline = false};
        ((Label)allowHyprlandMonitorSittingToggle.Child).Xalign = 0.0f;
        ((Label)allowHyprlandMonitorSittingToggle.Child).Yalign = 0.5f;
        cardBox.PackStart(allowHyprlandMonitorSittingToggle, false, false, 0);
        
        allowHyprlandMonitorSittingDesc = CreateDescriptionLabel(stringTable.GetEntry("LSS_HYPR_MS_TIP").GetLocalizedString());
        cardBox.PackStart(allowHyprlandMonitorSittingDesc, false, false, 0);
        
        var buttonBox = new Box(Orientation.Horizontal, 20) { Halign = Align.End };
        contentBox.PackEnd(buttonBox, false, false, 0);

        cancelBtn = new Button(stringTable.GetEntry("CANCEL").GetLocalizedString());
        saveBtn = new Button(stringTable.GetEntry("SAVE").GetLocalizedString());
        saveBtn.StyleContext.AddClass("suggested-action");

        cancelBtn.Clicked += (_, _) =>
        {
            ShowWindow(false);
        };
        saveBtn.Clicked += (_, _) =>
        {
            ShowWindow(false);
            SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls = legacyMoveToggle.Active;
            SaveLoadHandler.Instance.data.enableAutoMemoryTrim = memTrimToggle.Active;
            SaveLoadHandler.Instance.data.windowType = (WindowType)winTypeCombo.Active;
            SaveLoadHandler.Instance.data.useKWinApi = forceKWinToggle.Active;
            SaveLoadHandler.Instance.data.allowHyprlandMonitorSitting = allowHyprlandMonitorSittingToggle.Active;
            FindFirstObjectByType<SettingsHandlerToggles>().ApplySettings();
            SaveLoadHandler.Instance.SaveToDisk();
        };

        buttonBox.PackStart(cancelBtn, false, false, 0);
        buttonBox.PackStart(saveBtn, false, false, 0);

        // CSS 样式
        var cssProvider = new CssProvider();
        cssProvider.LoadFromData(@"
            #card {
                background: rgba(255, 255, 255, 0.20);
                border: 1px solid;
                margin-bottom: 20px;
            }
            .description-label {
                font-size: 9pt;
                opacity: 0.85;
            }
        ");

        StyleContext.AddProviderForScreen(
            Gdk.Screen.Default,
            cssProvider,
            600 // StyleProviderPriority.Application
        );
    }

    static Label CreateDescriptionLabel(string text)
    {
        var label = new Label(text)
        {
            LineWrap = true,
            //MaxWidthChars = 60,
            //LineWrapMode = Pango.WrapMode.WordChar,
            UseUnderline = false
        };
        label.Xalign = 0.0f;
        label.Yalign = 0.5f;
        label.StyleContext.AddClass("description-label");
        return label;
    }
    
    private void OnGUI()
    {
        if (SaveLoadHandler.Instance.safeMode || inEditor)
        {
            if (!showWindow)
                return;
        }
        else
        {
            return;
        }
        
        if (windowRect.width == 0) // windowRect defaults to (0,0,0,0) initially
        {
            var windowWidth = 660f;
            var windowHeight = 420f;
            windowRect = new Rect(0, 0, windowWidth, windowHeight);
            // Center it
            windowRect.center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        windowRect = GUILayout.Window(GetHashCode(), windowRect, DrawWindow, stringTable.GetEntry("LINUX_SPECIFIC").GetLocalizedString());
        
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);
    }
    
    public void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void DrawWindow(int windowId)
    {
        // Main horizontal layout: icon | content
        GUILayout.BeginHorizontal();

        // Left icon area
        GUILayout.BeginVertical(GUILayout.Width(100f)); // Approximate width for icon
        GUILayout.Space(40f); // Padding top
        if (icon != null)
        {
            GUILayout.Label(icon, GUILayout.Width(80f), GUILayout.Height(80f)); // Adjust size as needed
        }
        GUILayout.EndVertical();
        
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        GUILayout.Label(stringTable.GetEntry("LINUX_SPECIFIC_TIP").GetLocalizedString());

        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(250f));
        
        GUILayout.Space(20f);
        
        useLegacyMoveResizeCalls = GUILayout.Toggle(useLegacyMoveResizeCalls, stringTable.GetEntry("LSS_XMOVE").GetLocalizedString());
        GUILayout.Label(stringTable.GetEntry("LSS_XMOVE_TIP").GetLocalizedString());

        GUILayout.Space(10f);
        
        enableAutoMemoryTrim = GUILayout.Toggle(enableAutoMemoryTrim, stringTable.GetEntry("LSS_PMO").GetLocalizedString());

        GUILayout.Label(stringTable.GetEntry("LSS_PMO_TIP").GetLocalizedString());

        GUILayout.Space(10f);
        
        GUILayout.BeginHorizontal();
        GUILayout.Label(stringTable.GetEntry("LSS_WINTYPE").GetLocalizedString(), GUILayout.Width(100));
        
        windowType = (WindowType)GUILayout.SelectionGrid((int)windowType, new [] { stringTable.GetEntry("LSS_WINTYPE_NORMAL").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_DOCK").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_DESKTOP").GetLocalizedString(), stringTable.GetEntry("LSS_WINTYPE_BORDERED").GetLocalizedString() }, 4);
        GUILayout.EndHorizontal();

        GUILayout.Label(stringTable.GetEntry("LSS_WINTYPE_TIP").GetLocalizedString(), 
            GUI.skin.GetStyle("label"), GUILayout.ExpandHeight(false));
        
        GUILayout.Space(10f);
        
        forceKWin = GUILayout.Toggle(forceKWin, stringTable.GetEntry("LSS_KWIN").GetLocalizedString());

        GUILayout.Label(stringTable.GetEntry("LSS_KWIN_TIP").GetLocalizedString());
        
        GUILayout.Space(10f);
        
        allowHyprlandMonitorSitting = GUILayout.Toggle(allowHyprlandMonitorSitting, stringTable.GetEntry("LSS_HYPR_MS").GetLocalizedString());

        GUILayout.Label(stringTable.GetEntry("LSS_HYPR_MS_TIP").GetLocalizedString());
        
        GUILayout.EndScrollView();
        
        GUILayout.FlexibleSpace();
        
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        if (GUILayout.Button(stringTable.GetEntry("CANCEL").GetLocalizedString()))
        {
            ShowWindow(false);
        }
        if (GUILayout.Button(stringTable.GetEntry("SAVE").GetLocalizedString()))
        {
            SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls = useLegacyMoveResizeCalls;
            SaveLoadHandler.Instance.data.enableAutoMemoryTrim = enableAutoMemoryTrim;
            SaveLoadHandler.Instance.data.useKWinApi = forceKWin;
            SaveLoadHandler.Instance.data.windowType = windowType;
            SaveLoadHandler.Instance.data.allowHyprlandMonitorSitting = allowHyprlandMonitorSitting;

            FindFirstObjectByType<SettingsHandlerToggles>().ApplySettings();
            SaveLoadHandler.Instance.SaveToDisk();
            ShowWindow(false);
        }
        GUILayout.EndHorizontal();
        
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow();
    }
}