import { useState } from "react";

const milestones = [
  {
    id: "m0",
    phase: "MILESTONE 0",
    title: "リポジトリ作成 & 環境構築",
    period: "Week 1〜2",
    color: "#6366F1",
    icon: "🏗️",
    goal: "開発基盤を整える。ここが雑だと後で全部崩れる。",
    tasks: [
      {
        name: "GitHub リポジトリ作成",
        detail: "リポジトリ名例: dwo-project / musou-online",
        subtasks: [
          "git init → GitHub に push",
          "README.md 作成（プロジェクト概要・目標）",
          ".gitignore 設定（node_modules, dist, .env 等）",
          "LICENSE 選択（個人開発なら MIT or AGPL）",
          "Issue テンプレート & PR テンプレート作成",
        ],
        priority: "必須",
      },
      {
        name: "技術選定 & 確定",
        detail: "エンジン・フレームワークを決めて以降ブレない",
        subtasks: [
          "ゲームエンジン: Three.js（Web3D）or Babylon.js or Unity WebGL",
          "サーバー: Node.js + Socket.io（リアルタイム通信）",
          "DB: Firebase Firestore（得意分野を活かす）",
          "認証: Firebase Auth（Google / メール）",
          "ホスティング: Firebase Hosting + Cloud Functions",
          "ビルド: Vite + TypeScript（推奨）or vanilla JS",
        ],
        priority: "必須",
      },
      {
        name: "プロジェクト構造作成",
        detail: "モノレポ or クライアント/サーバー分離",
        subtasks: [
          "client/ — ゲームクライアント（Three.js等）",
          "server/ — ゲームサーバー（Node.js + Socket.io）",
          "shared/ — 共有型定義・定数・計算式",
          "tools/ — データ変換・マスタ管理ツール",
          "docs/ — 設計ドキュメント",
          "package.json / tsconfig.json 設定",
        ],
        priority: "必須",
      },
      {
        name: "CI/CD パイプライン",
        detail: "自動テスト・自動デプロイの仕組み",
        subtasks: [
          "GitHub Actions で lint / test 自動実行",
          "Firebase Hosting への自動デプロイ",
          "ブランチ戦略: main(本番) / develop(開発) / feature/*",
        ],
        priority: "推奨",
      },
    ],
  },
  {
    id: "m1",
    phase: "MILESTONE 1",
    title: "3D空間 & キャラ移動（素体）",
    period: "Week 3〜6",
    color: "#EC4899",
    icon: "🏃",
    goal: "3D空間内でキャラクターが動く。これがすべての土台。",
    tasks: [
      {
        name: "3D シーン構築",
        detail: "Three.js or Babylon.js で基本シーン",
        subtasks: [
          "平面フィールド（100m×100m程度）の作成",
          "カメラ（TPS視点）のセットアップ",
          "基本ライティング（DirectionalLight + AmbientLight）",
          "スカイボックス or グラデーション背景",
          "FPS カウンター表示",
        ],
        priority: "必須",
      },
      {
        name: "プレイヤーキャラクター",
        detail: "まずはボックス or 無料モデルで動かす",
        subtasks: [
          "キャラクター表示（最初は BoxGeometry でOK）",
          "WASD / 矢印キーで移動",
          "移動アニメーション（歩き・走り）",
          "カメラ追従（プレイヤーの後方固定）",
          "マウスドラッグでカメラ回転",
        ],
        priority: "必須",
      },
      {
        name: "入力システム",
        detail: "キーボード＋マウス＋ゲームパッド対応",
        subtasks: [
          "InputManager クラス作成",
          "キーバインド設定（変更可能に）",
          "Gamepad API 対応（任意）",
          "モバイルタッチ対応（バーチャルスティック）は後回し",
        ],
        priority: "必須",
      },
      {
        name: "無料3Dモデル導入",
        detail: "Mixamo / ReadyPlayerMe 等から仮モデル",
        subtasks: [
          "GLTFLoader でモデル読み込み",
          "Mixamo からアニメーション付きモデル取得",
          "Idle / Walk / Run アニメーション切り替え",
          "AnimationMixer でブレンド制御",
        ],
        priority: "推奨",
      },
    ],
  },
  {
    id: "m2",
    phase: "MILESTONE 2",
    title: "アクション戦闘システム（コア）",
    period: "Week 7〜14",
    color: "#EF4444",
    icon: "⚔️",
    goal: "無双っぽい爽快アクション。ゲームの心臓部。",
    tasks: [
      {
        name: "基本攻撃コンボ",
        detail: "通常攻撃（□）N1〜N6 のコンボチェーン",
        subtasks: [
          "攻撃ボタン入力→攻撃ステート遷移",
          "コンボカウンター管理（N1→N2→...→N6）",
          "入力受付時間ウィンドウ（タイミング猶予）",
          "攻撃モーション再生（Mixamoの攻撃アニメ）",
          "ヒットストップ演出（短時間のフリーズ）",
        ],
        priority: "必須",
      },
      {
        name: "チャージ攻撃",
        detail: "△ボタンで派生するコンボ分岐",
        subtasks: [
          "N1△ → C1（気絶系）",
          "N2△ → C2（打ち上げ系）",
          "N3△ → C3（範囲攻撃）",
          "N4△ → C4（吹き飛ばし）",
          "チャージ攻撃ごとのエフェクト差別化",
        ],
        priority: "必須",
      },
      {
        name: "ヒット判定システム",
        detail: "当たり判定の実装",
        subtasks: [
          "攻撃ヒットボックス（球 or カプセル）生成",
          "敵との衝突検出（距離 + 角度判定）",
          "ダメージ計算式の基礎実装",
          "ヒットエフェクト（パーティクル）",
          "ダメージ数字表示（フローティングテキスト）",
        ],
        priority: "必須",
      },
      {
        name: "敵AI（雑兵）",
        detail: "一騎当千の「千」の部分",
        subtasks: [
          "雑兵の自動生成（スポーンシステム）",
          "基本AI: 待機→接近→攻撃→のけぞり→死亡",
          "群衆制御（30〜50体同時表示目標）",
          "LOD: 遠い敵は簡略化モデル",
          "死亡演出（吹っ飛び + フェードアウト）",
        ],
        priority: "必須",
      },
      {
        name: "無双乱舞",
        detail: "ゲージ消費の必殺技",
        subtasks: [
          "無双ゲージUI（画面下部）",
          "ゲージ蓄積条件（敵撃破 / 被ダメージ）",
          "無双乱舞発動（○ボタン長押し）",
          "無双乱舞中の無敵時間",
          "専用エフェクト・カメラ演出",
        ],
        priority: "高",
      },
      {
        name: "キャラクターステート管理",
        detail: "有限状態マシン（FSM）で管理",
        subtasks: [
          "Idle / Move / Attack / Charge / Musou / Damage / Dead",
          "ステート遷移ルール定義",
          "ステートごとの入力受付制御",
          "スーパーアーマー（のけぞり耐性）管理",
        ],
        priority: "必須",
      },
    ],
  },
  {
    id: "m3",
    phase: "MILESTONE 3",
    title: "マップ & ミッション構造",
    period: "Week 15〜20",
    color: "#F59E0B",
    icon: "🗺️",
    goal: "合戦マップでミッションをこなす一連のゲームフロー。",
    tasks: [
      {
        name: "合戦マップ制作",
        detail: "最初の1ステージ（官渡の戦い等）",
        subtasks: [
          "マップデータ形式の設計（JSON定義）",
          "地形メッシュ作成（Blender → GLTF）",
          "拠点配置（味方本陣・敵本陣・中立拠点）",
          "拠点制圧システム（エリア内に一定時間→制圧）",
          "ミニマップUI表示",
        ],
        priority: "必須",
      },
      {
        name: "ミッションシステム",
        detail: "戦闘中に発生するサブミッション",
        subtasks: [
          "ミッション定義データ（JSON: 条件/報酬/テキスト）",
          "トリガー条件: 時間経過 / 拠点制圧 / 敵将撃破",
          "ミッション成功/失敗判定",
          "ミッション通知UI（画面右上）",
          "勝利/敗北条件管理",
        ],
        priority: "必須",
      },
      {
        name: "敵武将AI",
        detail: "雑兵よりも強い名前付き武将",
        subtasks: [
          "武将ステータス（HP, 攻撃力, 防御力, スキル）",
          "武将AI: ガード / 回避 / コンボ使用",
          "武将撃破演出（スローモーション等）",
          "武将ドロップ（経験値・装備）",
        ],
        priority: "高",
      },
      {
        name: "リザルト画面",
        detail: "戦闘終了後のスコア表示",
        subtasks: [
          "撃破数 / クリアタイム / 取得経験値 / ランク",
          "ドロップアイテム一覧",
          "MVP演出（あれば）",
          "リトライ / ロビーへ戻るボタン",
        ],
        priority: "必須",
      },
    ],
  },
  {
    id: "m4",
    phase: "MILESTONE 4",
    title: "オンラインマルチプレイ",
    period: "Week 21〜30",
    color: "#10B981",
    icon: "🌐",
    goal: "複数人で同時に戦場を共有する。最大の技術的チャレンジ。",
    tasks: [
      {
        name: "Socket.io サーバー基盤",
        detail: "リアルタイム通信の心臓部",
        subtasks: [
          "Node.js + Socket.io サーバー構築",
          "ルーム作成・参加・退出フロー",
          "サーバー権威型の位置同期（クライアント予測付き）",
          "ティックレート設定（20〜30Hz目標）",
          "遅延補正（ラグコンペンセーション）基礎",
        ],
        priority: "必須",
      },
      {
        name: "マッチメイキング",
        detail: "対戦相手のマッチングシステム",
        subtasks: [
          "ロビー画面UI（ルーム一覧 / クイックマッチ）",
          "Firestore でルーム管理（作成/検索/参加）",
          "対戦モード: 4v4 拠点争奪戦",
          "チーム振り分け（ランダム / 手動選択）",
          "準備完了→カウントダウン→開始フロー",
        ],
        priority: "必須",
      },
      {
        name: "同期処理",
        detail: "プレイヤー間のゲーム状態一致",
        subtasks: [
          "プレイヤー位置・回転のリアルタイム同期",
          "攻撃判定のサーバー検証",
          "拠点状態の同期（全クライアント一致）",
          "敵NPCの同期（サーバーがAI実行）",
          "補間（Interpolation）でカクつき軽減",
        ],
        priority: "必須",
      },
      {
        name: "チャット & コミュニケーション",
        detail: "プレイヤー間の交流手段",
        subtasks: [
          "テキストチャット（Socket.io）",
          "定型文チャット（「援軍求む！」等）",
          "チームチャット / 全体チャット切り替え",
        ],
        priority: "中",
      },
    ],
  },
  {
    id: "m5",
    phase: "MILESTONE 5",
    title: "キャラ育成 & 経済システム",
    period: "Week 31〜38",
    color: "#8B5CF6",
    icon: "📈",
    goal: "やり込み要素。プレイヤーが繰り返し遊ぶ理由を作る。",
    tasks: [
      {
        name: "キャラクター成長",
        detail: "レベル・ステータス・スキル",
        subtasks: [
          "経験値 & レベルシステム",
          "ステータス: 体力/攻撃/防御/速度/無双",
          "レベルアップ時のステータス上昇",
          "スキルツリー（パッシブ/アクティブスキル）",
          "武器種ごとの固有技能",
        ],
        priority: "必須",
      },
      {
        name: "装備システム",
        detail: "武器・防具の収集と強化",
        subtasks: [
          "武器: レアリティ（★1〜★5）/ 属性 / オプション",
          "武器強化（素材消費）",
          "防具・アクセサリスロット",
          "装備セット効果",
          "ドロップテーブル定義（マップごと）",
        ],
        priority: "必須",
      },
      {
        name: "ショップ & 通貨",
        detail: "ゲーム内経済の基盤",
        subtasks: [
          "ゲーム内通貨（金貨: 戦闘報酬）",
          "NPCショップ（消耗品・基本装備）",
          "武器鍛冶（強化・合成）",
          "将来的な課金通貨の設計（任意）",
        ],
        priority: "高",
      },
      {
        name: "Firestore データ設計",
        detail: "永続データの保存構造",
        subtasks: [
          "users/{uid}: プロフィール・設定",
          "users/{uid}/characters/{id}: キャラデータ",
          "users/{uid}/inventory/{id}: 所持アイテム",
          "leaderboards/{mode}: ランキング",
          "セキュリティルール（不正アクセス防止）",
        ],
        priority: "必須",
      },
    ],
  },
  {
    id: "m6",
    phase: "MILESTONE 6",
    title: "ポリッシュ & α版リリース",
    period: "Week 39〜48",
    color: "#06B6D4",
    icon: "✨",
    goal: "遊べるものとして人に見せられる品質にする。",
    tasks: [
      {
        name: "UI/UX 仕上げ",
        detail: "全画面のデザイン統一",
        subtasks: [
          "タイトル画面 / ロビー / キャラ選択 / 戦闘HUD",
          "設定画面（キーバインド / 音量 / 画質）",
          "チュートリアル（初回プレイガイド）",
          "ロード画面（Tips表示）",
        ],
        priority: "必須",
      },
      {
        name: "エフェクト & 演出強化",
        detail: "無双ゲーらしいド派手な演出",
        subtasks: [
          "ヒットエフェクト強化（パーティクル）",
          "無双乱舞の演出強化",
          "拠点制圧時の演出",
          "天候・時間帯変化（任意）",
        ],
        priority: "高",
      },
      {
        name: "サウンド実装",
        detail: "BGM & SE",
        subtasks: [
          "戦闘BGM（フリー素材 or 自作）",
          "SE: 攻撃ヒット / 武将撃破 / 拠点制圧",
          "UI操作音",
          "Howler.js or Web Audio API で再生管理",
        ],
        priority: "高",
      },
      {
        name: "テスト & バグ修正",
        detail: "品質保証",
        subtasks: [
          "全機能の動作テスト",
          "マルチプレイの負荷テスト",
          "ブラウザ互換性チェック（Chrome / Firefox / Edge）",
          "パフォーマンス最適化（60fps目標）",
          "知人にテストプレイ依頼→フィードバック収集",
        ],
        priority: "必須",
      },
      {
        name: "α版公開",
        detail: "最初の公開リリース",
        subtasks: [
          "Firebase Hosting にデプロイ",
          "OGP設定（SNSシェア用）",
          "フィードバック用 Google Form / Discord",
          "α版告知（Twitter / コミュニティ）",
        ],
        priority: "必須",
      },
    ],
  },
];

