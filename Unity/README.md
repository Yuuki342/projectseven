# Anime Open World スターターキット (Unity / URP)

Unity でアニメ調オープンワールドを作るための土台です。
フォルダを Unity プロジェクトに入れて、メニューを 1 回押すと遊べるデモシーンができます。

## 入っているもの

| 機能 | ファイル | 内容 |
| --- | --- | --- |
| トゥーンシェーダー | `Shaders/ToonLit.shader` | 2 階調のセル影・影色・くっきりハイライト・リムライト・アウトライン・頂点カラー |
| 水 | `Shaders/ToonWater.shader` | 波で揺れる反射、視線角度で変わる色 |
| キャラ操作 | `Scripts/Runtime/ThirdPersonController.cs` | 走る / ダッシュ / ジャンプ / **空中で滑空** / **水泳** / スタミナ |
| カメラ | `Scripts/Runtime/OrbitCamera.cs` | 三人称のオービットカメラ、ズーム、壁抜け防止 |
| ワールド | `Scripts/Runtime/WorldStreamer.cs` | プレイヤー周辺だけ地形チャンクを生成・破棄する無限ワールド。海・平原・山・雪山、木・**桜**・岩 |
| 昼夜 | `Scripts/Runtime/DayNightCycle.cs` | 太陽・月、環境光、フォグ、空の明るさが時刻で変化 |
| 入力 | `Scripts/Runtime/GameInput.cs` | 新 Input System / 旧 Input Manager の両対応。キーボード・マウス・ゲームパッド |
| HUD | `Scripts/Runtime/GameHUD.cs` | 操作説明・時刻・スタミナゲージ (仮) |
| セットアップ | `Editor/AnimeOpenWorldSetup.cs` | デモシーンを自動で組み立てるメニュー |

## 使い方

1. **Unity Hub で新規プロジェクトを作成**
   - Unity 6 (6000.0 LTS 以降) 推奨
   - テンプレートは **Universal 3D** (URP) を選ぶ
2. このリポジトリの `Unity/AnimeOpenWorld` フォルダを、作ったプロジェクトの `Assets/` の中にコピー
   → `Assets/AnimeOpenWorld/...` になれば OK
3. Unity のメニューから **Tools > Anime Open World > Create Demo Scene**
4. ▶ (Play) を押す

`Assets/AnimeOpenWorld/Scenes/AnimeOpenWorldDemo.unity` が作られ、ビルド設定にも追加されます。

## 操作

| 操作 | キーボード・マウス | ゲームパッド |
| --- | --- | --- |
| 移動 | WASD / 矢印キー | 左スティック |
| ダッシュ | 左 Shift | B / ○ |
| ジャンプ | Space | A / × |
| 滑空 (空中で) | Space (もう一度押すと解除) | A / × |
| カメラ | マウス | 右スティック |
| ズーム | ホイール | 十字キー上下 |
| カーソル解放 / 再ロック | Esc / 左クリック | - |
| 操作説明の表示切替 | F1 | - |

## 調整のポイント

- **世界の形を変える**: `World` オブジェクトの `WorldStreamer` で `seed` を変えると別の世界になります。`mountainHeight` や `continentScale` で山の高さ・陸の大きさを調整できます。
- **見える距離**: `viewRadius` を上げると遠くまで見えますが重くなります。セットアップ時にフォグの距離もこれに合わせています。
- **時間**: `Day Night Cycle` の `timeOfDay` で時刻、`dayLengthMinutes` で 1 日の長さ。`advanceTime` を切ると時間が止まります。
- **絵作り**: マテリアルは `Assets/AnimeOpenWorld/Generated/` にあります。`Shade Color` (影色) を紫寄りや赤寄りにすると雰囲気が大きく変わります。

## 次のステップ (おすすめ)

1. **キャラクターモデル**: [VRoid Studio](https://vroid.com/studio) でキャラを作り、[UniVRM](https://github.com/vrm-c/UniVRM) で読み込む。モデルを `Player/Visual` の代わりに置き、`ThirdPersonController.animator` に Animator を設定します (`Speed` / `Grounded` / `Gliding` / `Swimming` パラメータを自動で更新します)。
2. **アニメーション**: Mixamo や Unity Asset Store のモーションで、待機・走り・ジャンプ・滑空・泳ぎの Animator を作る。
3. **ポストエフェクト**: シーンに Global Volume を追加し、Bloom・Color Adjustments・Tonemapping を入れるとぐっとアニメっぽくなります。
4. **草・花**: GPU インスタンシング (`Graphics.RenderMeshInstanced`) で草を大量に描画。
5. **ゲーム要素**: クエスト、NPC との会話、採集アイテム、戦闘、ファストトラベル、セーブ。
6. **本格的なシェーダーが欲しくなったら**: Unity 公式の [Unity Toon Shader (UTS3)](https://docs.unity3d.com/Packages/com.unity.toonshader@latest) や VRM 標準の MToon も選択肢です。

## 既知の制限

- トゥーンシェーダーはメインライト (太陽/月) のみ対応です。ポイントライトなどの追加ライトは反映されません。
- 地形の生成はメインスレッドで行っています。カクつく場合は `chunksPerFrame` を 1 にするか `chunkResolution` を下げてください。
- このキットはこの環境では Unity で実行確認できていません。エラーが出たらコンソールの内容を教えてください。
