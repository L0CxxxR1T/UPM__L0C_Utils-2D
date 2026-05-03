// Assets/Editor/SpriteSlice/SpriteSlicePostprocessor.cs
// ※ Editor フォルダ内に配置すること

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// テクスチャインポート時に <see cref="SpriteSliceConfig"/> の設定に従い、
/// グリッドスライス → 透明トリミング → Pivot絶対座標維持 を行う AssetPostprocessor。
///
/// 【Pivot維持の仕組み】
///   トリミング前のPivot絶対座標 = cellOrigin + pivot_normalized * cellSize
///   トリミング後の新Pivot正規化値 = (Pivot絶対座標 - trimmedRect.origin) / trimmedRect.size
///   → SpriteRect が縮んでも、テクスチャ上の同じピクセルを指し続ける
/// </summary>
public class SpriteSlicePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        // ---- 対象エントリを検索 ----
        SpriteSliceConfig.Entry entry = FindConfigEntry(assetPath);
        if (entry == null) return;

        // ---- TextureImporter 設定 ----
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        // ※ isReadable はゲームビルド要件に合わせて設定してください
        //    ピクセル読み取りは自前ロードで行うため、ここでは変更しない

        // ---- テクスチャをファイルから直接読み込む ----
        // assetPath は "Assets/..." 形式なので、プロジェクトルートと結合してフルパスを作る
        string fullPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", assetPath));

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[SpriteSlice] ファイルが見つかりません: {fullPath}");
            return;
        }

        Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(fullPath)))
        {
            Debug.LogError($"[SpriteSlice] 画像の読み込みに失敗しました: {assetPath}");
            Object.DestroyImmediate(tex);
            return;
        }

        // ---- セルサイズ計算 ----
        int texW = tex.width;
        int texH = tex.height;
        int cols = entry.columns;
        int rows = entry.rows;
        int cellW = texW / cols;
        int cellH = texH / rows;

        if (texW % cols != 0 || texH % rows != 0)
            Debug.LogWarning(
                $"[SpriteSlice] {assetPath}: テクスチャサイズ ({texW}×{texH}) が " +
                $"{cols}列×{rows}行で割り切れません。端数ピクセルは無視されます。");

        // ---- 各セルの SpriteMetaData を構築 ----
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        SpriteMetaData[] metaList = new SpriteMetaData[cols * rows];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int index = row * cols + col;

                // Unity のピクセル座標は Y=0 が下端。
                // LoadImage は PNG を垂直反転して読み込むため、
                // 画像上の row 行目（上から）は Unity 座標では (rows-1-row)*cellH になる。
                int cellX = col * cellW;
                int cellY = (rows - 1 - row) * cellH;

                // ---- トリミング前の Pivot 絶対座標 + オフセット（テクスチャピクセル空間） ----
                // pivotOffset は「トリミング後の見た目のPivot位置」をピクセル単位でずらす。
                // オフセットはトリミング後に適用するため、Rect変化の影響を受けない。
                float pivAbsX = cellX + entry.pivot.x * cellW + entry.pivotOffset.x;
                float pivAbsY = cellY + entry.pivot.y * cellH + entry.pivotOffset.y;

                // ---- トリミング済み Rect を算出 ----
                Color[] pixels = tex.GetPixels(cellX, cellY, cellW, cellH);
                Rect trimmedRect = ComputeTrimmedRect(
                    pixels, cellX, cellY, cellW, cellH, entry.alphaThreshold);

                // 完全透明セルはフルセル Rect にフォールバック
                if (trimmedRect.width <= 0)
                {
                    trimmedRect = new Rect(cellX, cellY, cellW, cellH);
                    Debug.LogWarning(
                        $"[SpriteSlice] {baseName}_{index}: セルが完全透明です。" +
                        $"フルセル Rect を使用します。");
                }

                // ---- Pivot の再計算 ----
                // トリミング前の絶対座標がトリミング後 Rect 内のどの正規化位置に相当するかを算出。
                // これにより、SpriteRect が縮んでもテクスチャ上の同一ピクセルを指し続ける。
                float newPivX = (pivAbsX - trimmedRect.x) / trimmedRect.width;
                float newPivY = (pivAbsY - trimmedRect.y) / trimmedRect.height;

                metaList[index] = new SpriteMetaData
                {
                    name = $"{baseName}_{index}",
                    rect = trimmedRect,
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(newPivX, newPivY),
                    border = Vector4.zero,
                };
            }
        }

        Object.DestroyImmediate(tex);




        SpriteDataProviderFactories factory = new();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        List<SpriteRect> spriteRects = new();
        foreach (SpriteMetaData meta in metaList)
        {
            SpriteRect sprRect = new()
            {
                name = meta.name,
                rect = meta.rect,
                alignment = (SpriteAlignment)meta.alignment,
                pivot = meta.pivot,
                border = meta.border,
            };
            spriteRects.Add(sprRect);
        }
        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();

        Debug.Log(
            $"[SpriteSlice] {assetPath}: " +
            $"{cols}×{rows} グリッドスライス＋トリミング完了 ({cols * rows} スプライト)");
    }

    // ================================================================
    //  ヘルパーメソッド
    // ================================================================

    /// <summary>
    /// ピクセル配列（セル単位）から、アルファ閾値以上のピクセルを囲む最小矩形を
    /// テクスチャ座標（絶対ピクセル）で返す。
    /// 完全透明の場合は <c>Rect.zero</c> を返す。
    /// </summary>
    static Rect ComputeTrimmedRect(
        Color[] pixels, int originX, int originY,
        int cellW, int cellH, float threshold)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        for (int y = 0; y < cellH; y++)
            for (int x = 0; x < cellW; x++)
            {
                if (pixels[y * cellW + x].a < threshold) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX == int.MinValue) return Rect.zero; // 完全透明

        return new Rect(
            originX + minX,
            originY + minY,
            maxX - minX + 1,
            maxY - minY + 1);
    }

    /// <summary>
    /// プロジェクト内の全 <see cref="SpriteSliceConfig"/> から
    /// <paramref name="path"/> にマッチする最初のエントリを返す。
    /// </summary>
    static SpriteSliceConfig.Entry FindConfigEntry(string path)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:SpriteSliceConfig"))
        {
            var configPath = AssetDatabase.GUIDToAssetPath(guid);
            SpriteSliceConfig config = AssetDatabase.LoadAssetAtPath<SpriteSliceConfig>(configPath);
            if (config == null) continue;

            SpriteSliceConfig.Entry entry = config.FindMatchingEntry(path);
            if (entry != null) return entry;
        }
        return null;
    }
}
