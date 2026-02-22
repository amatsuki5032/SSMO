import { useState } from "react";

const phases = [
  {
    id: "concept",
    title: "Phase 1: 企画・コンセプト設計",
    subtitle: "Concept & Planning",
    duration: "2〜3ヶ月",
    color: "#E74C3C",
    icon: "🎯",
    tasks: [
      { name: "ゲームコンセプト策定", detail: "真三國無双Online風の一騎当千アクション＋オンライン対戦の方向性確定", days: 14 },
      { name: "ターゲットユーザー分析", detail: "市場調査・競合分析・ペルソナ設定", days: 10 },
      { name: "コアループ設計", detail: "ミッション→育成→装備強化→PvP/Co-opの循環設計", days: 14 },
      { name: "マネタイズ設計", detail: "課金モデル(武器ガチャ・コスチューム・スタミナ等)の策定", days: 7 },
      { name: "技術選定", detail: "エンジン(Unity/UE5)・サーバー・ネットワーク方式の決定", days: 10 },
      { name: "プロジェクト計画書作成", detail: "スケジュール・予算・チーム編成・マイルストーン策定", days: 7 },
    ],
  },
  {
    id: "prototype",
    title: "Phase 2: プロトタイプ開発",
    subtitle: "Prototype Development",
    duration: "3〜4ヶ月",
    color: "#E67E22",
    icon: "⚙️",
    tasks: [
      { name: "基本アクションシステム", detail: "通常攻撃コンボ・チャージ攻撃・無双乱舞の実装", days: 30 },
      { name: "敵AIプロトタイプ", detail: "雑兵AI・武将AI・群衆制御システムの基本実装", days: 21 },
      { name: "カメラ＆操作系", detail: "TPS/ロックオンカメラ・コントローラー/KB+M対応", days: 14 },
      { name: "ネットワーク基盤", detail: "クライアント-サーバー同期・ルームマッチング基盤", days: 21 },
      { name: "基本UI/UXフロー", detail: "ロビー→マッチ→戦闘→リザルトの画面遷移", days: 14 },
      { name: "プロトタイプ評価", detail: "社内テスト・フィードバック収集・方針修正", days: 10 },
    ],
  },
  {
    id: "preproduction",
    title: "Phase 3: プリプロダクション",
    subtitle: "Pre-Production",
    duration: "3〜4ヶ月",
    color: "#F1C40F",
    icon: "📐",
    tasks: [
      { name: "キャラクター設計", detail: "武将モデリング仕様・モーション仕様・カスタマイズ仕様確定", days: 21 },
      { name: "ステージ設計", detail: "合戦マップ仕様(官渡・赤壁・五丈原等)・ギミック設計", days: 21 },
      { name: "バトルシステム詳細設計", detail: "ダメージ計算式・属性相性・スキルツリー・装備システム", days: 21 },
      { name: "サーバーアーキテクチャ設計", detail: "マッチメイキング・レーティング・チャット・DB設計", days: 21 },
      { name: "アセットパイプライン構築", detail: "3Dモデル→リグ→アニメ→エンジンの自動化ワークフロー", days: 14 },
      { name: "テクニカルデザインドキュメント作成", detail: "全システムの技術仕様書完成", days: 14 },
    ],
  },
  {
    id: "production",
    title: "Phase 4: 本制作（アルファ版）",
    subtitle: "Production → Alpha",
    duration: "8〜12ヶ月",
    color: "#2ECC71",
    icon: "🔨",
    tasks: [
      { name: "キャラクター制作", detail: "3Dモデル・テクスチャ・モーション(初期20〜30体)", days: 120 },
      { name: "ステージ制作", detail: "合戦マップ(初期5〜8ステージ)・環境アート・ライティング", days: 90 },
      { name: "戦闘システム完成", detail: "全コンボ・無双乱舞・覚醒・属性・スキル実装", days: 60 },
      { name: "オンライン機能実装", detail: "4v4/8v8対戦・Co-opモード・ギルドシステム", days: 60 },
      { name: "育成＆経済システム", detail: "レベル・装備強化・素材収集・ショップ・ガチャ", days: 45 },
      { name: "UI/UX制作", detail: "全画面デザイン＆実装・HUD・メニュー・チャット", days: 45 },
      { name: "サウンド制作", detail: "BGM・SE・ボイス(CV)収録・実装", days: 60 },
      { name: "AI＆演出", detail: "武将AI強化・群衆戦闘・カットシーン・エフェクト", days: 45 },
    ],
  },
  {
    id: "beta",
    title: "Phase 5: ベータ版・QA",
    subtitle: "Beta & Quality Assurance",
    duration: "3〜4ヶ月",
    color: "#3498DB",
    icon: "🧪",
    tasks: [
      { name: "クローズドβテスト", detail: "限定ユーザーでのテスト・フィードバック収集", days: 21 },
      { name: "バランス調整", detail: "キャラ性能・ダメージ値・マッチメイキング最適化", days: 30 },
      { name: "バグ修正＆最適化", detail: "パフォーマンス最適化・メモリ管理・ロード時間短縮", days: 30 },
      { name: "セキュリティ対策", detail: "チート対策・不正検知・暗号化・脆弱性テスト", days: 21 },
      { name: "オープンβテスト", detail: "大規模負荷テスト・サーバー安定性検証", days: 14 },
      { name: "最終調整", detail: "βフィードバック反映・最終バランス・ポリッシュ", days: 21 },
    ],
  },
  {
    id: "launch",
    title: "Phase 6: ローンチ準備・リリース",
    subtitle: "Launch Preparation & Release",
    duration: "1〜2ヶ月",
    color: "#9B59B6",
    icon: "🚀",
    tasks: [
      { name: "ストア申請＆審査", detail: "Steam/PS/Xbox/App Store申請・レーティング取得", days: 21 },
      { name: "マーケティング", detail: "PV制作・SNS展開・プレスリリース・インフルエンサー施策", days: 30 },
      { name: "インフラ最終準備", detail: "本番サーバー構築・CDN設定・監視体制整備", days: 14 },
      { name: "カスタマーサポート体制", detail: "FAQ作成・問い合わせ対応フロー・GM体制構築", days: 14 },
      { name: "ローンチイベント企画", detail: "リリース記念イベント・ログインボーナス・初回限定ガチャ", days: 10 },
      { name: "正式リリース", detail: "🎉 サービス開始", days: 1 },
    ],
  },
  {
    id: "live",
    title: "Phase 7: 運営・ライブサービス",
    subtitle: "Live Operations (継続)",
    duration: "継続的",
    color: "#1ABC9C",
    icon: "♾️",
    tasks: [
      { name: "定期アップデート", detail: "新武将・新ステージ・新モード追加(月1〜2回)", days: -1 },
      { name: "シーズンイベント", detail: "期間限定イベント・ランキング戦・コラボ企画", days: -1 },
      { name: "バランスパッチ", detail: "キャラ調整・メタ環境改善・不具合修正", days: -1 },
      { name: "コミュニティ運営", detail: "公式SNS・Discord運営・ユーザーフィードバック対応", days: -1 },
      { name: "データ分析＆改善", detail: "KPI監視・課金率/リテンション分析・AB テスト", days: -1 },
      { name: "大型アップデート", detail: "新章追加・システム刷新(3〜6ヶ月ごと)", days: -1 },
    ],
  },
];

