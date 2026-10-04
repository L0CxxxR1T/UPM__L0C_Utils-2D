using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace XXXL0C.Utils2D.Editor
{
    /// <summary>
    /// Sprite Mode: MultipleのTexture2Dを対象として、各SpriteのPivot位置を維持しつつ最小矩形にトリミングする。
    /// </summary>
    public static class SpriteTrimmer
    {
        private const string MENU_PATH = "[ xxxL0C ]/Utils/[Sprite]ピボット維持トリミング";

        // 透明とみなすアルファのしきい値(0〜255)
        private const byte ALPHA_THRESHOLD = 0;

        [MenuItem(MENU_PATH, true)]
        private static bool ValidateTrim()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is Texture2D)
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem(MENU_PATH, false)]
        private static void Trim()
        {
            List<Texture2D> targets = new List<Texture2D>();
            foreach (Object obj in Selection.objects)
            {
                if (obj is Texture2D texture)
                {
                    targets.Add(texture);
                }
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Sprite Trimmer", "Texture2Dを選択してください。", "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Sprite Trimmer",
                $"テクスチャ{targets.Count}枚に、Spriteのトリミングを行います。\nピボット位置は維持されます。\n実行しますか?",
                "実行",
                "キャンセル");

            if (ok == false)
            {
                return;
            }

            int success = 0;
            foreach (Texture2D texture in targets)
            {
                if (TrimTexture(texture))
                {
                    success++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[SpriteTrimmer] 完了：{success}/{targets.Count} 個のテクスチャをトリミング");
        }

        /// <summary>
        /// テクスチャに含まれるSpriteを、ピクセルには触れずに矩形だけ縮めて元アセットへ上書きする。
        /// テクスチャ画像・スプライト名・配置は保持。
        /// </summary>
        private static bool TrimTexture(Texture2D texture)
        {
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[SpriteTrimmer] アセットパスを取得できません：{texture.name}");
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SpriteTrimmer] TextureImporterを取得できません：{assetPath}");
                return false;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Debug.LogWarning($"[SpriteTrimmer] Sprite ModeがMultipleではありません：{assetPath}");
                return false;
            }

            // readable / 非圧縮を一時的に保証して GetPixels を可能にする
            bool prevReadable = importer.isReadable;
            TextureImporterCompression prevCompression = importer.textureCompression;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Texture2D readable = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            int srcWidth = readable.width;
            Color32[] srcPixels = readable.GetPixels32();

            // 元の Sprite 情報を ISpriteEditorDataProvider 経由で取得する（spritesheet は廃止API）
            ISpriteEditorDataProvider provider = CreateDataProvider(importer);
            SpriteRect[] sprites = provider.GetSpriteRects();

            // 各 Sprite の矩形・ピボットだけを書き換える（name / spriteID / border 等は維持）。
            // 同じ SpriteRect インスタンスを使い回すので、アニメ等からの参照も壊れない。
            int trimmedCount = 0;
            foreach (SpriteRect sprite in sprites)
            {
                TrimResult result = CalculateTrim(sprite, srcPixels, srcWidth);

                Rect newRect = new Rect(
                    result.TrimmedRect.x,
                    result.TrimmedRect.y,
                    result.TrimmedRect.width,
                    result.TrimmedRect.height);

                // 実際に縮んだものだけカウント（完全透明＝元矩形のままはスキップ扱い）
                if (newRect != sprite.rect)
                {
                    trimmedCount++;
                }

                sprite.rect = newRect;
                sprite.pivot = result.NewPivot;
                sprite.alignment = SpriteAlignment.Custom; // Custom pivot を有効化
                // 9-slice の border は矩形を縮めた分だけはみ出す可能性があるのでクランプ／ゼロ化する
                sprite.border = ClampBorder(sprite.border, newRect.width, newRect.height);
            }

            // 元アセットの importer へ書き戻す
            provider.SetSpriteRects(sprites);
            provider.Apply();

            // トリミング用にいじった元 importer を元の設定へ戻しつつ reimport（書き戻しも確定する）
            RestoreImporter(importer, prevReadable, prevCompression);

            Debug.Log($"[SpriteTrimmer] {Path.GetFileNameWithoutExtension(assetPath)} の {sprites.Length} 個中 {trimmedCount} 個のSpriteを縮小");
            return true;
        }

        /// <summary>
        /// 1つの Sprite について、透明部分を除いた最小矩形と、ピボット維持後の normalized pivot を求める。
        /// </summary>
        private static TrimResult CalculateTrim(SpriteRect sprite, Color32[] srcPixels, int srcWidth)
        {
            RectInt srcRect = new RectInt(
                Mathf.RoundToInt(sprite.rect.x),
                Mathf.RoundToInt(sprite.rect.y),
                Mathf.RoundToInt(sprite.rect.width),
                Mathf.RoundToInt(sprite.rect.height));

            int minX = srcRect.width;
            int minY = srcRect.height;
            int maxX = -1;
            int maxY = -1;

            // sprite rect 内で不透明ピクセルの範囲を探す（座標は sprite rect ローカル）
            for (int y = 0; y < srcRect.height; y++)
            {
                for (int x = 0; x < srcRect.width; x++)
                {
                    int globalX = srcRect.x + x;
                    int globalY = srcRect.y + y;
                    byte alpha = srcPixels[globalY * srcWidth + globalX].a;
                    if (alpha > ALPHA_THRESHOLD)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0)
            {
                return new TrimResult
                {
                    SourceName = sprite.name,
                    SourceID = sprite.spriteID,
                    TrimmedRect = srcRect,
                    NewPivot = sprite.pivot
                };
            }

            int trimWidth = maxX - minX + 1;
            int trimHeight = maxY - minY + 1;

            float pivotPxX = sprite.pivot.x * srcRect.width;
            float pivotPxY = sprite.pivot.y * srcRect.height;

            float newPivotX = (pivotPxX - minX) / trimWidth;
            float newPivotY = (pivotPxY - minY) / trimHeight;

            RectInt trimmedGlobal = new RectInt(
                srcRect.x + minX,
                srcRect.y + minY,
                trimWidth,
                trimHeight);

            return new TrimResult
            {
                SourceName = sprite.name,
                SourceID = sprite.spriteID,
                TrimmedRect = trimmedGlobal,
                NewPivot = new Vector2(newPivotX, newPivotY)
            };
        }

        /// <summary>
        /// 9-slice の border（x=左, y=下, z=右, w=上 のピクセル値）を、縮めた後の矩形に収まるよう調整する。
        /// </summary>
        private static Vector4 ClampBorder(Vector4 border, float width, float height)
        {
            float left = Mathf.Max(0f, border.x);
            float bottom = Mathf.Max(0f, border.y);
            float right = Mathf.Max(0f, border.z);
            float top = Mathf.Max(0f, border.w);

            if (left + right > width)
            {
                right = Mathf.Max(0f, width - left);
                if (left > width)
                {
                    left = width;
                    right = 0f;
                }
            }

            if (bottom + top > height)
            {
                top = Mathf.Max(0f, height - bottom);
                if (bottom > height)
                {
                    bottom = height;
                    top = 0f;
                }
            }

            return new Vector4(left, bottom, right, top);
        }

        private static void RestoreImporter(
            TextureImporter importer,
            bool readable,
            TextureImporterCompression compression)
        {
            importer.isReadable = readable;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 指定した importer から ISpriteEditorDataProvider を生成・初期化して返す。
        /// </summary>
        private static ISpriteEditorDataProvider CreateDataProvider(TextureImporter importer)
        {
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        /// <summary>
        /// 1 Sprite 分のトリミング計算結果。
        /// </summary>
        private struct TrimResult
        {
            public string SourceName;
            public GUID SourceID;
            public RectInt TrimmedRect;
            public Vector2 NewPivot;
        }
    }
}
