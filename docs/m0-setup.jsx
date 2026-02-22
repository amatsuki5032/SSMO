import { useState } from "react";

const steps = [
  {
    id: 1,
    title: "Unity Hub インストール",
    time: "10分",
    color: "#6366F1",
    detail: "Unityのランチャー。全バージョンの管理・プロジェクト管理に必要。",
    actions: [
      { text: "https://unity.com/download にアクセス", type: "link", url: "https://unity.com/download" },
      { text: "「Download for Windows」をクリック → インストーラー実行", type: "action" },
      { text: "Unity ID を作成してログイン（Google連携でもOK）", type: "action" },
      { text: "ライセンス: 「Unity Personal」を選択（無料・売上20万ドルまで）", type: "action" },
    ],
    note: null,
  },
  {
    id: 2,
    title: "Unity 6 LTS インストール",
    time: "30〜60分（DL待ち）",
    color: "#EC4899",
    detail: "Unity 6 が最新のLTS。NGO（Netcode for GameObjects）との互換性もベスト。",
    actions: [
      { text: "Unity Hub → 左メニュー「Installs」→「Install Editor」", type: "action" },
      { text: "「Unity 6」の LTS 版を選択（6000.x.xxf1 LTS）", type: "action" },
      { text: "モジュール選択で以下にチェック:", type: "action" },
      { text: "  ✅ Microsoft Visual Studio Community（C#エディタ）", type: "check" },
      { text: "  ✅ Dedicated Server Build Support - Windows", type: "check" },
      { text: "  ✅ Dedicated Server Build Support - Linux", type: "check" },
      { text: "  ☐ Android / iOS / WebGL は今は不要（後で追加可）", type: "skip" },
      { text: "「Install」→ ダウンロード開始（約5〜10GB）", type: "action" },
    ],
    note: "⏰ DL中に Step 3〜5 を並行してやれる",
  },
  {
    id: 3,
    title: "Git LFS セットアップ",
    time: "5分",
    color: "#10B981",
    detail: "3Dモデル・テクスチャ等の大きいファイルをGitで管理するために必須。",
    actions: [
      { text: "Git がインストールされているか確認: git --version", type: "cmd" },
      { text: "未インストールなら https://git-scm.com/downloads からDL", type: "action" },
      { text: "Git LFS インストール:", type: "action" },
      { text: "git lfs install", type: "cmd" },
      { text: "これでグローバル設定完了。リポジトリ側の設定は Step 6 で行う。", type: "action" },
    ],
    note: null,
  },
  {
    id: 4,
    title: "GitHub リポジトリ作成",
    time: "5分",
    color: "#F59E0B",
    detail: "amatsuki5032 アカウントでリポジトリを作成。",
    actions: [
      { text: "GitHub → 「New repository」", type: "action" },
      { text: "リポジトリ名: musou-online（仮）", type: "action" },
      { text: "Visibility: Private（開発中は非公開推奨）", type: "action" },
      { text: "Add .gitignore: 「Unity」を選択", type: "action" },
      { text: "Add a license: MIT License（後で変更可）", type: "action" },
      { text: "「Create repository」", type: "action" },
    ],
    note: null,
  },
  {
    id: 5,
    title: "ローカルにクローン & 構造作成",
    time: "10分",
    color: "#8B5CF6",
    detail: "リポジトリをクローンして、ドキュメントフォルダだけ先に作っておく。",
    actions: [
      { text: "git clone https://github.com/amatsuki5032/musou-online.git", type: "cmd" },
      { text: "cd musou-online", type: "cmd" },
      { text: "以下のフォルダを作成:", type: "action" },
      { text: "mkdir docs", type: "cmd" },
      { text: "docs/ フォルダに設計メモを置いていく場所（今はOK）", type: "action" },
      { text: "※ Unity プロジェクトフォルダはStep 6で作成", type: "action" },
    ],
    note: null,
  },
  {
    id: 6,
    title: "Unity プロジェクト作成 & 初期設定",
    time: "15分",
    color: "#EF4444",
    detail: "Unity Hub からプロジェクトを作成し、ネットワーク関連パッケージを入れる。",
    actions: [
      { text: "Unity Hub →「Projects」→「New Project」", type: "action" },
      { text: "テンプレート:「3D (Built-in Render Pipeline)」を選択", type: "action" },
      { text: "  ※ URP でもOKだが、最初はBuilt-inのほうがシンプル", type: "skip" },
      { text: "Project Name: musou-online", type: "action" },
      { text: "Location: クローンしたリポジトリのルート直下に作成", type: "action" },
      { text: "  ※ リポジトリ直下 = Unityプロジェクトのルート にする", type: "check" },
      { text: "「Create project」→ Unity エディタが起動", type: "action" },
    ],
    note: "フォルダ構成は後述の「プロジェクト構造」参照",
  },
  {
    id: 7,
    title: "ネットワークパッケージ導入",
    time: "10分",
    color: "#06B6D4",
    detail: "NGO（Netcode for GameObjects）と関連パッケージをインストール。",
    actions: [
      { text: "Unity → Window → Package Manager", type: "action" },
      { text: "左上「+」→「Add package by name」で以下を順に追加:", type: "action" },
      { text: "com.unity.netcode.gameobjects", type: "cmd" },
      { text: "  → Netcode for GameObjects（メインのネットワークライブラリ）", type: "check" },
      { text: "com.unity.multiplayer.tools", type: "cmd" },
      { text: "  → Multiplayer Tools（ネットワーク統計・プロファイラ）", type: "check" },
      { text: "Unity Transport は NGO の依存で自動インストールされる", type: "action" },
      { text: "パッケージ追加後、コンパイルエラーが出ないことを確認", type: "action" },
    ],
    note: null,
  },
  {
    id: 8,
    title: "ParrelSync 導入（マルチテスト用）",
    time: "5分",
    color: "#F97316",
    detail: "エディタを複数起動してローカルでマルチプレイテストできる神ツール。",
    actions: [
      { text: "Package Manager →「+」→「Add package from git URL」", type: "action" },
      { text: "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync", type: "cmd" },
      { text: "  → Enter で追加", type: "action" },
      { text: "メニューに「ParrelSync」が追加されたことを確認", type: "action" },
      { text: "ParrelSync → Clones Manager → 「Create new clone」", type: "action" },
      { text: "  → プロジェクトのクローン（シンボリックリンク）が作成される", type: "action" },
      { text: "これで2つのUnityエディタで同時テスト可能に", type: "action" },
    ],
    note: "⚠️ クローン側は読み取り専用。コード編集はメイン側で行う。",
  },
  {
    id: 9,
    title: "プロジェクト初期設定",
    time: "10分",
    color: "#D946EF",
    detail: "FixedUpdate 60Hz 設定やフォルダ構成など。",
    actions: [
      { text: "Edit → Project Settings → Time", type: "action" },
      { text: "Fixed Timestep: 0.01667（= 1/60 = 60Hz固定ティック）", type: "check" },
      { text: "Edit → Project Settings → Player", type: "action" },
      { text: "VSync Count: Don't Sync（描画FPS可変にするため）", type: "check" },
      { text: "Application.targetFrameRate は設定画面から変更可能にする（後で実装）", type: "action" },
      { text: "Assets フォルダ内に以下のサブフォルダを作成:", type: "action" },
      { text: "  Scripts/  Prefabs/  Scenes/  Materials/  Models/  Effects/", type: "check" },
      { text: "Scripts/ の中にさらに:", type: "action" },
      { text: "  Netcode/  Combat/  Character/  UI/  Shared/  Server/", type: "check" },
    ],
    note: null,
  },
  {
    id: 10,
    title: "最初の動作確認：Hello Network",
    time: "20分",
    color: "#10B981",
    detail: "NGOで「ホスト起動 → クライアント接続 → ログ表示」を確認。全ての土台。",
    actions: [
      { text: "空の GameObject 作成 →「NetworkManager」にリネーム", type: "action" },
      { text: "Add Component →「Network Manager」", type: "action" },
      { text: "Network Transport に「Unity Transport」を追加", type: "action" },
      { text: "新規 C# スクリプト「HelloNetwork.cs」を作成:", type: "action" },
      { text: "（下の「最初のコード」参照）", type: "action" },
      { text: "Play → 画面のボタンで Host 起動", type: "action" },
      { text: "ParrelSync のクローンを開く → Play → Client で接続", type: "action" },
      { text: "Console に「Client connected!」が出れば成功 🎉", type: "action" },
    ],
    note: "🎉 これが動けば M0 完了。ネットワーク対戦ゲームの第一歩！",
  },
];

