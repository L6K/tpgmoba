using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Enigma.UI;

public class DiagnoseUI
{
    public static void Execute()
    {
        // シーン内の UIDocument を探す
        var uiDoc = GameObject.FindObjectOfType<UIDocument>();
        if (uiDoc == null) { Debug.LogError("[Diag] UIDocument not found in scene!"); return; }

        Debug.Log($"[Diag] UIDocument found on: {uiDoc.gameObject.name}");
        Debug.Log($"[Diag] PanelSettings: {(uiDoc.panelSettings != null ? uiDoc.panelSettings.name : "NULL")}");
        Debug.Log($"[Diag] VisualTreeAsset: {(uiDoc.visualTreeAsset != null ? uiDoc.visualTreeAsset.name : "NULL")}");

        // HomeScreenController
        var ctrl = uiDoc.GetComponent<HomeScreenController>();
        Debug.Log($"[Diag] HomeScreenController: {(ctrl != null ? "found" : "NOT FOUND")}");

        // rootVisualElement
        var root = uiDoc.rootVisualElement;
        if (root == null) { Debug.LogError("[Diag] rootVisualElement is null!"); return; }

        // ボタンを検索
        var btnSettings = root.Q<Button>("btn-settings");
        Debug.Log($"[Diag] btn-settings: {(btnSettings != null ? "found" : "NOT FOUND")}");

        var overlay = root.Q<VisualElement>("settings-overlay");
        Debug.Log($"[Diag] settings-overlay: {(overlay != null ? "found" : "NOT FOUND")}");

        // EventSystem
        var es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        Debug.Log($"[Diag] EventSystem: {(es != null ? "found" : "NOT FOUND")}");
    }
}
