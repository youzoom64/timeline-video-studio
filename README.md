# Timeline Video Studio

Unity Timelineで画像・文字・3D・音声を編集し、MP4を書き出すWindows用の専用エディタ。

## EXEで使う

[Releases](../../releases/latest) から `TimelineVideoStudio-*-Windows-x64.zip` をダウンロードし、ZIP全体を展開して `TimelineVideoStudio.exe` を起動する。Unity Editorのインストールは不要。EXEと同じフォルダーにあるDLL・データフォルダーも一緒に保持する。FFmpeg／FFprobeは不足時に自動取得する。

## ソースからビルドする

Unity 6000.3.18f1で開き、`Tools > Timeline Video Studio > Create Scene` の後に `Build Windows App` を選ぶ。

操作は [OPERATIONS.md](OPERATIONS.md)、外部 API は [CONTROL_API.md](CONTROL_API.md) を参照。Windows EXE は `Build/TimelineVideoStudio.exe`。エフェクトのサンプル定義は `Build/Plugins/light_shake.json`。

このプロジェクト専用の作業指示は [AGENTS.md](AGENTS.md)。動画制作と効果音使用のスキルを [.agents/skills](.agents/skills) に同梱している。

GitHubにはUnityのソース、ProjectSettings、Packages、プラグイン定義、操作スクリプト、説明資料を保存する。Unityのキャッシュ、ローカルのビルド、バックアップ、制作動画の素材は各作業環境で管理する。

FFmpeg／FFprobeは、起動時に見つからなければ自動インストールする。ユーザーのアプリデータ領域へ取得し、ダウンロードのSHA-256と実行を検証してから使う。管理者権限やソースの書き換えは不要。初回の自動取得にはインターネット接続が必要で、準備状況は画面に表示する。MP4出力は準備完了を待って開始する。

既存のツールを使う場合は、EXEの隣の `tools/`・`tools/bin/`・`bin/`・EXEと同じフォルダー、またはPATHへ置く。`TIMELINE_STUDIO_FFMPEG`・`TIMELINE_STUDIO_FFPROBE` で実行ファイルやフォルダーを指定できる。自動インストール先は `TIMELINE_STUDIO_TOOLS_DIR` で変更できる。取得元は [GyanのWindows用FFmpegビルド](https://www.gyan.dev/ffmpeg/builds/)。

APIの既定接続先は `http://127.0.0.1:18764`。アプリは `TIMELINE_STUDIO_API_PORT`、Pythonクライアントは同じ環境変数、`TIMELINE_STUDIO_API_URL`、または `python scripts/studio_cli.py --api http://127.0.0.1:18765 state` で接続先を変更できる。PythonのAPIクライアントと効果音同期スクリプトはPython 3.10以降の標準ライブラリで動作する。

素材パスは編集JSONからの相対パスで保存し、読み込み時に解決する。別のPCへ移す場合は、JSONと参照する素材を同じフォルダー構成で移す。別ドライブの素材は絶対パスのまま保存するため、移動前にJSONと同じドライブへ素材をまとめる。旧JSONの絶対パスにも対応する。

アプリ内では素材ファイルのあるフォルダを開き、画像・WAV・OBJを選んでタイムラインに追加する。CubeとSphereも追加できる。JSON形式のプロジェクトを保存して編集を再開できる。NVIDIA GPUのある環境ではNVENC、その他の環境ではCPUエンコードを初期選択する。
