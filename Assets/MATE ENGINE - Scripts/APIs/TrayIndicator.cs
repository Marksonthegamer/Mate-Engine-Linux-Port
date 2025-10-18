using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class TrayMenuEntry
{
    public string Label;
    public Action Callback;
    public bool IsToggle;
    public bool InitialState;

    public TrayMenuEntry(string label, Action callback = null, bool isToggle = false, bool initialState = false)
    {
        Label = label;
        Callback = callback;
        IsToggle = isToggle;
        InitialState = initialState;
    }
}

public class TrayIndicator : MonoBehaviour
{
    #region API
    public enum AppIndicatorCategory
    {
        ApplicationStatus = 0,
        Communications = 1,
        SystemServices = 2,
        Other = 3
    }

    public enum AppIndicatorStatus
    {
        Passive = 0,   // Hidden
        Active = 1,    // Visible with normal icon
        Attention = 2  // Visible with attention icon (e.g., blinking)
    }

    [DllImport("libappindicator3")]
    private static extern IntPtr app_indicator_new(string id, string icon_name, AppIndicatorCategory category);

    [DllImport("libappindicator3")]
    private static extern void app_indicator_set_status(IntPtr indicator, AppIndicatorStatus status);

    [DllImport("libappindicator3")]
    private static extern void app_indicator_set_icon(IntPtr indicator, string icon_name);

    [DllImport("libappindicator3")]
    private static extern void app_indicator_set_attention_icon(IntPtr indicator, string attention_icon_name);

    [DllImport("libappindicator3")]
    private static extern void app_indicator_set_menu(IntPtr indicator, IntPtr menu);
    
    [DllImport("libappindicator3")]
    private static extern void app_indicator_set_icon_full(IntPtr indicator, string icon_name, string icon_desc);

    // GTK P/Invokes for basic menu (requires libgtk-3-0)
    [DllImport("libgtk-3")]
    private static extern IntPtr gtk_menu_new();
    
    [DllImport("libgtk-3")]
    private static extern bool gtk_init_check(ref int argc, ref IntPtr argv);

    [DllImport("libgtk-3")]
    private static extern IntPtr gtk_menu_item_new_with_label(string label);

    [DllImport("libgtk-3")]
    private static extern IntPtr gtk_check_menu_item_new_with_label(string label);

    [DllImport("libgtk-3")]
    private static extern void gtk_check_menu_item_set_active(IntPtr check_item, bool is_active);

    [DllImport("libgtk-3")]
    private static extern bool gtk_check_menu_item_get_active(IntPtr check_item);

    [DllImport("libgtk-3")]
    private static extern IntPtr gtk_separator_menu_item_new();

    [DllImport("libgtk-3")]
    private static extern void gtk_menu_shell_append(IntPtr menu_shell, IntPtr child);

    [DllImport("libgtk-3")]
    private static extern void gtk_widget_show(IntPtr widget);

    [DllImport("libgtk-3")]
    private static extern void gtk_widget_destroy(IntPtr widget);

    [DllImport("libglib-2.0.so.0")]
    private static extern bool g_main_context_pending(IntPtr context);

    [DllImport("libglib-2.0.so.0")]
    private static extern void g_main_context_iteration(IntPtr context, bool may_block);
    
    [DllImport("libgobject-2.0")]
    private static extern ulong g_signal_connect_data(IntPtr instance, 
        string detailed_signal, 
        IntPtr c_handler, 
        IntPtr data, 
        IntPtr destroy_data, 
        uint connect_flags);

    // Delegate matching GtkMenuItem::activate signature
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GtkMenuItemActivateDelegate(IntPtr menuItem, IntPtr userData);

    private IntPtr indicatorHandle;
    private IntPtr menuHandle;
    private List<GCHandle> delegateHandles = new();
    
    #endregion

    public static TrayIndicator Instance;
    
    Dictionary<IntPtr, Action> MenuActions = new();

    public Func<List<TrayMenuEntry>> OnBuildMenu;

    private void OnEnable()
    {
        Instance = this;
    }
    
