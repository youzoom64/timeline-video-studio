# Timeline Video StudioのJSONから効果音を同期する

Python 3で、同スキル内の `scripts/timeline_sfx_events.py` を実行する。標準ライブラリで動作する。

## 対応の保存

編集プロジェクトと同じ作業フォルダーに `bindings.json` を保存し、各効果音へ次を記録する。

- `eventId`、`sfxClipId`、`targetId`: 出来事・配置済み効果音・同期対象のID。
- `category`、`meaning`、`approvedPath`: 音の役割、対象の意味、採用済み音源。
- `trigger.kind`: `clip_enter`、`clip_exit`、`parameter_run_start`、`parameter_run_end`。
- 変形では `trigger.parameters` に `x`・`y`・`scaleX` など、`direction` に `change`・`increase`・`decrease`、`run` に対象の連続変化の番号を指定する。
- `trigger.offset`: 動作との時間差。基本値は0秒で、意図して調整した差を記録する。
- `simultaneousTargetIds`: 同時の変化を一つの音へまとめた場合の、残りの対象ID。

編集データの `start` とキーフレームの `time`、または `duration` から時刻を計算する。途中にキーフレームを加えた連続動作は、変化が止まる点までを一つの区間として扱う。

編集JSONの素材の相対パスは、そのJSONのフォルダーを基準に解決する。対応JSONの `approvedPath` を相対パスにする場合も、対応JSONのフォルダーを基準に解決する。

## 実行

`scan --project <編集JSON> --out <変化一覧JSON>` で、登場時刻と位置・倍率・回転の変更値を抽出する。どの変化に音を使うかは、対象の役割と発話に基づいて `bindings.json` に登録する。

`sync --project <編集JSON> --bindings <対応JSON> --out <結果JSON>` で、各効果音の時刻を再計算し、現行配置との差を出す。

起動中の編集データを使う場合は `--api http://127.0.0.1:<実際のポート>` を付ける。`--apply` を付けると変更が必要な効果音の開始時刻だけをAPIへ反映して保存する。実行前の編集データを結果JSONの隣に保存する。

同期はコマンド実行時に行う。エディタ内の操作中に自動追従させる場合は、同じ対応を編集操作の更新処理へ組み込む。

対象の削除、指定された動きの消失、同時だった対象の時刻の分離、同一音声トラックでの重なりは、配置を変更する前に報告する。画像へ描き込まれた変化は `clip_enter` で扱い、プラグインやグループによる動きは、その効果の仕様に対応する同期点を別途定義する。