const folderStructure = [
  { path: "musou-online/", indent: 0, type: "root", note: "リポジトリルート = Unityプロジェクトルート" },
  { path: "├── Assets/", indent: 1, type: "folder", note: "" },
  { path: "│   ├── Scripts/", indent: 2, type: "folder", note: "" },
  { path: "│   │   ├── Netcode/", indent: 3, type: "folder", note: "ネットワーク同期・予測・補間" },
  { path: "│   │   ├── Combat/", indent: 3, type: "folder", note: "ヒット判定・ダメージ・コンボ" },
  { path: "│   │   ├── Character/", indent: 3, type: "folder", note: "移動・ステート・アニメ" },
  { path: "│   │   ├── UI/", indent: 3, type: "folder", note: "HUD・メニュー・ロビー" },
  { path: "│   │   ├── Shared/", indent: 3, type: "folder", note: "定数・計算式・データ定義" },
  { path: "│   │   └── Server/", indent: 3, type: "folder", note: "サーバー専用ロジック・AI" },
  { path: "│   ├── Prefabs/", indent: 2, type: "folder", note: "キャラ・弾・エフェクト等" },
  { path: "│   ├── Scenes/", indent: 2, type: "folder", note: "" },
  { path: "│   ├── Models/", indent: 2, type: "folder", note: "3Dモデル (.fbx, .gltf)" },
  { path: "│   ├── Materials/", indent: 2, type: "folder", note: "" },
  { path: "│   └── Effects/", indent: 2, type: "folder", note: "パーティクル・シェーダー" },
  { path: "├── docs/", indent: 1, type: "folder", note: "設計ドキュメント" },
  { path: "├── .gitignore", indent: 1, type: "file", note: "Unity用（GitHub自動生成）" },
  { path: "├── .gitattributes", indent: 1, type: "file", note: "Git LFS 設定" },
  { path: "└── README.md", indent: 1, type: "file", note: "" },
];

