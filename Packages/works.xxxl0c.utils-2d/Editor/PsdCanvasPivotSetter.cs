using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace XXXL0C.Utils2D.Editor
{
    /// <summary>
    /// PSD Importerでレイヤー単位にインポートした各Spriteのピボットを、キャンバス上の共通の1点へ揃える。
    /// どのSpriteも同じ座標に置けばPSD上の配置どおりに重なるようになる。
    /// </summary>
    public static class PsdCanvasPivotSetter
    {
        private const string LOG_TAG = "[PsdCanvasPivot]";

        // PSD Importer内部のシリアライズ名(internalのためSerializedObject経由で読む)
        private const string PROP_LAYERED_SPRITES = "m_LayeredSpriteImportData";
        private const string PROP_PSD_LAYERS = "m_PsdLayers";
        private const string PROP_SPRITE_ID = "m_SpriteID";
        private const string PROP_SPRITE_POSITION = "spritePosition";
        private const string PROP_IS_GROUP = "m_IsGroup";
        private const string PROP_DOC_ALIGNMENT = "m_DocumentAlignment";
        private const string PROP_DOC_PIVOT = "m_DocumentPivot";

        // PSDヘッダ: シグネチャ(4) + バージョン(2) + 予約(6) + チャンネル数(2) の後に高さ・幅(ビッグエンディアン)
        private const string PSD_SIGNATURE = "8BPS";
        private const int PSD_HEADER_SIZE = 22;
        private const int PSD_HEIGHT_OFFSET = 14;
        private const int PSD_WIDTH_OFFSET = 18;

        /// <summary>
        /// 選択中のアセットからPSD Importerを重複なしで集める。
        /// </summary>
        public static List<PSDImporter> CollectSelectedImporters()
        {
            List<PSDImporter> result = new List<PSDImporter>();
            HashSet<string> paths = new HashSet<string>();
            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || paths.Add(path) == false)
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is PSDImporter importer)
                {
                    result.Add(importer);
                }
            }

            return result;
        }

        /// <summary>
        /// インポーターに設定されているドキュメントピボット(Character RigのPivot)を正規化座標で返す。
        /// </summary>
        public static Vector2 GetDocumentPivot(PSDImporter importer)
        {
            SerializedObject so = new SerializedObject(importer);
            SpriteAlignment alignment = (SpriteAlignment)FindRequired(so, PROP_DOC_ALIGNMENT).intValue;
            Vector2 custom = FindRequired(so, PROP_DOC_PIVOT).vector2Value;
            return AlignmentToPivot(alignment, custom);
        }

        /// <summary>
        /// SpriteAlignmentを正規化ピボット(0〜1)へ変換する。
        /// </summary>
        public static Vector2 AlignmentToPivot(SpriteAlignment alignment, Vector2 custom)
        {
            switch (alignment)
            {
                case SpriteAlignment.Center: return new Vector2(0.5f, 0.5f);
                case SpriteAlignment.TopLeft: return new Vector2(0f, 1f);
                case SpriteAlignment.TopCenter: return new Vector2(0.5f, 1f);
                case SpriteAlignment.TopRight: return new Vector2(1f, 1f);
                case SpriteAlignment.LeftCenter: return new Vector2(0f, 0.5f);
                case SpriteAlignment.RightCenter: return new Vector2(1f, 0.5f);
                case SpriteAlignment.BottomLeft: return new Vector2(0f, 0f);
                case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0f);
                case SpriteAlignment.BottomRight: return new Vector2(1f, 0f);
                default: return custom;
            }
        }

        /// <summary>
        /// キャンバス上の正規化座標 canvasPivot を、各レイヤーSpriteのピボットとして設定して再インポートする。
        /// </summary>
        public static bool Apply(PSDImporter importer, Vector2 canvasPivot)
        {
            string assetPath = importer.assetPath;

            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Multiple
                || importer.useMosaicMode == false)
            {
                Debug.LogWarning($"{LOG_TAG} Sprite Mode: Multiple / Import Mode: Individual Sprites (Mosaic) のPSDではありません：{assetPath}", importer);
                return false;
            }

            Vector2Int canvasSize = ReadCanvasSize(assetPath);
            Vector2 pivotPx = Vector2.Scale(canvasPivot, canvasSize);
            Dictionary<GUID, Vector2> positions = ReadLayerPositions(importer);

            ISpriteEditorDataProvider provider = importer;
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] sprites = provider.GetSpriteRects();

            int updated = 0;
            foreach (SpriteRect sprite in sprites)
            {
                // レイヤー由来でないSprite(Sprite Editorで手動追加した矩形など)はキャンバス位置を持たないので触らない
                if (positions.TryGetValue(sprite.spriteID, out Vector2 position) == false)
                {
                    continue;
                }

                Vector2 size = sprite.rect.size;
                if (size.x <= 0f || size.y <= 0f)
                {
                    Debug.LogWarning($"{LOG_TAG} サイズが0のSpriteをスキップ：{sprite.name}", importer);
                    continue;
                }

                // インポーターのプレハブ配置と同じく「spritePosition + pivot * rect.size」がキャンバス上のピボット位置になる
                sprite.pivot = new Vector2(
                    (pivotPx.x - position.x) / size.x,
                    (pivotPx.y - position.y) / size.y);
                sprite.alignment = SpriteAlignment.Custom;
                updated++;
            }

            provider.SetSpriteRects(sprites);
            provider.Apply();
            importer.SaveAndReimport();

            Debug.Log($"{LOG_TAG} {Path.GetFileName(assetPath)} の {sprites.Length} 個中 {updated} 個のピボットを設定 (キャンバス {canvasSize.x}x{canvasSize.y}, 基準 {canvasPivot})", importer);
            return true;
        }

        /// <summary>
        /// レイヤー由来のSpriteについて、spriteID → キャンバス上の左下座標(px) の対応を読む。
        /// </summary>
        private static Dictionary<GUID, Vector2> ReadLayerPositions(PSDImporter importer)
        {
            SerializedObject so = new SerializedObject(importer);

            HashSet<string> layerSpriteIds = new HashSet<string>();
            SerializedProperty layers = FindRequired(so, PROP_PSD_LAYERS);
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.FindPropertyRelative(PROP_IS_GROUP).boolValue)
                {
                    continue;
                }

                layerSpriteIds.Add(layer.FindPropertyRelative(PROP_SPRITE_ID).stringValue);
            }

            Dictionary<GUID, Vector2> result = new Dictionary<GUID, Vector2>();
            SerializedProperty sprites = FindRequired(so, PROP_LAYERED_SPRITES);
            for (int i = 0; i < sprites.arraySize; i++)
            {
                SerializedProperty sprite = sprites.GetArrayElementAtIndex(i);
                string id = sprite.FindPropertyRelative(PROP_SPRITE_ID).stringValue;
                if (layerSpriteIds.Contains(id) == false)
                {
                    continue;
                }

                result[new GUID(id)] = sprite.FindPropertyRelative(PROP_SPRITE_POSITION).vector2Value;
            }

            return result;
        }

        /// <summary>
        /// PSD/PSBのヘッダからキャンバスサイズを読む。
        /// </summary>
        private static Vector2Int ReadCanvasSize(string assetPath)
        {
            byte[] header = new byte[PSD_HEADER_SIZE];
            using (FileStream stream = File.OpenRead(assetPath))
            {
                int read = stream.Read(header, 0, header.Length);
                if (read < header.Length)
                {
                    throw new InvalidDataException($"PSDヘッダを読み込めません：{assetPath}");
                }
            }

            string signature = System.Text.Encoding.ASCII.GetString(header, 0, PSD_SIGNATURE.Length);
            if (string.Equals(signature, PSD_SIGNATURE, System.StringComparison.Ordinal) == false)
            {
                throw new InvalidDataException($"PSD/PSBファイルではありません：{assetPath}");
            }

            int height = ReadInt32BigEndian(header, PSD_HEIGHT_OFFSET);
            int width = ReadInt32BigEndian(header, PSD_WIDTH_OFFSET);
            return new Vector2Int(width, height);
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        /// <summary>
        /// PSD Importerの内部フィールドを取得する。パッケージ更新で構造が変わった場合は例外で知らせる。
        /// </summary>
        private static SerializedProperty FindRequired(SerializedObject so, string name)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop == null)
            {
                throw new System.InvalidOperationException($"PSD Importerのフィールド {name} が見つかりません。パッケージのバージョンで内部構造が変わった可能性があります。");
            }

            return prop;
        }
    }
}
