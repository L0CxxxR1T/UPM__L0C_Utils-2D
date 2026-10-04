using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.PSD;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XXXL0C.Utils2D.Editor
{
    /// <summary>
    /// 選択中のPSDに対して、レイヤーSpriteのピボットをキャンバス基準で一括設定するウィンドウ。
    /// </summary>
    public sealed class PsdCanvasPivotWindow : EditorWindow
    {
        private const string MENU_PATH = "[ xxxL0C ]/Utils/[PSD]キャンバス基準ピボット一括設定";
        private const string TITLE = "PSDピボット設定";

        [SerializeField] private bool _useImporterPivot = true;
        [SerializeField] private SpriteAlignment _alignment = SpriteAlignment.BottomCenter;
        [SerializeField] private Vector2 _customPivot = new Vector2(0.5f, 0f);

        private Label _targetLabel;
        private Button _applyButton;
        private VisualElement _overrideGroup;
        private Vector2Field _customField;

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            GetWindow<PsdCanvasPivotWindow>(TITLE);
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;

            root.Add(new HelpBox(
                "PSD Importerでレイヤーごとに読み込んだSpriteのピボットを、キャンバス上の同じ位置に揃えます。\n" +
                "どのSpriteも同じ座標に置けばPSD上の配置どおりに表示されるため、SpriteResolverで差し替えても位置がずれません。",
                HelpBoxMessageType.Info));

            Toggle useImporterToggle = new Toggle("インポーターの設定を使用")
            {
                value = _useImporterPivot,
                tooltip = "PSD ImporterのCharacter Rig > Pivot の値を使用します。"
            };
            useImporterToggle.RegisterValueChangedCallback(evt =>
            {
                _useImporterPivot = evt.newValue;
                RefreshOverrideGroup();
            });
            root.Add(useImporterToggle);

            _overrideGroup = new VisualElement();
            EnumField alignmentField = new EnumField("キャンバス上のピボット", _alignment);
            alignmentField.RegisterValueChangedCallback(evt =>
            {
                _alignment = (SpriteAlignment)evt.newValue;
                RefreshOverrideGroup();
            });
            _overrideGroup.Add(alignmentField);

            _customField = new Vector2Field("カスタム(0〜1)") { value = _customPivot };
            _customField.RegisterValueChangedCallback(evt => _customPivot = evt.newValue);
            _overrideGroup.Add(_customField);
            root.Add(_overrideGroup);

            _targetLabel = new Label();
            _targetLabel.style.marginTop = 8;
            root.Add(_targetLabel);

            _applyButton = new Button(ApplyToSelection) { text = "選択中のPSDに適用" };
            _applyButton.style.marginTop = 4;
            root.Add(_applyButton);

            Selection.selectionChanged += RefreshTargets;
            RefreshOverrideGroup();
            RefreshTargets();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RefreshTargets;
        }

        private void RefreshOverrideGroup()
        {
            _overrideGroup.SetEnabled(_useImporterPivot == false);
            _customField.style.display = _alignment == SpriteAlignment.Custom ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshTargets()
        {
            int count = PsdCanvasPivotSetter.CollectSelectedImporters().Count;
            _targetLabel.text = $"対象のPSD：{count} 件";
            _applyButton.SetEnabled(count > 0);
        }

        private void ApplyToSelection()
        {
            List<PSDImporter> targets = PsdCanvasPivotSetter.CollectSelectedImporters();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(TITLE, "PSDファイルを選択してください。", "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                TITLE,
                $"PSD {targets.Count} 件のレイヤーSpriteのピボットを、キャンバス基準で上書きします。\n実行しますか?",
                "実行",
                "キャンセル");
            if (ok == false)
            {
                return;
            }

            Vector2 overridePivot = PsdCanvasPivotSetter.AlignmentToPivot(_alignment, _customPivot);
            int success = 0;
            foreach (PSDImporter importer in targets)
            {
                try
                {
                    Vector2 pivot = _useImporterPivot ? PsdCanvasPivotSetter.GetDocumentPivot(importer) : overridePivot;
                    if (PsdCanvasPivotSetter.Apply(importer, pivot))
                    {
                        success++;
                    }
                }
                catch (Exception e)
                {
                    // 1件の失敗で残りを止めないよう、ログに出して次へ進む
                    Debug.LogError($"[PsdCanvasPivot] 処理に失敗しました：{importer.assetPath}", importer);
                    Debug.LogException(e);
                }
            }

            Debug.Log($"[PsdCanvasPivot] 完了：{success}/{targets.Count} 件のPSDに適用");
        }
    }
}
