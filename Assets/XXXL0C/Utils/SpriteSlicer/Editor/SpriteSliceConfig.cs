// Assets/Editor/SpriteSlice/SpriteSliceConfig.cs
// ※ Editor フォルダ内に配置すること

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// テクスチャパス → スライス設定 のマッピングを管理する ScriptableObject。
/// 右クリック → Create → Tools → Sprite Slice Config で作成。
/// </summary>
[CreateAssetMenu(fileName = "SpriteSliceConfig", menuName = "Tools/Sprite Slice Config")]
public class SpriteSliceConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip(
            "Assets/ から始まる相対パス。\n" +
            "末尾が '/' → フォルダ前方一致（例: Assets/Sprites/Chara/）\n" +
            "末尾が '/' 以外 → 完全一致（例: Assets/Sprites/hero.png）")]
        public string assetPath = "Assets/";

        [Min(1), Tooltip("横方向のセル数")]
        public int columns = 3;

        [Min(1), Tooltip("縦方向のセル数")]
        public int rows = 4;

        [Tooltip(
            "トリミング前のセル矩形に対するPivotの正規化座標。\n" +
            "Unity座標系（左下が原点）。\n" +
            "例: 中央下 = (0.5, 0)  中央 = (0.5, 0.5)")]
        public Vector2 pivot = new(0.5f, 0f);

        [Tooltip(
            "トリミング後にPivotをピクセル単位でずらすオフセット。\n" +
            "Unity座標系（右・上が正方向）。\n" +
            "X: 奇数幅セルの左右微調整に 0.5 単位も使用可。\n" +
            "Y: 正の値で上方向にずれる。")]
        public Vector2 pivotOffset = Vector2.zero;

        [Range(0f, 1f), Tooltip(
            "この値以上のアルファを持つピクセルを「不透明」とみなす。\n" +
            "小さくするほど薄いピクセルも含めてトリミングされる。")]
        public float alphaThreshold = 0.01f;
    }

    public List<Entry> entries = new();

    /// <summary>
    /// <paramref name="path"/> に最初にマッチしたエントリを返す。
    /// マッチしなければ <c>null</c>。
    /// </summary>
    public Entry FindMatchingEntry(string path)
    {
        foreach (Entry e in entries)
        {
            if (string.IsNullOrEmpty(e.assetPath)) continue;

            bool match = e.assetPath.EndsWith("/")
                ? path.StartsWith(e.assetPath)   // フォルダ前方一致
                : path == e.assetPath;            // 完全一致

            if (match) return e;
        }
        return null;
    }
}