const gitattributes = `# 3D Models
*.fbx filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text
*.gltf filter=lfs diff=lfs merge=lfs -text
*.glb filter=lfs diff=lfs merge=lfs -text

# Textures
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
*.hdr filter=lfs diff=lfs merge=lfs -text

# Audio
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text

# Unity
*.unitypackage filter=lfs diff=lfs merge=lfs -text
*.asset filter=lfs diff=lfs merge=lfs -text`;

const helloNetworkCode = `using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 最初のネットワーク動作確認スクリプト
/// シーン上の GameObject にアタッチして使う
/// </summary>
public class HelloNetwork : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host (Server + Client)"))
                NetworkManager.Singleton.StartHost();

            if (GUILayout.Button("Client"))
                NetworkManager.Singleton.StartClient();

            if (GUILayout.Button("Server (Dedicated)"))
                NetworkManager.Singleton.StartServer();
        }
        else
        {
            GUILayout.Label($"Mode: {(NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client")}");
            GUILayout.Label($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            if (NetworkManager.Singleton.IsServer)
            {
                // サーバー側: 接続イベントを監視
                // （実際はStart時に一度だけ登録すべきだが、
                //   動作確認用なので簡易実装）
            }
        }

        GUILayout.EndArea();
    }

    void Start()
    {
        // クライアント接続時のコールバック
        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log($"Client connected! ID: {clientId}");
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += (clientId) =>
        {
            Debug.Log($"Client disconnected! ID: {clientId}");
        };
    }
}`;