    private void Update()
    {
        if (indicatorHandle != IntPtr.Zero)
        {
            while (g_main_context_pending(IntPtr.Zero))
            {
                g_main_context_iteration(IntPtr.Zero, false);
            }
        }
    }

    public void InitializeTrayIcon(string iconName)
    {
        int argc = 0;
        IntPtr argv = IntPtr.Zero;
        if (!gtk_init_check(ref argc, ref argv))
        {
            Debug.LogError("Failed to initialize GTK");
            return;
        }
        // Create the indicator with a unique ID, normal icon name (use a theme icon like "applications-system"), and category
        indicatorHandle = app_indicator_new(iconName, "applications-system", AppIndicatorCategory.ApplicationStatus);

        if (indicatorHandle == IntPtr.Zero)
        {
            Debug.LogError("Failed to create AppIndicator");
            return;
        }

        // Set to active status with normal icon
        app_indicator_set_status(indicatorHandle, AppIndicatorStatus.Active);
#if  UNITY_EDITOR
        app_indicator_set_icon_full(indicatorHandle, Application.dataPath + "/MATE ENGINE - Icons/DevICON_70x70.png", Application.productName);
#else
        app_indicator_set_icon_full(indicatorHandle, Application.dataPath + "/Resources/UnityPlayer.png", Application.productName);
#endif
        Application.runInBackground = true;
    }

    public void AddMenuItem(List<TrayMenuEntry> menuEntries)
    {
        CreateMenu(menuEntries);
    }

    public void RefreshMenu()
    {
        CleanupMenu();
        if (OnBuildMenu != null)
        {
            CreateMenu(OnBuildMenu());
        }
    }

    private void CreateMenu(List<TrayMenuEntry> menuEntries)
    {
        menuHandle = gtk_menu_new();
        if (menuEntries != null)
        {
            foreach (var entry in menuEntries)
            {
                if (entry.Label == "Separator")
                {
                    IntPtr separator = gtk_separator_menu_item_new();
                    gtk_menu_shell_append(menuHandle, separator);
                    gtk_widget_show(separator);
                }
                else
                {
                    IntPtr menuItem;
                    bool isToggle = entry.IsToggle;

                    if (isToggle)
                    {
                        // Create toggle (check) menu item
                        menuItem = gtk_check_menu_item_new_with_label(entry.Label);
                        gtk_check_menu_item_set_active(menuItem, entry.InitialState);
                    }
                    else
                    {
                        // Regular menu item
                        menuItem = gtk_menu_item_new_with_label(entry.Label);
                    }

                    gtk_menu_shell_append(menuHandle, menuItem);
                    gtk_widget_show(menuItem);
                    
                    if (entry.Callback != null)
                    {
                        MenuActions.Add(menuItem, entry.Callback);
                    }

                    GtkMenuItemActivateDelegate menuItemDelegate = OnMenuItemClicked;
                    IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(menuItemDelegate);
                    g_signal_connect_data(menuItem, "activate", callbackPtr, IntPtr.Zero, IntPtr.Zero, 0);
                    delegateHandles.Add(GCHandle.Alloc(menuItemDelegate));
                }
            }
        }
        
        app_indicator_set_menu(indicatorHandle, menuHandle);
    }

    private void OnMenuItemClicked(IntPtr menuItem, IntPtr userData)
    {
        if (MenuActions.ContainsKey(menuItem))
        {
            MenuActions[menuItem].Invoke();
        }

        // Optional: Refresh menu after action (e.g., to update other items)
        RefreshMenu();
    }
    
    void OnApplicationQuit()
    {
        if (indicatorHandle != IntPtr.Zero)
        {
            app_indicator_set_status(indicatorHandle, AppIndicatorStatus.Passive);
            indicatorHandle = IntPtr.Zero;
        }
        CleanupMenu();
    }

    void OnDestroy()
    {
        CleanupMenu();
    }

    private void CleanupMenu()
    {
        if (menuHandle != IntPtr.Zero)
        {
            gtk_widget_destroy(menuHandle);
            menuHandle = IntPtr.Zero;
        }
        MenuActions.Clear();
        foreach (var handle in delegateHandles)
        {
            if (handle.IsAllocated)
                handle.Free();
        }
        delegateHandles.Clear();
    }
}