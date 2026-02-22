import { useState } from "react";

const engineComparison = [
  {
    name: "Unity (C#)",
    score: 92,
    color: "#00D4AA",
    pros: [
      "Netcode for GameObjects（公式ネットワーク）",
      "FishNet / Mirror 等の成熟したネットライブラリ",
      "アセットストア（キャラ・エフェクト大量）",
      "3Dアクションの実績豊富（原神等）",
      "C#は学習しやすい",
    ],
    cons: [
      "ライセンス料（売上100万ドル超で発生）",
      "ランタイムフィー問題（2024年撤回済み）",
    ],
    verdict: "⭐ 推奨：4v4アクション対戦なら最適解",
  },
  {
    name: "Godot 4 (GDScript/C#)",
    score: 72,
    color: "#478CBF",
    pros: [
      "完全無料・オープンソース",
      "組み込みマルチプレイヤーAPI",
      "軽量で起動が速い",
      "3D機能がv4で大幅強化",
    ],
    cons: [
      "アクションゲームの実績少なめ",
      "高度なネットコードは自前実装が多い",
      "アセットストアが貧弱",
    ],
    verdict: "良い選択だがネットワーク周りで苦労する可能性",
  },
  {
    name: "Unreal Engine 5 (C++/BP)",
    score: 85,
    color: "#0D47A1",
    pros: [
      "最高峰のグラフィック",
      "Replication（ネットワーク同期）が強力",
      "大規模対戦の実績豊富",
      "Lyra Starter Game（テンプレート）",
    ],
    cons: [
      "C++の学習コスト高",
      "ビルド時間が長い",
      "少人数開発には重い",
    ],
    verdict: "パワフルだが1〜2人開発にはオーバースペック気味",
  },
];

const netcodeTypes = [
  {
    name: "サーバー権威型 + クライアント予測",
    tag: "推奨",
    color: "#10B981",
    desc: "サーバーが正解を持ち、クライアントは先行して動く。ズレたらサーバーの結果で補正。",
    how: [
      "クライアント：入力即座にローカル実行（予測）",
      "サーバー：入力受信→シミュレーション→結果を全員に配信",
      "クライアント：サーバー結果とズレていたら巻き戻して再計算（リコンシリエーション）",
    ],
    pros: "チート耐性高い・FPS/アクションの標準手法",
    cons: "実装複雑・サーバー運用コストあり",
    games: "Overwatch, Valorant, Fortnite",
  },
  {
    name: "ロールバックネットコード",
    tag: "格ゲー向け",
    color: "#F59E0B",
    desc: "各フレームの状態を保存し、ラグ発生時は過去に巻き戻して再シミュレーション。",
    how: [
      "各フレームのゲーム状態をスナップショット保存",
      "相手の入力が届いたら、該当フレームまでロールバック",
      "そこから現在フレームまで高速再シミュレーション",
    ],
    pros: "入力遅延ゼロ・P2Pでも可能",
    cons: "状態保存のメモリコスト・参加人数に限界",
    games: "ストリートファイター6, GGST",
  },
  {
    name: "ラグコンペンセーション（巻き戻し判定）",
    tag: "射撃/ヒット判定用",
    color: "#8B5CF6",
    desc: "攻撃判定時にサーバーが「その瞬間のワールド状態」に巻き戻して当たり判定。",
    how: [
      "サーバーが過去N秒のワールド状態を保持",
      "プレイヤーの攻撃入力 + タイムスタンプを受信",
      "そのタイムスタンプ時点の位置でヒット判定",
      "結果を全員に配信",
    ],
    pros: "攻撃した側の体感が良い（見えた通りに当たる）",
    cons: "やられた側は「もう避けたのに当たった」が起きる",
    games: "Counter-Strike 2, Overwatch",
  },
];