export default function M0Guide() {
  const [completed, setCompleted] = useState({});
  const [activeCode, setActiveCode] = useState(null);

  const toggleStep = (id) => setCompleted(prev => ({ ...prev, [id]: !prev[id] }));
  const completedCount = Object.values(completed).filter(Boolean).length;
  const progress = (completedCount / steps.length) * 100;

  return (
    <div style={{
      minHeight: "100vh",
      background: "#0B0B10",
      color: "#E4E4E7",
      fontFamily: "'Noto Sans JP', sans-serif",
      padding: "20px 16px",
    }}>
      <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@300;400;500;700;900&family=JetBrains+Mono:wght@400;500;700&family=Orbitron:wght@500;700;900&display=swap" rel="stylesheet" />

      {/* Header */}
      <div style={{
        textAlign: "center", marginBottom: 20, padding: "22px 16px",
        background: "linear-gradient(160deg, #0f0a1e 0%, #0a1220 100%)",
        borderRadius: 14, border: "1px solid #ffffff10",
      }}>
        <div style={{ fontFamily: "Orbitron", fontSize: 10, letterSpacing: 4, color: "#6366F1", fontWeight: 700, marginBottom: 4 }}>MILESTONE 0</div>
        <h1 style={{ fontSize: 22, fontWeight: 900, margin: "0 0 4px", color: "#FAFAFA" }}>環境構築 & 初回セットアップ</h1>
        <div style={{ fontSize: 11, color: "#71717A" }}>Unity インストール → GitHub → NGO導入 → 最初の通信確認</div>

        {/* Progress Bar */}
        <div style={{ marginTop: 16, background: "#1E1E28", borderRadius: 8, height: 8, overflow: "hidden" }}>
          <div style={{
            width: `${progress}%`, height: "100%",
            background: progress === 100 ? "linear-gradient(90deg, #10B981, #34D399)" : "linear-gradient(90deg, #6366F1, #818CF8)",
            borderRadius: 8, transition: "width 0.3s ease",
          }} />
        </div>
        <div style={{ fontSize: 12, color: "#71717A", marginTop: 6 }}>
          {completedCount} / {steps.length} 完了
          {progress === 100 && <span style={{ color: "#10B981", marginLeft: 8 }}>🎉 M0 クリア！</span>}
        </div>
      </div>

      {/* Steps */}
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {steps.map((step) => {
          const isDone = completed[step.id];
          return (
            <div key={step.id} style={{
              background: "#16161C",
              borderRadius: 12,
              border: `1px solid ${isDone ? "#10B98140" : step.color + "25"}`,
              overflow: "hidden",
              opacity: isDone ? 0.7 : 1,
              transition: "all 0.2s ease",
            }}>
              {/* Step Header */}
              <div style={{
                display: "flex", alignItems: "center", gap: 10, padding: "14px 16px",
              }}>
                <button onClick={() => toggleStep(step.id)} style={{
                  width: 28, height: 28, borderRadius: 8, border: `2px solid ${isDone ? "#10B981" : step.color}`,
                  background: isDone ? "#10B98120" : "transparent", cursor: "pointer",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  fontSize: 14, color: isDone ? "#10B981" : "transparent", flexShrink: 0,
                }}>
                  ✓
                </button>
                <div style={{ flex: 1 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{ fontSize: 11, fontWeight: 700, color: step.color, fontFamily: "Orbitron" }}>
                      STEP {step.id}
                    </span>
                    <span style={{ fontSize: 15, fontWeight: 700, color: isDone ? "#71717A" : "#FAFAFA", textDecoration: isDone ? "line-through" : "none" }}>
                      {step.title}
                    </span>
                  </div>
                  <div style={{ fontSize: 11, color: "#71717A", marginTop: 2 }}>{step.detail}</div>
                </div>
                <div style={{
                  padding: "3px 10px", background: step.color + "15", borderRadius: 6,
                  fontSize: 11, color: step.color, fontWeight: 600, flexShrink: 0,
                }}>⏱ {step.time}</div>
              </div>

              {/* Actions */}
              {!isDone && (
                <div style={{ padding: "0 16px 14px", borderTop: "1px solid #1E1E28", paddingTop: 10 }}>
                  {step.actions.map((action, i) => (
                    <div key={i} style={{
                      display: "flex", alignItems: "flex-start", gap: 8, marginBottom: 5,
                      fontSize: 12, lineHeight: 1.6,
                      paddingLeft: action.type === "check" || action.type === "skip" ? 12 : 0,
                    }}>
                      <span style={{
                        flexShrink: 0, marginTop: 2,
                        color: action.type === "cmd" ? "#06B6D4"
                          : action.type === "check" ? "#10B981"
                          : action.type === "skip" ? "#71717A"
                          : step.color,
                      }}>
                        {action.type === "cmd" ? "$" : action.type === "check" ? "✅" : action.type === "skip" ? "⬜" : "▸"}
                      </span>
                      {action.type === "cmd" ? (
                        <code style={{
                          background: "#0D0D14", padding: "2px 8px", borderRadius: 4,
                          fontFamily: "JetBrains Mono", fontSize: 11, color: "#06B6D4",
                          border: "1px solid #1E1E28",
                        }}>{action.text}</code>
                      ) : action.type === "link" ? (
                        <span style={{ color: "#818CF8" }}>{action.text}</span>
                      ) : (
                        <span style={{ color: action.type === "skip" ? "#52525B" : "#A1A1AA" }}>{action.text}</span>
                      )}
                    </div>
                  ))}
                  {step.note && (
                    <div style={{
                      marginTop: 8, padding: "8px 12px", background: "#F59E0B10", borderRadius: 6,
                      fontSize: 11, color: "#FBBF24", borderLeft: "3px solid #F59E0B40",
                    }}>{step.note}</div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Code Sections */}
      <div style={{ marginTop: 20 }}>
        {/* .gitattributes */}
        <div style={{ background: "#16161C", borderRadius: 12, border: "1px solid #27272A", marginBottom: 10, overflow: "hidden" }}>
          <div
            onClick={() => setActiveCode(activeCode === "git" ? null : "git")}
            style={{ padding: "12px 16px", cursor: "pointer", display: "flex", alignItems: "center", gap: 8 }}
          >
            <span style={{ fontSize: 13, fontWeight: 700, color: "#10B981" }}>📄 .gitattributes（Git LFS 設定）</span>
            <span style={{ marginLeft: "auto", fontSize: 10, color: "#52525B" }}>{activeCode === "git" ? "▲" : "▼"}</span>
          </div>
          {activeCode === "git" && (
            <div style={{ padding: "0 16px 14px", borderTop: "1px solid #1E1E28", paddingTop: 10 }}>
              <div style={{ fontSize: 11, color: "#71717A", marginBottom: 8 }}>
                リポジトリのルートに配置。3Dモデル・テクスチャ・音声ファイルをLFSで管理する。
              </div>
              <pre style={{
                background: "#0D0D14", padding: 12, borderRadius: 8, fontSize: 11,
                fontFamily: "JetBrains Mono", color: "#A1A1AA", overflowX: "auto",
                border: "1px solid #1E1E28", lineHeight: 1.6,
              }}>{gitattributes}</pre>
            </div>
          )}
        </div>

        {/* HelloNetwork.cs */}
        <div style={{ background: "#16161C", borderRadius: 12, border: "1px solid #27272A", marginBottom: 10, overflow: "hidden" }}>
          <div
            onClick={() => setActiveCode(activeCode === "hello" ? null : "hello")}
            style={{ padding: "12px 16px", cursor: "pointer", display: "flex", alignItems: "center", gap: 8 }}
          >
            <span style={{ fontSize: 13, fontWeight: 700, color: "#06B6D4" }}>📄 HelloNetwork.cs（最初の動作確認コード）</span>
            <span style={{ marginLeft: "auto", fontSize: 10, color: "#52525B" }}>{activeCode === "hello" ? "▲" : "▼"}</span>
          </div>
          {activeCode === "hello" && (
            <div style={{ padding: "0 16px 14px", borderTop: "1px solid #1E1E28", paddingTop: 10 }}>
              <div style={{ fontSize: 11, color: "#71717A", marginBottom: 8 }}>
                Assets/Scripts/Netcode/ に配置。シーン上の空GameObjectにアタッチ。
                NetworkManager と同じ GameObject でも別でもOK。
              </div>
              <pre style={{
                background: "#0D0D14", padding: 12, borderRadius: 8, fontSize: 11,
                fontFamily: "JetBrains Mono", color: "#A1A1AA", overflowX: "auto",
                border: "1px solid #1E1E28", lineHeight: 1.5, whiteSpace: "pre-wrap",
              }}>{helloNetworkCode}</pre>
            </div>
          )}
        </div>

        {/* Folder Structure */}
        <div style={{ background: "#16161C", borderRadius: 12, border: "1px solid #27272A", overflow: "hidden" }}>
          <div
            onClick={() => setActiveCode(activeCode === "folder" ? null : "folder")}
            style={{ padding: "12px 16px", cursor: "pointer", display: "flex", alignItems: "center", gap: 8 }}
          >
            <span style={{ fontSize: 13, fontWeight: 700, color: "#F59E0B" }}>📁 プロジェクト構造</span>
            <span style={{ marginLeft: "auto", fontSize: 10, color: "#52525B" }}>{activeCode === "folder" ? "▲" : "▼"}</span>
          </div>
          {activeCode === "folder" && (
            <div style={{ padding: "0 16px 14px", borderTop: "1px solid #1E1E28", paddingTop: 10 }}>
              {folderStructure.map((item, i) => (
                <div key={i} style={{
                  display: "flex", alignItems: "center", gap: 8,
                  fontSize: 12, fontFamily: "JetBrains Mono",
                  color: item.type === "root" ? "#F59E0B" : item.type === "folder" ? "#818CF8" : "#71717A",
                  marginBottom: 2,
                }}>
                  <span>{item.path}</span>
                  {item.note && <span style={{ fontSize: 10, color: "#52525B", fontFamily: "'Noto Sans JP', sans-serif" }}>← {item.note}</span>}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* First Commit */}
      <div style={{
        marginTop: 20, padding: 16, background: "#16161C", borderRadius: 12,
        border: "1px solid #10B98130",
      }}>
        <div style={{ fontSize: 14, fontWeight: 700, color: "#10B981", marginBottom: 10 }}>🎉 全部終わったら最初のコミット</div>
        <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          {[
            "git add -A",
            'git commit -m "M0: Initial Unity project with NGO networking"',
            "git push origin main",
          ].map((cmd, i) => (
            <div key={i} style={{
              display: "flex", alignItems: "center", gap: 8, fontSize: 12,
            }}>
              <span style={{ color: "#06B6D4" }}>$</span>
              <code style={{
                background: "#0D0D14", padding: "3px 10px", borderRadius: 4,
                fontFamily: "JetBrains Mono", fontSize: 11, color: "#06B6D4",
                border: "1px solid #1E1E28",
              }}>{cmd}</code>
            </div>
          ))}
        </div>
        <div style={{
          marginTop: 12, padding: "10px 14px", background: "#F59E0B10", borderRadius: 8,
          fontSize: 12, color: "#FBBF24", lineHeight: 1.8,
        }}>
          💡 ここまでできれば M0 完了。次の M1 では「2人が同じ空間を走り回る」ネットワーク移動同期に入ります。
          箱人間でいいので、まず動かすことが大事。
        </div>
      </div>
    </div>
  );
}