const techDecisions = [
  {
    question: "ゲームエンジン（3D描画）",
    options: [
      { name: "Three.js", pros: "Web標準・学習資料豊富・軽量", cons: "物理エンジン別途必要・ゲーム特化機能なし", rec: true },
      { name: "Babylon.js", pros: "ゲーム向け機能充実・物理内蔵・TS対応", cons: "Three.jsより情報少なめ", rec: false },
      { name: "PlayCanvas", pros: "エディタ付き・チーム開発向け", cons: "クラウド依存・自由度低め", rec: false },
    ],
    recommendation: "Three.js: あまつきさんのJS経験と学習コストのバランスが最適。Babylon.jsも良い選択肢。",
  },
  {
    question: "リアルタイム通信",
    options: [
      { name: "Socket.io", pros: "Node.js親和性高・安定・資料豊富", cons: "大規模には向かない", rec: true },
      { name: "Colyseus", pros: "ゲームサーバー特化・状態同期自動", cons: "学習コスト", rec: false },
      { name: "WebRTC (P2P)", pros: "サーバー負荷低", cons: "チート対策困難・NAT問題", rec: false },
    ],
    recommendation: "Socket.io: まず動くものを作る段階ではこれが最速。規模拡大時にColyseusへ移行も可。",
  },
  {
    question: "バックエンド & DB",
    options: [
      { name: "Firebase", pros: "あまつきさんの得意分野！Auth/Firestore/Hosting一式", cons: "リアルタイム戦闘ロジックは別サーバー必要", rec: true },
      { name: "Supabase", pros: "PostgreSQL・リアルタイム・オープンソース", cons: "Firebase程の実績なし", rec: false },
    ],
    recommendation: "Firebase: 認証・永続データ・ホスティングはFirebase、戦闘サーバーは別途Node.jsが最適構成。",
  },
];