const milestones = [
  {
    id: "m0",
    phase: "M0",
    title: "リポジトリ & 環境構築",
    period: "Week 1-2",
    color: "#6366F1",
    icon: "🏗️",
    goal: "開発基盤を完璧に整える",
    tasks: [
      {
        name: "リポジトリ構成",
        items: [
          "GitHub リポジトリ作成（Private推奨）",
          "Unity プロジェクト作成（Unity 2022 LTS or 6）",
          "Git LFS 設定（3Dモデル・テクスチャ用 必須）",
          ".gitignore（Unity用テンプレート）",
          "ブランチ戦略: main / develop / feature/* / netcode/*",
          "README.md + CONTRIBUTING.md",
        ],
      },
      {
        name: "プロジェクト構造",
        items: [
          "Assets/Scripts/Client/ — クライアント固有処理",
          "Assets/Scripts/Server/ — Dedicated Server 処理",
          "Assets/Scripts/Shared/ — 共有ロジック（ダメージ計算等）",
          "Assets/Scripts/Netcode/ — ネットワーク同期レイヤー",
          "Assets/Scripts/Combat/ — 戦闘システム",
          "Assets/Scripts/AI/ — 雑兵・武将AI",
          "Assets/Prefabs/ Assets/Models/ Assets/Effects/",
        ],
      },
      {
        name: "ネットワーク基盤選定 & 導入",
        items: [
          "⭐ Unity Netcode for GameObjects（NGO）導入",
          "  or FishNet（より柔軟・無料）",
          "  or Mirror（実績豊富・オープンソース）",
          "Transport 選定: Unity Transport / LiteNetLib",
          "テスト用ローカルサーバー起動確認",
          "ParrelSync 導入（エディタ複数起動でマルチテスト）",
        ],
      },
      {
        name: "Dedicated Server 構成",
        items: [
          "Unity Dedicated Server Build Target 設定",
          "ヘッドレスサーバービルド（描画なし）の動作確認",
          "サーバー/クライアント コード分離（#if SERVER / #if CLIENT）",
          "Docker コンテナ化（デプロイ用）",
        ],
      },
    ],
  },
  {
    id: "m1",
    phase: "M1",
    title: "ネットワーク同期の土台",
    period: "Week 3-8",
    color: "#EF4444",
    icon: "🌐",
    goal: "「2人が同じ空間で動ける」を最優先で実現。見た目は後。",
    critical: true,
    tasks: [
      {
        name: "★ クライアント予測 & サーバー権威",
        items: [
          "入力をサーバーへ送信（InputをシリアライズしてRPC）",
          "クライアント側で入力を即座にローカル実行（予測移動）",
          "サーバーで入力を処理→正しい位置を算出",
          "サーバーから全クライアントに状態を配信（State Sync）",
          "クライアントで予測とサーバー結果を比較",
          "ズレていたら巻き戻し→再計算（Reconciliation）",
        ],
      },
      {
        name: "★ 補間 & 外挿（他プレイヤー表示）",
        items: [
          "他プレイヤーの位置を補間表示（Interpolation）",
          "バッファリング: 直近N個の状態を保持",
          "補間遅延: 100ms 遅れで滑らかに表示",
          "パケットロス時の外挿（Extrapolation）",
          "スナップ閾値: 大きくズレたら瞬間移動",
        ],
      },
      {
        name: "★ ラグコンペンセーション（攻撃判定）",
        items: [
          "サーバーが過去2秒分のワールドスナップショット保持",
          "攻撃入力 + クライアント側タイムスタンプをサーバーへ送信",
          "サーバーが該当時刻のワールド状態に巻き戻し",
          "巻き戻した状態でヒット判定実行",
          "結果を全員に配信（ヒットした / しなかった）",
          "最大補正時間の上限設定（例: 200ms）",
        ],
      },
      {
        name: "ネットワーク最適化",
        items: [
          "ティックレート: サーバー60Hz / クライアント送信30Hz",
          "デルタ圧縮（変化した値のみ送信）",
          "入力バッファリング（ジッター対策）",
          "RTT（往復遅延）測定 & 表示",
          "パケットロスシミュレーター（テスト用）",
          "ネットワーク統計HUD（Ping / PacketLoss / Jitter）",
        ],
      },
      {
        name: "テスト環境",
        items: [
          "ParrelSync でローカル2〜4人テスト",
          "Unity Network Simulator（遅延・ロス注入）",
          "50ms / 100ms / 200ms 遅延での動作確認",
          "5% / 10% パケットロスでの動作確認",
          "ログ出力: 予測ミス率・補正量の記録",
        ],
      },
    ],
  },
  {
    id: "m2",
    phase: "M2",
    title: "戦闘アクション実装",
    period: "Week 9-18",
    color: "#EC4899",
    icon: "⚔️",
    goal: "無双系の一騎当千コンボアクション。ネットワーク同期込み。",
    tasks: [
      {
        name: "キャラクターコントローラー",
        items: [
          "CharacterController ベースの移動",
          "走り / ダッシュ / ジャンプ",
          "TPS カメラ（Cinemachine FreeLook）",
          "ロックオンシステム（対人戦で重要）",
          "移動入力のネットワーク同期（M1の仕組み上に構築）",
        ],
      },
      {
        name: "コンボシステム",
        items: [
          "通常攻撃 N1→N2→N3→N4→N5→N6",
          "チャージ攻撃 C1〜C6（□→△ 派生）",
          "入力バッファ（先行入力受付）",
          "コンボ受付ウィンドウ（タイミング猶予）",
          "Animator + StateMachineBehaviour で制御",
          "攻撃モーション中の移動制限",
        ],
      },
      {
        name: "ヒット判定 & ダメージ",
        items: [
          "攻撃ヒットボックス（Trigger Collider）",
          "★ ヒット判定はサーバー権威（クライアントは演出のみ）",
          "★ ラグコンペンセーション適用（M1で実装済み）",
          "ダメージ計算式: ATK × モーション倍率 × 属性 - DEF",
          "ヒットストップ（フレーム停止演出）",
          "ノックバック / 打ち上げ / 吹き飛ばし",
          "ダメージ数字のフローティング表示",
        ],
      },
      {
        name: "無双乱舞 & 覚醒",
        items: [
          "無双ゲージ蓄積（攻撃・被弾で上昇）",
          "無双乱舞発動（無敵 + 連続攻撃）",
          "覚醒状態（一定時間ステータスUP）",
          "サーバー側でゲージ管理（不正防止）",
        ],
      },
      {
        name: "キャラクターステートマシン",
        items: [
          "Idle / Move / Attack / Charge / Musou / Guard",
          "Hitstun / Knockback / Launch / Down / Dead",
          "★ ステート遷移をサーバーが管理",
          "クライアント予測: 攻撃ステートも予測実行",
          "スーパーアーマー管理（特定技でのけぞり無効）",
        ],
      },
    ],
  },
  {
    id: "m3",
    phase: "M3",
    title: "4v4 対戦モード実装",
    period: "Week 19-28",
    color: "#F59E0B",
    icon: "🏟️",
    goal: "4v4で戦える対戦フローの完成。これがゲームの核。",
    tasks: [
      {
        name: "マッチメイキング",
        items: [
          "ロビーシステム（ルーム作成 / 参加 / 準備完了）",
          "Firebase Firestore でルーム管理",
          "  rooms/{id}: ルーム情報・参加者リスト",
          "  matchmaking_queue: マッチング待ち行列",
          "クイックマッチ（自動マッチング）",
          "チーム選択（赤軍 / 青軍）",
          "全員準備完了→カウントダウン→ゲーム開始",
        ],
      },
      {
        name: "合戦マップ",
        items: [
          "マップ1: 官渡の戦い（中規模・拠点5つ）",
          "拠点制圧ルール（エリア内に人数が多い陣営が制圧）",
          "雑兵NPC配置（各チーム20〜30体）",
          "武将NPC（各チーム大将1体 + 副将2体）",
          "大将撃破 or 制限時間での拠点数勝利",
          "ミニマップ（味方位置・拠点状態表示）",
        ],
      },
      {
        name: "雑兵 & 武将AI（サーバー実行）",
        items: [
          "★ AI は Dedicated Server 上で実行",
          "雑兵AI: 最寄りの敵に接近→攻撃（シンプル）",
          "武将AI: ガード / 回避 / コンボ使用 / スキル使用",
          "AI状態を全クライアントに同期（位置・ステート・HP）",
          "Spatial Hashing で大量AIの処理効率化",
        ],
      },
      {
        name: "対戦 HUD & UI",
        items: [
          "自キャラ HP / 無双ゲージ / 覚醒ゲージ",
          "敵プレイヤーの頭上HP表示",
          "撃破ログ（画面右上: ○○が××を撃破）",
          "チームスコア表示",
          "制限時間カウントダウン",
          "Ping表示（常時）",
        ],
      },
      {
        name: "リザルト & レーティング",
        items: [
          "戦績: 撃破数 / 被撃破数 / アシスト / 拠点制圧数",
          "MVP選出（最多撃破 or 最多貢献）",
          "ELOレーティング計算（勝敗で上下）",
          "Firestore にレーティング保存",
          "ランキングボード（シーズン制）",
        ],
      },
    ],
  },
  {
    id: "m4",
    phase: "M4",
    title: "キャラクター & コンテンツ",
    period: "Week 29-38",
    color: "#8B5CF6",
    icon: "👤",
    goal: "複数キャラ・武器種の差別化でリプレイ性を確保。",
    tasks: [
      {
        name: "武器種システム（初期4〜6種）",
        items: [
          "大剣（リーチ長・隙大・高威力）",
          "双剣（手数多・隙小・低威力）",
          "槍（中距離・突き特化）",
          "弓（遠距離・低耐久）",
          "各武器種でN1〜N6 + C1〜C6の固有モーション",
          "武器種ごとの無双乱舞",
        ],
      },
      {
        name: "キャラクター作成",
        items: [
          "初期キャラ8〜12体（各チーム4〜6体選択可能）",
          "キャラごとの固有スキル（パッシブ + アクティブ）",
          "3Dモデル: Mixamo + 改造 or AssetStore活用",
          "モーション: Mixamo or 自作（攻撃は自作推奨）",
          "ボイス（将来対応）/ SE差別化",
        ],
      },
      {
        name: "育成 & カスタマイズ",
        items: [
          "キャラレベル & ステータス成長",
          "装備: 武器（攻撃力 + オプション効果）/ 防具 / アクセ",
          "スキルセット選択（4つのスキルから2つ選択等）",
          "見た目カスタマイズ（カラーバリエーション）",
          "Firestore にキャラデータ永続化",
        ],
      },
    ],
  },
  {
    id: "m5",
    phase: "M5",
    title: "インフラ & チート対策",
    period: "Week 39-44",
    color: "#10B981",
    icon: "🛡️",
    goal: "対人戦で絶対に必要なセキュリティとサーバー安定性。",
    tasks: [
      {
        name: "Dedicated Server デプロイ",
        items: [
          "Docker イメージ作成（Unity Headless Server）",
          "AWS / GCP でコンテナ実行（ECS or GKE）",
          "リージョン: 東京（ap-northeast-1）最優先",
          "オートスケール設定（同時マッチ数に応じて）",
          "サーバー監視（Prometheus + Grafana or CloudWatch）",
          "ゲームサーバーオーケストレーション（Agones等）",
        ],
      },
      {
        name: "★ チート対策（サーバー権威が基本）",
        items: [
          "移動速度の上限チェック（サーバー側）",
          "攻撃間隔の妥当性検証（サーバー側）",
          "ダメージ計算は100%サーバー（クライアント値を信用しない）",
          "異常値検知（瞬間移動・異常攻撃速度のログ）",
          "クライアントのメモリ改ざん検知（基本レベル）",
          "通報システム + リプレイ保存",
        ],
      },
      {
        name: "通信セキュリティ",
        items: [
          "パケット暗号化（TLS / DTLS）",
          "リプレイ攻撃防止（シーケンス番号 + タイムスタンプ）",
          "レートリミット（異常な送信頻度をブロック）",
          "Firebase Auth トークン検証（なりすまし防止）",
        ],
      },
    ],
  },
  {
    id: "m6",
    phase: "M6",
    title: "ポリッシュ & α版リリース",
    period: "Week 45-52",
    color: "#06B6D4",
    icon: "🚀",
    goal: "遊べる品質に仕上げて公開。フィードバックを得る。",
    tasks: [
      {
        name: "演出 & エフェクト",
        items: [
          "ヒットエフェクト（VFX Graph / Particle System）",
          "無双乱舞演出（カメラワーク + エフェクト）",
          "拠点制圧エフェクト",
          "撃破演出（スローモーション + カメラズーム）",
          "BGM & SE実装（Wwise or FMOD or Unity Audio）",
        ],
      },
      {
        name: "UI/UX 完成",
        items: [
          "タイトル画面 / ログイン / ロビー / マッチング中",
          "キャラ選択画面 / ロード画面 / HUD / リザルト",
          "設定画面（グラフィック / サウンド / キーバインド）",
          "チュートリアル（1人用の練習ミッション）",
        ],
      },
      {
        name: "テスト & 最適化",
        items: [
          "8人同時対戦の負荷テスト",
          "各遅延環境でのプレイテスト（50/100/200ms）",
          "FPS最適化（目標: 60fps @ 中スペPC）",
          "メモリリーク検出",
          "クラッシュレポート収集（Sentry等）",
        ],
      },
      {
        name: "α版公開",
        items: [
          "Steam or itch.io でビルド配布",
          "フィードバック用 Discord サーバー開設",
          "既知の問題リスト公開",
          "プレイ動画撮影 → Twitter / YouTube で告知",
        ],
      },
    ],
  },
];

