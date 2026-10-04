# \[ L0C_Utils:2D \]

![バージョン](https://img.shields.io/badge/ver-1.0.0-1458b8?style=flat-square&labelColor=black)
![ライセンス](https://img.shields.io/badge/license-MIT-1458b8?style=flat-square&labelColor=black)

2D 制作向けの Unity エディタ拡張集です。

| ツール | できること |
| --- | --- |
| [PSD キャンバス基準ピボット](#psd-キャンバス基準ピボット) | PSD から読み込んだレイヤー Sprite のピボットを、キャンバス上の同じ点に揃える |
| [ピボット維持トリミング](#ピボット維持トリミング) | Sprite の矩形を透明部分を除いた最小サイズに縮める。ピボットの位置は変わらない |

---

## インストール

Package Manager の `+` から `Install package from git URL...` を選び、次の URL を入力してください。

```plaintext
https://github.com/L0CxxxR1T/UPM__L0C_Utils-2D.git?path=Packages/works.xxxl0c.utils-2d
```

### 動作環境

| 項目 | 内容 |
| --- | --- |
| Unity | 6000.3 以降 |
| 依存パッケージ | `com.unity.2d.psdimporter` 12.0.2 / `com.unity.2d.sprite` 1.0.0 |
| 名前空間 | `XXXL0C.Utils2D.Editor` |

依存パッケージはインストール時に自動で追加されます。

---

## PSD キャンバス基準ピボット

**メニュー**：`[ xxxL0C ] > Utils > [PSD]キャンバス基準ピボット一括設定`

PSD Importer でレイヤーごとに読み込んだ Sprite は、それぞれ自分の矩形を基準にピボットを持っています。このツールは全レイヤーのピボットを「キャンバス上の同じ1点」に揃えます。

揃えたあとはどの Sprite も同じ座標に置くだけで PSD 上の配置どおりに重なるため、SpriteResolver でパーツを差し替えても位置がずれません。

### 使い方

1. メニューからウィンドウを開く
2. Project ウィンドウで対象の PSD / PSB を選ぶ（複数選択可）
3. ピボットの基準を決める
   - **インポーターの設定を使用**：PSD Importer の `Character Rig > Pivot` の値を使います
   - オフにした場合は、`キャンバス上のピボット` で位置を選びます。`Custom` を選ぶと 0〜1 の座標で指定できます
4. `選択中のPSDに適用` を押す

### 対象になる PSD

Inspector で次のように設定されている PSD だけが処理されます。条件に合わないものはスキップされ、Console に警告が出ます。

| 項目 | 設定値 |
| --- | --- |
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Import Mode | Individual Sprites (Mosaic) |

### 注意

- 既存のピボットは上書きされます
- レイヤーから作られた Sprite だけが対象です。グループレイヤーや、Sprite Editor で手動追加した矩形はそのまま残ります
- PSD Importer の内部データを読んでいるため、PSD Importer のバージョンによっては動かないことがあります。その場合はエラーで通知されます

---

## ピボット維持トリミング

**メニュー**：`[ xxxL0C ] > Utils > [Sprite]ピボット維持トリミング`

Sprite Mode が Multiple のテクスチャについて、各 Sprite の矩形を不透明ピクセルがある範囲まで縮めます。ピボットは縮める前と同じ位置を指すように再計算されるので、シーン上の見た目は変わりません。

### 使い方

1. Project ウィンドウで対象のテクスチャを選ぶ（複数選択可）
2. メニューを実行し、確認ダイアログで `実行` を押す

### 動作の詳細

- 書き換えるのは Sprite の矩形・ピボット・9-slice の Border だけです。画像のピクセル、Sprite の名前や ID はそのまま残るため、アニメーションなどからの参照は壊れません
- アルファが 0 のピクセルを透明とみなします
- 完全に透明な Sprite は縮めずに元の矩形のまま残します
- 縮めた矩形に Border が収まらない場合は、収まる値に切り詰めます
- 処理中だけ Read/Write を有効化し、圧縮を無効にします。終わると元の設定に戻ります

### 注意

- Sprite Mode が Multiple 以外のテクスチャはスキップされます
- PSD / PSB は対象外です（PSD はテクスチャとは別のインポーターで読み込まれるため）

---

## ライセンス

[MIT License](LICENSE)

© 2026 L0C_R1T

![X(Publisher)](https://img.shields.io/badge/%40xxxL0C-black?style=flat&logo=x&logoColor=white&labelColor=black&link=https%3A%2F%2Fx.com%2FxxxL0C)
![Github](https://img.shields.io/badge/-black?style=flat-square&logo=github&logoColor=white&labelColor=black&link=https%3A%2F%2Fgithub.com%2FL0CxxxR1T)
![Bluesky(Private)](https://img.shields.io/badge/-black?style=flat-square&logo=bluesky&logoColor=white&labelColor=black&link=https%3A%2F%2Fbsky.app%2Fprofile%2Fxxxl0c.works)