function TaskCard({ task, color }) {
  const [open, setOpen] = useState(false);
  const priorityColors = {
    "必須": "#EF4444",
    "高": "#F59E0B",
    "推奨": "#3B82F6",
    "中": "#6B7280",
  };
  return (
    <div style={{
      background: "#1E1E24",
      borderRadius: 10,
      border: `1px solid #2A2A32`,
      marginBottom: 8,
      overflow: "hidden",
    }}>
      <div
        onClick={() => setOpen(!open)}
        style={{
          padding: "12px 16px",
          cursor: "pointer",
          display: "flex",
          alignItems: "center",
          gap: 10,
        }}
      >
        <span style={{
          fontSize: 10,
          padding: "2px 8px",
          borderRadius: 4,
          background: (priorityColors[task.priority] || "#6B7280") + "25",
          color: priorityColors[task.priority] || "#6B7280",
          fontWeight: 700,
          flexShrink: 0,
        }}>
          {task.priority}
        </span>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 14, fontWeight: 600, color: "#F0F0F5" }}>{task.name}</div>
          <div style={{ fontSize: 11, color: "#71717A", marginTop: 2 }}>{task.detail}</div>
        </div>
        <span style={{ color: "#52525B", fontSize: 11, transition: "transform 0.2s", transform: open ? "rotate(180deg)" : "" }}>▼</span>
      </div>
      {open && (
        <div style={{ padding: "0 16px 14px", borderTop: "1px solid #2A2A32", paddingTop: 10 }}>
          {task.subtasks.map((st, i) => (
            <div key={i} style={{
              display: "flex",
              alignItems: "flex-start",
              gap: 8,
              marginBottom: 6,
              fontSize: 12,
              color: "#A1A1AA",
            }}>
              <span style={{ color: color, flexShrink: 0, marginTop: 1 }}>○</span>
              <span>{st}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default function DevPlan() {
  const [activePhase, setActivePhase] = useState("m0");
  const [showTech, setShowTech] = useState(false);

  const currentMilestone = milestones.find(m => m.id === activePhase);

  return (
    <div style={{
      minHeight: "100vh",
      background: "#111116",
      color: "#E4E4E7",
      fontFamily: "'Noto Sans JP', 'Inter', sans-serif",
      padding: "20px 16px",
    }}>
      <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@300;400;500;700;900&family=Orbitron:wght@500;700;900&display=swap" rel="stylesheet" />

      {/* Header */}
      <div style={{
        textAlign: "center",
        marginBottom: 24,
        padding: "28px 20px",
        background: "linear-gradient(160deg, #1a1025 0%, #0f1a2e 50%, #0a1628 100%)",
        borderRadius: 14,
        border: "1px solid #ffffff10",
        position: "relative",
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute", inset: 0,
          background: "radial-gradient(ellipse at 20% 50%, #6366F115 0%, transparent 50%), radial-gradient(ellipse at 80% 30%, #EC489915 0%, transparent 50%)",
        }} />
        <div style={{ position: "relative" }}>
          <div style={{ fontFamily: "Orbitron", fontSize: 11, letterSpacing: 4, color: "#6366F1", fontWeight: 700, marginBottom: 6 }}>
            DEVELOPMENT ROADMAP
          </div>
          <h1 style={{ fontSize: 26, fontWeight: 900, margin: "0 0 4px", color: "#FAFAFA" }}>
            真三國無双Online風ゲーム
          </h1>
          <div style={{ fontSize: 13, color: "#71717A" }}>リポジトリ作成 → α版リリースまでの実践計画</div>
          <div style={{ display: "flex", gap: 12, justifyContent: "center", marginTop: 16, flexWrap: "wrap" }}>
            <div style={{ padding: "6px 14px", background: "#6366F120", borderRadius: 8, fontSize: 12, color: "#818CF8" }}>
              📅 約48週間（12ヶ月）
            </div>
            <div style={{ padding: "6px 14px", background: "#EC489920", borderRadius: 8, fontSize: 12, color: "#F472B6" }}>
              🧑‍💻 1〜2人開発想定
            </div>
            <div style={{ padding: "6px 14px", background: "#10B98120", borderRadius: 8, fontSize: 12, color: "#34D399" }}>
              🌐 Web（Three.js + Firebase）
            </div>
          </div>
        </div>
      </div>

      {/* Tech Decision Toggle */}
      <button
        onClick={() => setShowTech(!showTech)}
        style={{
          width: "100%",
          padding: "12px 16px",
          marginBottom: 16,
          background: showTech ? "#1a1025" : "#18181B",
          border: `1px solid ${showTech ? "#6366F140" : "#27272A"}`,
          borderRadius: 10,
          color: "#A78BFA",
          fontSize: 13,
          fontWeight: 600,
          cursor: "pointer",
          fontFamily: "'Noto Sans JP', sans-serif",
          textAlign: "left",
          display: "flex",
          alignItems: "center",
          gap: 8,
        }}
      >
        🛠 技術選定ガイド {showTech ? "▲" : "▼"}
      </button>

      {showTech && (
        <div style={{ marginBottom: 20, display: "flex", flexDirection: "column", gap: 10 }}>
          {techDecisions.map((td, idx) => (
            <div key={idx} style={{ background: "#18181B", borderRadius: 10, border: "1px solid #27272A", padding: 16 }}>
              <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA", marginBottom: 10 }}>❓ {td.question}</div>
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {td.options.map((opt, oi) => (
                  <div key={oi} style={{
                    padding: "8px 12px",
                    background: opt.rec ? "#6366F110" : "#0A0A0F",
                    borderRadius: 8,
                    border: opt.rec ? "1px solid #6366F140" : "1px solid #1E1E24",
                    fontSize: 12,
                  }}>
                    <div style={{ fontWeight: 600, color: opt.rec ? "#818CF8" : "#A1A1AA", marginBottom: 4 }}>
                      {opt.rec ? "⭐ " : ""}{opt.name}
                    </div>
                    <div style={{ color: "#10B981", fontSize: 11 }}>✓ {opt.pros}</div>
                    <div style={{ color: "#EF4444", fontSize: 11 }}>✗ {opt.cons}</div>
                  </div>
                ))}
              </div>
              <div style={{ marginTop: 8, padding: "8px 12px", background: "#F59E0B10", borderRadius: 6, fontSize: 11, color: "#FBBF24" }}>
                💡 {td.recommendation}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Milestone Navigation */}
      <div style={{
        display: "flex",
        gap: 4,
        marginBottom: 16,
        overflowX: "auto",
        paddingBottom: 4,
      }}>
        {milestones.map((m) => (
          <button
            key={m.id}
            onClick={() => setActivePhase(m.id)}
            style={{
              padding: "8px 12px",
              borderRadius: 8,
              border: activePhase === m.id ? `1px solid ${m.color}60` : "1px solid transparent",
              background: activePhase === m.id ? m.color + "18" : "#18181B",
              color: activePhase === m.id ? m.color : "#71717A",
              fontSize: 12,
              fontWeight: 600,
              cursor: "pointer",
              whiteSpace: "nowrap",
              fontFamily: "'Noto Sans JP', sans-serif",
              transition: "all 0.15s ease",
              flexShrink: 0,
            }}
          >
            {m.icon} {m.phase.replace("MILESTONE ", "M")}
          </button>
        ))}
      </div>

      {/* Timeline Bar */}
      <div style={{
        display: "flex",
        gap: 2,
        marginBottom: 20,
        height: 6,
        borderRadius: 3,
        overflow: "hidden",
      }}>
        {milestones.map((m) => (
          <div
            key={m.id}
            style={{
              flex: m.id === "m2" || m.id === "m4" ? 2 : 1,
              background: activePhase === m.id ? m.color : m.color + "30",
              transition: "background 0.2s ease",
              cursor: "pointer",
            }}
            onClick={() => setActivePhase(m.id)}
          />
        ))}
      </div>

      {/* Active Milestone Detail */}
      {currentMilestone && (
        <div>
          <div style={{
            display: "flex",
            alignItems: "center",
            gap: 12,
            marginBottom: 16,
            padding: "16px 20px",
            background: currentMilestone.color + "10",
            borderRadius: 12,
            border: `1px solid ${currentMilestone.color}30`,
          }}>
            <div style={{ fontSize: 32 }}>{currentMilestone.icon}</div>
            <div style={{ flex: 1 }}>
              <div style={{ fontFamily: "Orbitron", fontSize: 11, color: currentMilestone.color, fontWeight: 700, letterSpacing: 2 }}>
                {currentMilestone.phase}
              </div>
              <div style={{ fontSize: 18, fontWeight: 900, color: "#FAFAFA", marginTop: 2 }}>
                {currentMilestone.title}
              </div>
              <div style={{ fontSize: 12, color: "#A1A1AA", marginTop: 4 }}>
                {currentMilestone.goal}
              </div>
            </div>
            <div style={{
              padding: "6px 14px",
              background: currentMilestone.color + "20",
              color: currentMilestone.color,
              borderRadius: 8,
              fontSize: 12,
              fontWeight: 700,
              flexShrink: 0,
            }}>
              {currentMilestone.period}
            </div>
          </div>

          <div>
            {currentMilestone.tasks.map((task, idx) => (
              <TaskCard key={idx} task={task} color={currentMilestone.color} />
            ))}
          </div>
        </div>
      )}

      {/* Footer Note */}
      <div style={{
        marginTop: 24,
        padding: 16,
        background: "#18181B",
        borderRadius: 10,
        border: "1px solid #27272A",
        fontSize: 12,
        color: "#71717A",
        lineHeight: 1.8,
      }}>
        <div style={{ fontWeight: 700, color: "#F59E0B", marginBottom: 6 }}>⚡ 開発のコツ</div>
        <div>
          <strong style={{ color: "#E4E4E7" }}>「動くものを最速で作る」</strong>が最優先。
          見た目は後からいくらでも改善できるが、動かないコードは改善しようがない。
          M1で箱人間が走り回るだけでもモチベーションは爆上がりする。
          各Milestoneの終わりには必ず「遊べるもの」を完成させること。
        </div>
        <div style={{ marginTop: 8, color: "#6366F1" }}>
          🔄 このロードマップは目安。進捗に合わせて柔軟に調整すべし。
        </div>
      </div>
    </div>
  );
}