function NetcodeCard({ item }) {
  const [open, setOpen] = useState(false);
  return (
    <div style={{
      background: "#16161C",
      borderRadius: 10,
      border: `1px solid ${item.color}30`,
      overflow: "hidden",
      marginBottom: 8,
    }}>
      <div onClick={() => setOpen(!open)} style={{
        padding: "14px 16px",
        cursor: "pointer",
        display: "flex",
        alignItems: "center",
        gap: 10,
      }}>
        <span style={{
          fontSize: 10, padding: "2px 8px", borderRadius: 4,
          background: item.color + "20", color: item.color, fontWeight: 700,
        }}>{item.tag}</span>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA" }}>{item.name}</div>
          <div style={{ fontSize: 11, color: "#71717A", marginTop: 2 }}>{item.desc}</div>
        </div>
        <span style={{ color: "#52525B", fontSize: 11, transform: open ? "rotate(180deg)" : "", transition: "0.2s" }}>▼</span>
      </div>
      {open && (
        <div style={{ padding: "0 16px 14px", borderTop: `1px solid #27272A`, paddingTop: 12 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: "#A1A1AA", marginBottom: 6 }}>仕組み:</div>
          {item.how.map((h, i) => (
            <div key={i} style={{ fontSize: 12, color: "#D4D4D8", marginBottom: 4, paddingLeft: 8, borderLeft: `2px solid ${item.color}40` }}>
              {i + 1}. {h}
            </div>
          ))}
          <div style={{ display: "flex", gap: 12, marginTop: 10, fontSize: 11 }}>
            <div><span style={{ color: "#10B981" }}>✓</span> <span style={{ color: "#A1A1AA" }}>{item.pros}</span></div>
            <div><span style={{ color: "#EF4444" }}>✗</span> <span style={{ color: "#A1A1AA" }}>{item.cons}</span></div>
          </div>
          <div style={{ marginTop: 6, fontSize: 11, color: "#71717A" }}>採用ゲーム: {item.games}</div>
        </div>
      )}
    </div>
  );
}

