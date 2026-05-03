// Assets/Editor/SpriteSlice/SpriteSliceReimportMenu.cs
// ※ Editor フォルダ内に配置すること

using UnityEditor;
using UnityEngine;

/// <summary>
/// SpriteSliceConfig を変更した後、対象テクスチャを手動で再インポートするためのメニュー。
/// Project ビューでテクスチャを選択 → Tools → Sprite Slice → 選択中のテクスチャを再インポート
/// </summary>
public static class SpriteSliceReimportMenu
{
    private const string MenuPath = "Tools/Sprite Slice/選択中のテクスチャを再インポート";

    [MenuItem(MenuPath)]
    static void ReimportSelected()
    {
        int count = 0;
        foreach (Object obj in Selection.objects)
        {
            if (obj is not Texture2D) continue;

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            count++;
        }

        if (count > 0)
            Debug.Log($"[SpriteSlice] {count} 個のテクスチャを再インポートしました。");
        else
            Debug.LogWarning("[SpriteSlice] Texture2D が選択されていません。");
    }

    // メニューアイテムの有効/無効切り替え（Texture2D が選択されているときだけ有効）
    [MenuItem(MenuPath, validate = true)]
    static bool ValidateReimportSelected()
    {
        foreach (Object obj in Selection.objects)
            if (obj is Texture2D) return true;
        return false;
    }
}
