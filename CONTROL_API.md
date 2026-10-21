# 外部操作 API

## Transformの横倍率・縦倍率

映像クリップの `scaleX`・`scaleY`、`endScaleX`・`endScaleY` は既定1、最小0。描画は元の大きさに共通倍率 `scale` と各軸倍率を掛ける。`lockAspect` は縦横比固定（既定false）。開始・終了を個別に設定できる。

`point.set` は `scaleX`・`scaleY`・`nextScaleX`・`nextScaleY` に対応する。`segment.get/set` は `startScaleX`・`startScaleY`・`endScaleX`・`endScaleY` を使う。GUIの「動き・効果」に同じ項目を表示する。旧JSONは各軸倍率1で読み込む。

軸倍率・位置・回転・共通倍率と中間点の編集は、対象クリップのデータとTransformを更新し、素材・GameObject・再生グラフを再利用する。プレビューとMP4は同じ補間を使用する。

## MP4出力とキャンセル

MP4出力ボタンは、出力中に「出力キャンセル」へ切り替わる。同じボタンまたはAPIの `export.cancel` で、音声準備・映像転送・エンコードを中止できる。完了前のファイルは作業フォルダ内に置き、完了時に指定先へ反映する。

映像はRGBフレームをFFmpegへ直接転送し、表示が変わらないフレームの描画結果を再利用する。処理時間・描画枚数・再利用枚数を `EXPORT_STATS` としてログへ出す。

## 音声6トラック

WAVクリップの `audioTrack` は、0=効果音1、1=効果音2、2=BGM1、3=BGM2、4=音声1、5=音声2。タイムラインに6段を表示し、ドラッグまたは設定画面で所属を変更できる。各段は独立したUnity AudioTrackとAudioSourceで再生する。

レイヤーのタイトルを左クリックすると表示・ミュートを切り替える。●はオン、○はオフ。JSONの `hiddenVisualLayers` と `mutedAudioTracks` に保存し、プレビューとMP4へ反映する。APIは `layer.toggle` に `layer`（0始まり）と `kind`（音声は `WAV`、映像は `Image`）を渡す。

オン・オフは読み込み済みオブジェクトの可視状態とAudioSourceのmuteを直接切り替える。素材と再生グラフを再利用し、再生中の時刻を保つ。

タイムラインは固定の表示領域内でShift＋ホイールにより映像・グループ・音声を上下スクロールする。上端の境界をドラッグするとメイン画面との高さ配分を変更できる。APIの `timeline.vertical` は先頭行番号、`timeline.height` は高さ（ピクセル）を `value` に渡す。状態は `timelineFirstRow` と `timelineHeight` で取得する。

`clip.set` で `audioTrack`、`volume`（倍率、既定1）、`fadeIn`・`fadeOut`（秒）、`sourceOffset`（素材内の開始秒）を変更できる。保存JSONとMP4の音声ミックスにも反映する。既存のWAVクリップは音声1へ読み込む。

起動中の Windows アプリが `http://127.0.0.1:18764` を公開する。`GET /state` はプロジェクト、クリップ、区間、中間点、プラグイン一覧、再生位置を返す。`GET /frame` は現在フレームの PNG。変更は `POST /command` に JSON を送る。コマンド結果にも最新状態が含まれる。

複数のアプリを同時に開く場合、起動時の環境変数 `TIMELINE_STUDIO_API_PORT` に未使用のポート番号を指定できる。デモ専用インスタンスでは `18765` を使用し、既存の `18764` の編集状態を保持する。

`python scripts/studio_cli.py state` で確認できる。コマンド JSON を UTF-8 のファイルに保存して `python scripts/studio_cli.py command path/to/command.json` で実行する。`python scripts/studio_cli.py frame path/to/preview.png` で画面を確認する。

クライアントの接続先は `--api URL`、`TIMELINE_STUDIO_API_URL`、または `TIMELINE_STUDIO_API_PORT` で変更する。保存JSONの素材パスは保存先からの相対パス、読み込み後のAPI状態は解決済みの絶対パスになる。FFmpeg／FFprobeは不足時に自動取得し、MP4出力は取得完了を待つ。

| op | 主なフィールド | 動作 |
| --- | --- | --- |
| `project.new` | `title` | 新規作成 |
| `project.load` / `project.save` | `path` | JSON 読込・保存 |
| `project.set` | `field`, `value` | title, width, height, fps, bpm, duration（最低動画長） |
| `clip.add` | `kind`, `path`, `text`, `start`, `layer` | Image, WAV, OBJ, Text, Cube, Sphere を追加 |
| `clip.set` | `id`, `field`, `value` | 開始時刻、長さ、レイヤー、位置などを変更 |
| `clip.select` | `id` | 選択 |
| `clip.split` | `id`, `at` | 分割 |
| `clip.duplicate` / `clip.delete` | `id` | 複製・削除 |
| `point.add` / `point.delete` | `id`, `at` | 中間点の追加・削除。`at` は動画全体の秒数 |
| `segment.get` | `id`, `segment` | 指定区間の開始・終了値を `segmentValues` に返す。番号は 0 から |
| `segment.set` | `id`, `segment`, `values` | 指定区間の開始・終了値を設定 |
| `plugin.list` | なし | 利用可能なプラグイン一覧を返す |
| `effect.add` | `id`, `plugin` | クリップにエフェクトを追加 |
| `effect.set` / `effect.delete` | `id`, `effectId`, `field`, `value` | パラメータ変更・削除 |
| `transport.seek` | `seconds` | シーク |
| `transport.play` / `transport.pause` | なし | 再生・一時停止 |
| `timeline.zoom` | `value` | 時間軸の表示倍率（ピクセル/秒。縮小下限は動画の長さに応じて変わり、最大400） |
| `timeline.scroll` | `seconds` | 時間軸の左端を指定秒へ移動 |
| `history.undo` / `history.redo` | なし | 元に戻す・やり直す |
| `export.mp4` | `path`, `gpu` | MP4 書き出し。`gpu:true` は NVENC |

区間設定の例:

```json
{
  "op": "segment.set",
  "id": "クリップID",
  "segment": 0,
  "values": {
    "startX": 0, "startY": 0, "startScale": 1, "startRotation": 0,
    "endX": 5, "endY": 0, "endScale": 1, "endRotation": 0
  }
}
```

エフェクト設定の `field` は `a`, `b`, `c`, `d`, `enabled`。JSON プラグインはアプリの `Plugins` フォルダに入れ、再起動すると読み込まれる。種類は `shake`、`pulse`、`swing`、`spin`、`tint`。独自処理を実装する DLL は公開インターフェース `TimelineVideoStudio.IStudioEffectPlugin` を実装し、同じ `Plugins` フォルダに置く。
# 音声クリップのループ

`clip.set` の `field: "loop"` と `value: "true"` でループを有効にする。音声設定の「ループ再生を有効」と同じ設定で、クリップの長さまで元音源を繰り返す。`sourceOffset` は曲の開始位置、`duration` は配置する長さを表す。ループ設定は保存・複製・分割・Undo／Redo・MP4出力に引き継がれる。