export default function DevPlan() {
  const [activeTab, setActiveTab] = useState("plan");
  const [activePhase, setActivePhase] = useState("m0");
  const [expandedTasks, setExpandedTasks] = useState({});

  const current = milestones.find(m => m.id === activePhase);

  const toggleTask = (phaseId, taskIdx) => {
    const key = `${phaseId}-${taskIdx}`;
    setExpandedTasks(prev => ({ ...prev, [key]: !prev[key] }));
  };

  return (
    <div style={{
      minHeight: "100vh",
      background: "#0C0C12",
      color: "#E4E4E7",
      fontFamily: "'Noto Sans JP', sans-serif",
      padding: "20px 16px",
    }}>
      <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@300;400;500;700;900&family=Orbitron:wght@500;700;900&display=swap" rel="stylesheet" />

      {/* Header */}
      <div style={{
        textAlign: "center", marginBottom: 24, padding: "28px 20px",
        background: "linear-gradient(160deg, #1a0a1e 0%, #0a1628 50%, #0f1a12 100%)",
        borderRadius: 14, border: "1px solid #ffffff10", position: "relative", overflow: "hidden",
      }}>
        <div style={{ position: "absolute", inset: 0, background: "radial-gradient(ellipse at 30% 50%, #EF444415 0%, transparent 50%), radial-gradient(ellipse at 70% 30%, #10B98115 0%, transparent 50%)" }} />
        <div style={{ position: "relative" }}>
          <div style={{ fontFamily: "Orbitron", fontSize: 11, letterSpacing: 4, color: "#EF4444", fontWeight: 700, marginBottom: 6 }}>
            4v4 PVP ACTION GAME
          </div>
          <h1 style={{ fontSize: 26, fontWeight: 900, margin: "0 0 4px", color: "#FAFAFA" }}>
            真三國無双Online風 3Dアクション対戦
          </h1>
          <div style={{ fontSize: 12, color: "#71717A", marginTop: 4 }}>
            ラグ対策最優先 × サーバー権威型 × Dedicated Server
          </div>
          <div style={{ display: "flex", gap: 8, justifyContent: "center", marginTop: 14, flexWrap: "wrap" }}>
            {[
              { text: "🎮 Unity + C#", bg: "#00D4AA" },
              { text: "🌐 Dedicated Server", bg: "#EF4444" },
              { text: "⚡ クライアント予測", bg: "#F59E0B" },
              { text: "🛡️ ラグコンペンセーション", bg: "#8B5CF6" },
            ].map((b, i) => (
              <div key={i} style={{ padding: "5px 12px", background: b.bg + "18", borderRadius: 6, fontSize: 11, color: b.bg, fontWeight: 600, border: `1px solid ${b.bg}25` }}>
                {b.text}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div style={{ display: "flex", gap: 3, marginBottom: 20, padding: 3, background: "#16161C", borderRadius: 10, width: "fit-content" }}>
        {[
          { id: "plan", label: "📋 開発計画" },
          { id: "engine", label: "🎮 エンジン比較" },
          { id: "netcode", label: "🌐 ネットコード解説" },
        ].map(tab => (
          <button key={tab.id} onClick={() => setActiveTab(tab.id)} style={{
            padding: "7px 14px", borderRadius: 8, border: "none", cursor: "pointer", fontSize: 12,
            fontFamily: "'Noto Sans JP', sans-serif", fontWeight: activeTab === tab.id ? 600 : 400,
            background: activeTab === tab.id ? "#27272A" : "transparent",
            color: activeTab === tab.id ? "#fff" : "#71717A", transition: "0.2s",
          }}>{tab.label}</button>
        ))}
      </div>

      {/* Engine Comparison Tab */}
      {activeTab === "engine" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {engineComparison.map((eng, idx) => (
            <div key={idx} style={{
              background: "#16161C", borderRadius: 12, border: `1px solid ${eng.color}30`, padding: 18,
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 12 }}>
                <div style={{ fontSize: 18, fontWeight: 900, color: eng.color, fontFamily: "Orbitron" }}>{eng.name}</div>
                <div style={{ marginLeft: "auto", padding: "4px 12px", background: eng.color + "20", borderRadius: 6, fontSize: 13, fontWeight: 700, color: eng.color }}>
                  {eng.score}/100
                </div>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, fontSize: 12, marginBottom: 10 }}>
                <div>
                  {eng.pros.map((p, i) => <div key={i} style={{ color: "#10B981", marginBottom: 3 }}>✓ {p}</div>)}
                </div>
                <div>
                  {eng.cons.map((c, i) => <div key={i} style={{ color: "#EF4444", marginBottom: 3 }}>✗ {c}</div>)}
                </div>
              </div>
              <div style={{ padding: "8px 12px", background: "#0C0C12", borderRadius: 6, fontSize: 12, color: "#F59E0B" }}>
                💡 {eng.verdict}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Netcode Tab */}
      {activeTab === "netcode" && (
        <div>
          <div style={{
            padding: 16, marginBottom: 16, background: "#16161C", borderRadius: 12,
            border: "1px solid #EF444430", fontSize: 13, lineHeight: 1.8,
          }}>
            <div style={{ fontWeight: 700, color: "#EF4444", marginBottom: 6 }}>
              🎯 4v4アクション対戦で必要なネットコード構成
            </div>
            <div style={{ color: "#D4D4D8" }}>
              <strong style={{ color: "#FAFAFA" }}>サーバー権威型</strong>（チート対策の土台）＋
              <strong style={{ color: "#FAFAFA" }}>クライアント予測</strong>（操作の即応性）＋
              <strong style={{ color: "#FAFAFA" }}>ラグコンペンセーション</strong>（ヒット判定の公平性）<br />
              この3つを組み合わせるのが現代のアクション対戦ゲームの標準。
            </div>
          </div>
          {netcodeTypes.map((nt, idx) => <NetcodeCard key={idx} item={nt} />)}
          <div style={{
            marginTop: 12, padding: 14, background: "#16161C", borderRadius: 10,
            border: "1px solid #27272A", fontSize: 12, color: "#71717A", lineHeight: 1.8,
          }}>
            <div style={{ fontWeight: 700, color: "#06B6D4", marginBottom: 4 }}>📚 学習リソース</div>
            <div>• Gabriel Gambetta "Fast-Paced Multiplayer"（必読）</div>
            <div>• GDC "Overwatch Gameplay Architecture and Netcode"</div>
            <div>• GDC "It IS Rocket Science! The Physics of Rocket League"</div>
            <div>• Unity NGO Documentation（公式）</div>
            <div>• Valve Developer Wiki "Source Multiplayer Networking"</div>
          </div>
        </div>
      )}

      {/* Plan Tab */}
      {activeTab === "plan" && (
        <>
          {/* Phase Selector */}
          <div style={{ display: "flex", gap: 3, marginBottom: 12, overflowX: "auto", paddingBottom: 4 }}>
            {milestones.map(m => (
              <button key={m.id} onClick={() => setActivePhase(m.id)} style={{
                padding: "7px 12px", borderRadius: 8, fontSize: 11, fontWeight: 600, cursor: "pointer",
                border: activePhase === m.id ? `1px solid ${m.color}60` : "1px solid transparent",
                background: activePhase === m.id ? m.color + "18" : "#16161C",
                color: activePhase === m.id ? m.color : "#71717A",
                fontFamily: "'Noto Sans JP', sans-serif", whiteSpace: "nowrap", flexShrink: 0,
                transition: "0.15s",
              }}>{m.icon} {m.phase}</button>
            ))}
          </div>

          {/* Timeline */}
          <div style={{ display: "flex", gap: 2, marginBottom: 16, height: 5, borderRadius: 3, overflow: "hidden" }}>
            {milestones.map(m => (
              <div key={m.id} onClick={() => setActivePhase(m.id)} style={{
                flex: m.id === "m1" || m.id === "m2" || m.id === "m3" ? 2 : 1,
                background: activePhase === m.id ? m.color : m.color + "30",
                cursor: "pointer", transition: "0.2s",
              }} />
            ))}
          </div>

          {/* Phase Detail */}
          {current && (
            <div>
              <div style={{
                display: "flex", alignItems: "center", gap: 12, marginBottom: 14, padding: "14px 18px",
                background: current.color + "10", borderRadius: 12, border: `1px solid ${current.color}30`,
              }}>
                <div style={{ fontSize: 30 }}>{current.icon}</div>
                <div style={{ flex: 1 }}>
                  <div style={{ fontFamily: "Orbitron", fontSize: 10, color: current.color, fontWeight: 700, letterSpacing: 2 }}>
                    {current.phase} {current.critical ? "— 最重要フェーズ 🔥" : ""}
                  </div>
                  <div style={{ fontSize: 17, fontWeight: 900, color: "#FAFAFA", marginTop: 2 }}>{current.title}</div>
                  <div style={{ fontSize: 11, color: "#A1A1AA", marginTop: 3 }}>{current.goal}</div>
                </div>
                <div style={{
                  padding: "5px 12px", background: current.color + "20", color: current.color,
                  borderRadius: 6, fontSize: 11, fontWeight: 700, flexShrink: 0,
                }}>{current.period}</div>
              </div>

              {current.tasks.map((task, tIdx) => {
                const key = `${current.id}-${tIdx}`;
                const isOpen = expandedTasks[key] !== false;
                return (
                  <div key={tIdx} style={{
                    background: "#16161C", borderRadius: 10, border: "1px solid #27272A",
                    marginBottom: 8, overflow: "hidden",
                  }}>
                    <div onClick={() => toggleTask(current.id, tIdx)} style={{
                      padding: "12px 16px", cursor: "pointer", display: "flex", alignItems: "center", gap: 10,
                    }}>
                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: 14, fontWeight: 600, color: "#F0F0F5" }}>{task.name}</div>
                      </div>
                      <span style={{ fontSize: 10, color: "#52525B" }}>{task.items.length}項目</span>
                      <span style={{ color: "#52525B", fontSize: 11, transform: isOpen ? "rotate(180deg)" : "", transition: "0.2s" }}>▼</span>
                    </div>
                    {isOpen && (
                      <div style={{ padding: "0 16px 14px", borderTop: "1px solid #27272A", paddingTop: 10 }}>
                        {task.items.map((item, i) => {
                          const isCritical = item.startsWith("★");
                          return (
                            <div key={i} style={{
                              display: "flex", alignItems: "flex-start", gap: 8, marginBottom: 5,
                              fontSize: 12, color: isCritical ? "#F59E0B" : "#A1A1AA",
                              fontWeight: isCritical ? 600 : 400,
                              paddingLeft: item.startsWith("  ") ? 16 : 0,
                            }}>
                              <span style={{ color: isCritical ? "#EF4444" : current.color, flexShrink: 0, marginTop: 1 }}>
                                {isCritical ? "★" : "○"}
                              </span>
                              <span>{item.replace(/^★\s*/, "")}</span>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}

          {/* Key Principle */}
          <div style={{
            marginTop: 20, padding: 16, background: "#16161C", borderRadius: 10,
            border: "1px solid #EF444430", fontSize: 12, lineHeight: 1.8,
          }}>
            <div style={{ fontWeight: 700, color: "#EF4444", marginBottom: 6, fontSize: 13 }}>
              🔥 最重要原則: ネットワークは後付けできない
            </div>
            <div style={{ color: "#D4D4D8" }}>
              一般的なゲーム開発は「まずオフラインで完成させてからネットワーク対応」だが、
              <strong style={{ color: "#FAFAFA" }}>対人アクション対戦ゲームではこれは致命的な間違い</strong>。
              M1で最初にネットワーク同期を作り、その上にすべての機能を積み上げる。
              オフラインで完璧に動くコードをオンライン化するのは、最初からオンライン前提で作るより遥かに困難。
            </div>
          </div>
        </>
      )}
    </div>
  );
}