const teamRoles = [
  { role: "プロデューサー", count: "1名", icon: "👔" },
  { role: "ディレクター", count: "1名", icon: "🎬" },
  { role: "プランナー/GD", count: "3〜5名", icon: "📋" },
  { role: "プログラマー", count: "8〜15名", icon: "💻" },
  { role: "3Dアーティスト", count: "8〜12名", icon: "🎨" },
  { role: "2D/UIデザイナー", count: "2〜4名", icon: "🖌️" },
  { role: "アニメーター", count: "3〜5名", icon: "🏃" },
  { role: "サウンドデザイナー", count: "1〜2名", icon: "🎵" },
  { role: "サーバーエンジニア", count: "3〜5名", icon: "🖥️" },
  { role: "QAテスター", count: "3〜5名", icon: "🐛" },
  { role: "運営/CS", count: "2〜3名", icon: "📞" },
];

const techStack = [
  { category: "ゲームエンジン", items: "Unreal Engine 5 / Unity" },
  { category: "サーバー", items: "Go / C++ / Rust + gRPC" },
  { category: "DB", items: "PostgreSQL + Redis + MongoDB" },
  { category: "インフラ", items: "AWS / GCP (Kubernetes)" },
  { category: "リアルタイム通信", items: "WebSocket / Mirror / Photon" },
  { category: "CI/CD", items: "Jenkins / GitHub Actions" },
];

function GanttBar({ task, maxDays, color }) {
  const width = task.days > 0 ? Math.max((task.days / maxDays) * 100, 8) : 100;
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 6 }}>
      <div style={{ width: 200, minWidth: 200, fontSize: 13, color: "#D4D4D8", textAlign: "right", fontFamily: "'Noto Sans JP', sans-serif" }}>
        {task.name}
      </div>
      <div style={{ flex: 1, position: "relative" }}>
        <div
          style={{
            width: `${width}%`,
            height: 28,
            background: `linear-gradient(90deg, ${color}CC, ${color}88)`,
            borderRadius: 4,
            display: "flex",
            alignItems: "center",
            paddingLeft: 8,
            fontSize: 11,
            color: "#fff",
            fontFamily: "'Noto Sans JP', sans-serif",
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
            border: `1px solid ${color}`,
            transition: "all 0.3s ease",
          }}
          title={task.detail}
        >
          {task.days > 0 ? `${task.days}日` : "継続"}
          <span style={{ marginLeft: 8, opacity: 0.8, fontSize: 10 }}>{task.detail}</span>
        </div>
      </div>
    </div>
  );
}

export default function GameDevChart() {
  const [expandedPhase, setExpandedPhase] = useState(null);
  const [activeTab, setActiveTab] = useState("chart");

  const totalMonths = "24〜36ヶ月";
  const maxDays = 120;

  return (
    <div style={{
      minHeight: "100vh",
      background: "#0A0A0F",
      color: "#E4E4E7",
      fontFamily: "'Noto Sans JP', 'Inter', sans-serif",
      padding: "24px 20px",
    }}>
      <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@300;400;500;700;900&family=Rajdhani:wght@500;600;700&display=swap" rel="stylesheet" />

      {/* Header */}
      <div style={{
        textAlign: "center",
        marginBottom: 32,
        padding: "32px 20px",
        background: "linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)",
        borderRadius: 16,
        border: "1px solid #ffffff15",
        position: "relative",
        overflow: "hidden",
      }}>
        <div style={{
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: "radial-gradient(ellipse at 30% 50%, #E74C3C15 0%, transparent 50%), radial-gradient(ellipse at 70% 50%, #3498DB15 0%, transparent 50%)",
        }} />
        <div style={{ position: "relative" }}>
          <div style={{ fontSize: 14, letterSpacing: 6, color: "#9B59B6", fontFamily: "Rajdhani", fontWeight: 600, marginBottom: 8 }}>
            GAME DEVELOPMENT ROADMAP
          </div>
          <h1 style={{
            fontSize: 32,
            fontWeight: 900,
            margin: "0 0 8px",
            background: "linear-gradient(90deg, #E74C3C, #E67E22, #F1C40F, #2ECC71, #3498DB, #9B59B6)",
            WebkitBackgroundClip: "text",
            WebkitTextFillColor: "transparent",
            letterSpacing: 2,
          }}>
            真三國無双オンライン風ゲーム
          </h1>
          <div style={{ fontSize: 14, color: "#71717A", marginTop: 4 }}>
            一騎当千アクション × オンラインマルチプレイ
          </div>
          <div style={{
            display: "inline-flex",
            alignItems: "center",
            gap: 8,
            marginTop: 16,
            padding: "8px 20px",
            background: "#ffffff08",
            borderRadius: 8,
            border: "1px solid #ffffff10",
            fontSize: 14,
            color: "#F1C40F",
            fontWeight: 700,
          }}>
            ⏱ 総開発期間（目安）: {totalMonths}
          </div>
        </div>
      </div>

      {/* Tab Navigation */}
      <div style={{ display: "flex", gap: 4, marginBottom: 24, padding: 4, background: "#18181B", borderRadius: 10, width: "fit-content" }}>
        {[
          { id: "chart", label: "📊 開発フェーズ" },
          { id: "team", label: "👥 チーム構成" },
          { id: "tech", label: "🛠 技術スタック" },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              padding: "8px 16px",
              borderRadius: 8,
              border: "none",
              cursor: "pointer",
              fontSize: 13,
              fontFamily: "'Noto Sans JP', sans-serif",
              fontWeight: activeTab === tab.id ? 600 : 400,
              background: activeTab === tab.id ? "#27272A" : "transparent",
              color: activeTab === tab.id ? "#fff" : "#71717A",
              transition: "all 0.2s ease",
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Chart Tab */}
      {activeTab === "chart" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {phases.map((phase, idx) => {
            const isExpanded = expandedPhase === phase.id;
            return (
              <div
                key={phase.id}
                style={{
                  background: "#18181B",
                  borderRadius: 12,
                  border: `1px solid ${isExpanded ? phase.color + "60" : "#27272A"}`,
                  overflow: "hidden",
                  transition: "all 0.3s ease",
                }}
              >
                {/* Phase Header */}
                <div
                  onClick={() => setExpandedPhase(isExpanded ? null : phase.id)}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    padding: "16px 20px",
                    cursor: "pointer",
                    userSelect: "none",
                  }}
                >
                  <div style={{
                    width: 40,
                    height: 40,
                    borderRadius: 10,
                    background: phase.color + "20",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: 20,
                    flexShrink: 0,
                  }}>
                    {phase.icon}
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 15, fontWeight: 700, color: "#FAFAFA" }}>{phase.title}</div>
                    <div style={{ fontSize: 12, color: "#71717A", marginTop: 2 }}>{phase.subtitle}</div>
                  </div>
                  <div style={{
                    padding: "4px 12px",
                    background: phase.color + "20",
                    color: phase.color,
                    borderRadius: 6,
                    fontSize: 12,
                    fontWeight: 600,
                    flexShrink: 0,
                  }}>
                    {phase.duration}
                  </div>
                  <div style={{
                    fontSize: 12,
                    color: "#52525B",
                    transform: isExpanded ? "rotate(180deg)" : "rotate(0deg)",
                    transition: "transform 0.2s ease",
                  }}>
                    ▼
                  </div>
                </div>

                {/* Expanded Gantt */}
                {isExpanded && (
                  <div style={{
                    padding: "0 20px 20px",
                    borderTop: "1px solid #27272A",
                    paddingTop: 16,
                  }}>
                    {phase.tasks.map((task, tIdx) => (
                      <GanttBar key={tIdx} task={task} maxDays={maxDays} color={phase.color} />
                    ))}
                    <div style={{
                      marginTop: 12,
                      padding: 12,
                      background: "#0A0A0F",
                      borderRadius: 8,
                      fontSize: 12,
                      color: "#71717A",
                    }}>
                      💡 タスク数: {phase.tasks.length}件 |
                      {phase.tasks[0].days > 0
                        ? ` 最長タスク: ${Math.max(...phase.tasks.map(t => t.days))}日（一部並行作業）`
                        : " 全タスク継続的に実施"
                      }
                    </div>
                  </div>
                )}
              </div>
            );
          })}

          {/* Timeline Summary */}
          <div style={{
            marginTop: 8,
            padding: 20,
            background: "#18181B",
            borderRadius: 12,
            border: "1px solid #27272A",
          }}>
            <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 12, color: "#FAFAFA" }}>📅 タイムライン概要</div>
            <div style={{ display: "flex", gap: 4, alignItems: "center", flexWrap: "wrap" }}>
              {phases.map((phase, idx) => (
                <div key={idx} style={{ display: "flex", alignItems: "center", gap: 4 }}>
                  <div style={{
                    padding: "6px 12px",
                    background: phase.color + "25",
                    color: phase.color,
                    borderRadius: 6,
                    fontSize: 11,
                    fontWeight: 600,
                    whiteSpace: "nowrap",
                  }}>
                    {phase.icon} {phase.duration}
                  </div>
                  {idx < phases.length - 1 && (
                    <span style={{ color: "#3F3F46", fontSize: 16 }}>→</span>
                  )}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Team Tab */}
      {activeTab === "team" && (
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
          gap: 12,
        }}>
          {teamRoles.map((member, idx) => (
            <div key={idx} style={{
              padding: 16,
              background: "#18181B",
              borderRadius: 12,
              border: "1px solid #27272A",
              display: "flex",
              alignItems: "center",
              gap: 12,
            }}>
              <div style={{ fontSize: 28 }}>{member.icon}</div>
              <div>
                <div style={{ fontSize: 14, fontWeight: 600, color: "#FAFAFA" }}>{member.role}</div>
                <div style={{ fontSize: 13, color: "#3498DB", fontWeight: 500 }}>{member.count}</div>
              </div>
            </div>
          ))}
          <div style={{
            padding: 16,
            background: "linear-gradient(135deg, #1a1a2e, #16213e)",
            borderRadius: 12,
            border: "1px solid #9B59B640",
            gridColumn: "1 / -1",
          }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#9B59B6", marginBottom: 4 }}>
              👥 合計チーム規模（目安）
            </div>
            <div style={{ fontSize: 24, fontWeight: 900, color: "#FAFAFA" }}>
              35〜60名
            </div>
            <div style={{ fontSize: 12, color: "#71717A", marginTop: 4 }}>
              ※ フェーズにより変動。初期は少人数、本制作フェーズで最大規模
            </div>
          </div>
        </div>
      )}

      {/* Tech Stack Tab */}
      {activeTab === "tech" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {techStack.map((tech, idx) => (
            <div key={idx} style={{
              padding: "14px 20px",
              background: "#18181B",
              borderRadius: 10,
              border: "1px solid #27272A",
              display: "flex",
              alignItems: "center",
              gap: 16,
            }}>
              <div style={{
                minWidth: 140,
                fontSize: 13,
                fontWeight: 600,
                color: "#2ECC71",
              }}>
                {tech.category}
              </div>
              <div style={{ fontSize: 14, color: "#D4D4D8", fontFamily: "'Rajdhani', monospace", fontWeight: 500 }}>
                {tech.items}
              </div>
            </div>
          ))}
          <div style={{
            marginTop: 8,
            padding: 16,
            background: "#18181B",
            borderRadius: 12,
            border: "1px solid #F1C40F30",
          }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: "#F1C40F", marginBottom: 8 }}>⚠️ 重要な技術判断ポイント</div>
            <div style={{ fontSize: 12, color: "#A1A1AA", lineHeight: 1.8 }}>
              • <strong style={{ color: "#E4E4E7" }}>同期方式</strong>: 大人数アクション＝サーバー権威型が必須。P2P不可<br />
              • <strong style={{ color: "#E4E4E7" }}>群衆描画</strong>: GPU Instancing + LOD で数百体同時表示<br />
              • <strong style={{ color: "#E4E4E7" }}>ヒット判定</strong>: 広範囲攻撃の高速判定にSpatial Hashing推奨<br />
              • <strong style={{ color: "#E4E4E7" }}>スケーリング</strong>: マイクロサービス＋自動スケールで負荷対応
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
