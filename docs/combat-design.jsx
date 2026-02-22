import { useState } from "react";

const sections = [
  {
    id: "overview",
    label: "全体設計",
    icon: "🎯",
    color: "#6366F1",
  },
  {
    id: "melee",
    label: "近接戦闘",
    icon: "⚔️",
    color: "#EF4444",
  },
  {
    id: "ranged",
    label: "遠隔攻撃",
    icon: "🏹",
    color: "#F59E0B",
  },
  {
    id: "netcode",
    label: "ネットコード",
    icon: "🌐",
    color: "#10B981",
  },
  {
    id: "state",
    label: "ステート管理",
    icon: "🔄",
    color: "#8B5CF6",
  },
  {
    id: "timeline",
    label: "実装順序",
    icon: "📅",
    color: "#EC4899",
  },
];

/* ─── OVERVIEW ─── */
const overviewData = {
  concept: {
    title: "戦闘コンセプト",
    items: [
      { label: "ジャンル", value: "近接アクション対戦（無双系）", accent: true },
      { label: "交戦距離", value: "近接 0〜5m（メイン）/ 遠隔 5〜100m（サブ）" },
      { label: "対戦形式", value: "4v4 チーム戦（+ NPC雑兵/武将）" },
      { label: "ティックレート", value: "サーバー 60Hz / クライアント送信 30Hz" },
      { label: "許容遅延", value: "近接ヒット判定 ≤ 150ms / 遠隔 ≤ 200ms" },
      { label: "判定方式", value: "サーバー権威 + ラグコンペンセーション" },
    ],
  },
  ranges: [
    { range: "0〜2m", name: "密着", desc: "通常コンボ・投げ・ガード崩し", color: "#EF4444", width: "15%" },
    { range: "2〜5m", name: "近接", desc: "リーチ長武器・チャージ攻撃・突進技", color: "#F59E0B", width: "25%" },
    { range: "5〜15m", name: "中距離", desc: "ダッシュ攻撃・一部スキル・気弾系", color: "#8B5CF6", width: "25%" },
    { range: "15〜100m", name: "遠距離", desc: "弓矢・投擲・大型スキル・援護射撃", color: "#3B82F6", width: "35%" },
  ],
  weapons: [
    { name: "大剣", range: "3m", speed: "遅", power: "★★★★★", style: "広範囲薙ぎ払い・一撃重視", color: "#EF4444" },
    { name: "双剣", range: "1.5m", speed: "速", power: "★★☆☆☆", style: "手数型・連撃コンボ", color: "#EC4899" },
    { name: "槍", range: "4.5m", speed: "中", power: "★★★☆☆", style: "突き特化・リーチ戦", color: "#10B981" },
    { name: "戟", range: "3.5m", speed: "中", power: "★★★★☆", style: "打ち上げ・回転斬り", color: "#F59E0B" },
    { name: "拳", range: "1m", speed: "最速", power: "★★☆☆☆", style: "超近距離ラッシュ・投げ", color: "#8B5CF6" },
    { name: "弓", range: "100m", speed: "遅", power: "★★★☆☆", style: "遠距離射撃・牽制", color: "#3B82F6" },
  ],
};

/* ─── MELEE SYSTEM ─── */
const meleeSystem = [
  {
    title: "コンボチェーン構造",
    color: "#EF4444",
    critical: true,
    content: [
      {
        subtitle: "通常攻撃（□ / 左クリック）",
        items: [
          "N1 → N2 → N3 → N4 → N5 → N6（最大6段）",
          "各段に固有のモーション・判定範囲・ダメージ倍率",
          "入力受付ウィンドウ: 各段の後半 30% のフレーム",
          "先行入力バッファ: 最大 150ms 先の入力を保持",
          "★ サーバーもコンボ段数を追跡（不正コンボ防止）",
        ],
      },
      {
        subtitle: "チャージ攻撃（△ / 右クリック）",
        items: [
          "N1→△: C1（ガード崩し系・単体高威力）",
          "N2→△: C2（打ち上げ→空中コンボ起点）",
          "N3→△: C3（周囲360°薙ぎ払い）",
          "N4→△: C4（突進→吹き飛ばし）",
          "N5→△: C5（多段ヒット範囲攻撃）",
          "N6→△: C6（武器種固有の最大技）",
          "★ チャージ派生タイミングもサーバー検証",
        ],
      },
    ],
  },
  {
    title: "ヒットボックス設計",
    color: "#F59E0B",
    critical: true,
    content: [
      {
        subtitle: "判定の仕組み",
        items: [
          "攻撃側: 武器モーションに合わせたカプセル型 Hitbox",
          "被弾側: キャラクターのカプセル型 Hurtbox",
          "判定タイミング: アニメーションの「アクティブフレーム」のみ",
          "1つの攻撃で同じ対象には1回のみヒット（多段技は別設定）",
          "★ 最終判定はサーバー上で実行",
        ],
      },
      {
        subtitle: "近接攻撃のヒット判定フロー",
        items: [
          "1. クライアント: 攻撃入力 → ローカル予測実行",
          "2. クライアント: 予測ヒット → 即座にヒットエフェクト再生（仮）",
          "3. サーバー: 入力受信 → ラグコンペンセーション発動",
          "4. サーバー: 攻撃者のタイムスタンプ時点の敵位置を復元",
          "5. サーバー: 復元位置で Hitbox vs Hurtbox 判定",
          "6. サーバー: ヒット確定 → 全クライアントに通知",
          "7. クライアント: 予測が正しければそのまま / 外れていたらエフェクト取消",
        ],
      },
      {
        subtitle: "ヒットボックスのサイズ目安",
        items: [
          "大剣横薙ぎ: 幅 3m × 奥行 1.5m の扇形",
          "槍突き: 幅 0.5m × 奥行 4.5m の直線",
          "拳: 幅 1m × 奥行 1m の球",
          "C3(周囲攻撃): 半径 3m の円",
          "★ ヒットボックスデータはサーバーにもマスタ保持",
        ],
      },
    ],
  },
  {
    title: "被弾リアクション",
    color: "#EC4899",
    critical: true,
    content: [
      {
        subtitle: "リアクション種別",
        items: [
          "のけぞり（Hitstun）: 軽攻撃被弾。短時間行動不能",
          "よろめき（Stagger）: 重攻撃被弾。長めの行動不能",
          "打ち上げ（Launch）: C2系。空中に浮く→追撃可能",
          "叩きつけ（Slam）: 空中から地面へ。ダウン状態に",
          "吹き飛ばし（Knockback）: C4系。大きく後退",
          "ダウン（Down）: 地面に倒れる。起き上がり無敵あり",
          "気絶（Stun）: C1/投げ系。一定時間完全無防備",
        ],
      },
      {
        subtitle: "★ ネットワーク同期のポイント",
        items: [
          "リアクション開始はサーバーが命令（権威）",
          "リアクション中の位置変化もサーバーが計算",
          "打ち上げの軌道: 放物線パラメータをサーバーが決定",
          "クライアントは演出のみ（モーション再生 + エフェクト）",
          "ヒットストップ: サーバーからの確定通知後にローカルで実行",
          "ヒットストップ時間: 通常 3F(50ms) / 重攻撃 5F(83ms)",
        ],
      },
    ],
  },
  {
    title: "ガード & 回避",
    color: "#10B981",
    content: [
      {
        subtitle: "ガードシステム",
        items: [
          "ガード入力: L1 / Shift 長押し",
          "ガード成功: ダメージ 80% カット + のけぞり軽減",
          "ガード削り: 連続ガードでガードゲージ減少→ガードブレイク",
          "ジャストガード（200msウィンドウ）: ダメージ0 + 反撃確定",
          "★ ジャストガード判定はサーバー権威（タイミング検証）",
          "ガード不能攻撃: C1・投げ・無双乱舞",
        ],
      },
      {
        subtitle: "回避（ステップ）",
        items: [
          "回避入力: ×(A) / Space",
          "無敵フレーム: 回避開始から 6F(100ms)",
          "回避硬直: 無敵後 12F(200ms) は攻撃不可",
          "回避スタミナ消費（連続回避制限: 最大3回→クールダウン）",
          "★ 無敵フレームの判定: サーバーが管理",
          "★ 回避位置もサーバーが計算（瞬間移動チート防止）",
        ],
      },
    ],
  },
  {
    title: "無双乱舞 & 覚醒",
    color: "#8B5CF6",
    content: [
      {
        subtitle: "無双乱舞（Ultimate）",
        items: [
          "無双ゲージ MAX で発動可能（○ / R ）",
          "発動時: 全方位に衝撃波（ガード不能・周囲の敵を浮かせ）",
          "乱舞中: 連続攻撃（約 3〜5秒）+ 無敵",
          "フィニッシュ: 大ダメージ広範囲攻撃",
          "★ ゲージ管理はサーバー権威",
          "★ 無敵状態もサーバーが管理（クライアント詐称不可）",
          "範囲: 半径 5m（近接メイン武器）/ 半径 10m（大型武器）",
        ],
      },
      {
        subtitle: "覚醒（Awakening）",
        items: [
          "覚醒ゲージ（別ゲージ）MAX で発動",
          "効果: 攻撃力 +30% / 防御力 +20% / 速度 +15%（30秒間）",
          "覚醒中に無双乱舞 → 真・無双乱舞（強化版）",
          "★ ステータス倍率はサーバーが適用（バフ管理サーバー側）",
        ],
      },
    ],
  },
  {
    title: "ダメージ計算式",
    color: "#06B6D4",
    content: [
      {
        subtitle: "基本ダメージ計算（サーバー側で実行）",
        items: [
          "基礎ダメージ = ATK × モーション倍率",
          "属性補正 = 属性相性テーブル参照（火>風>雷>氷>火, 各1.2倍）",
          "防御計算 = 基礎ダメージ × 属性補正 × (100 / (100 + DEF))",
          "ガード時 = 最終ダメージ × 0.2（ジャストガードなら 0）",
          "クリティカル = 5%確率で 1.5倍（装備で確率UP可）",
          "最終ダメージ = 防御計算 × クリティカル × バフ補正",
          "★ 全計算をサーバーで実行。クライアントの値は一切信用しない",
        ],
      },
      {
        subtitle: "モーション倍率の例（大剣）",
        items: [
          "N1: 0.8 / N2: 0.9 / N3: 1.0 / N4: 1.1 / N5: 1.2 / N6: 1.8",
          "C1: 2.0（単体） / C2: 1.5（打ち上げ）/ C3: 1.3（範囲）",
          "C4: 1.8（突進）/ C5: 0.5×6hit / C6: 3.0（最大技）",
          "無双乱舞: 0.3×15hit + フィニッシュ 4.0",
        ],
      },
    ],
  },
];

/* ─── RANGED SYSTEM ─── */
const rangedSystem = [
  {
    title: "遠隔攻撃の種類",
    color: "#F59E0B",
    content: [
      {
        subtitle: "投射物（Projectile）タイプ",
        items: [
          "弓矢: 直線弾道 / 射程 100m / 弾速 40m/s",
          "投擲（手裏剣・石礫）: 射程 30m / 弾速 25m/s",
          "気弾・衝撃波: 射程 15m / 弾速 20m/s / 貫通あり",
          "火矢（範囲攻撃）: 射程 80m / 着弾点に AoE 3m",
          "各投射物は Prefab として生成 → 物理演算で飛翔",
        ],
      },
      {
        subtitle: "即着（Hitscan-like）タイプ",
        items: [
          "ダッシュ斬り: 射程 10m / 瞬間移動＋攻撃",
          "突進技: 射程 5〜15m / 直線突進",
          "これらは投射物ではなく「移動＋近接判定」として処理",
          "★ ダッシュ距離の上限はサーバーが検証",
        ],
      },
    ],
  },
  {
    title: "投射物のネットワーク同期",
    color: "#10B981",
    critical: true,
    content: [
      {
        subtitle: "★ サーバー権威型の投射物管理",
        items: [
          "1. クライアント: 射撃入力 → サーバーに送信",
          "2. クライアント: 予測として弾をローカル生成（仮表示）",
          "3. サーバー: 入力受信 → 弾の正式生成（サーバー上）",
          "4. サーバー: 弾の位置を毎ティック計算",
          "5. サーバー: 衝突判定（弾 vs プレイヤー Hurtbox）",
          "6. サーバー: ヒット時 → ダメージ確定 → 全員に通知",
          "7. サーバー: 弾の位置を定期同期（10Hz程度でOK）",
        ],
      },
      {
        subtitle: "★ 弾のラグコンペンセーション",
        items: [
          "弓矢（弾速 40m/s）の場合:",
          "  → 100ms遅延 = 4m のズレ → 要補正",
          "  → サーバーが射撃時刻の敵位置で予測軌道を計算",
          "  → 弾着時刻まで弾を進め、その時点での判定実行",
          "近距離技（弾速 20m/s・射程 15m）の場合:",
          "  → 着弾まで 0.75秒 → この間に敵は移動する",
          "  → こちらは予測なし（リアルタイム物理判定でOK）",
          "方針: 弾速が速い(>30m/s)弓のみラグ補正、他はリアル判定",
        ],
      },
    ],
  },
  {
    title: "遠隔 vs 近接のバランス設計",
    color: "#8B5CF6",
    content: [
      {
        subtitle: "遠隔が強すぎない設計方針",
        items: [
          "弓のDPS < 近接のDPS（遠隔はあくまで牽制）",
          "弓チャージ中は移動不可 or 大幅減速",
          "弓の連射間隔: 1.2秒（近接コンボのDPSの60%程度）",
          "弓ヒット時ののけぞり: 無し or 極小（コンボ起点にならない）",
          "近接側のアプローチ手段: ダッシュ・突進技・ガード前進",
          "マップ設計: 遮蔽物を多くして射線を限定",
          "射撃中はミニマップに位置表示（居場所バレ）",
        ],
      },
      {
        subtitle: "遠隔の役割",
        items: [
          "拠点防衛時の牽制・足止め",
          "味方の近接コンボ中の援護（打ち上げた敵への追撃）",
          "逃走する敵へのトドメ",
          "雑兵処理（多数を効率よく倒す）",
          "※ 近接メインゲームにおける「サポート枠」の位置づけ",
        ],
      },
    ],
  },
];

/* ─── NETCODE DETAILS ─── */
const netcodeDetails = [
  {
    title: "近接戦闘特有のラグ問題と対策",
    color: "#EF4444",
    critical: true,
    content: [
      {
        subtitle: "問題1: 「当たったのに当たってない」",
        items: [
          "原因: クライアント予測で敵を斬ったが、サーバー判定では敵が既に移動済み",
          "対策: ラグコンペンセーション（攻撃者の視点時刻で判定）",
          "結果: 攻撃した側は「見えた通りに当たる」",
          "代償: やられた側に「もう避けたのに？」が稀に発生",
          "許容値: 最大 150ms までの巻き戻し（それ以上は切り捨て）",
        ],
      },
      {
        subtitle: "問題2: ヒットストップの同期",
        items: [
          "近接ゲームの「気持ちよさ」にヒットストップは不可欠",
          "しかしサーバー確認を待つと遅延が入り台無しに",
          "対策: クライアント予測ヒット時に即座にヒットストップ再生",
          "サーバーから「ミス」通知が来たらストップを途中キャンセル",
          "近距離（< 5m）での予測精度は高いのでほぼ問題なし",
        ],
      },
      {
        subtitle: "問題3: 投げ・ガード崩しの同時発生",
        items: [
          "2人が同時に投げを入力 → どちらが勝つ？",
          "対策: サーバーのタイムスタンプで先着順",
          "同一ティック内の場合: 入力受信順 or ランダム",
          "投げ抜け: 投げられた側が 150ms 以内に投げ入力 → 投げ抜け成立",
          "★ すべてサーバーが判定（クライアント同士の直接やり取り禁止）",
        ],
      },
      {
        subtitle: "問題4: 打ち上げ→空中コンボの同期",
        items: [
          "C2で敵を打ち上げ → 空中追撃 → 叩きつけ の一連の流れ",
          "空中の敵の位置は放物線（物理）で計算",
          "★ サーバーが打ち上げパラメータを決定（初速・角度）",
          "★ クライアントは同じパラメータで物理再現（決定論的）",
          "追撃ヒットもサーバー判定（空中の Hurtbox 位置はサーバーが正）",
        ],
      },
      {
        subtitle: "問題5: 群衆戦（多対多）の判定爆発",
        items: [
          "C3（360°攻撃）が雑兵20体 + プレイヤー3人に同時ヒット",
          "全てのヒットをサーバーが1ティックで処理する必要",
          "対策1: Spatial Hashing で判定対象を事前フィルタ",
          "対策2: NPC雑兵のヒット判定を簡略化（球 vs 球のみ）",
          "対策3: 1フレームで処理するヒット上限を設定（例: 最大30体）",
          "対策4: NPC判定を別スレッドで並列処理",
        ],
      },
    ],
  },
  {
    title: "同期するデータと頻度",
    color: "#10B981",
    content: [
      {
        subtitle: "毎ティック同期（60Hz サーバー → 30Hz クライアント）",
        items: [
          "プレイヤー位置 (Vector3): 12 bytes",
          "プレイヤー回転 (Y軸のみ): 2 bytes（圧縮）",
          "現在ステート (enum): 1 byte",
          "コンボ段数: 1 byte",
          "HP: 2 bytes",
          "合計: 約 18 bytes/プレイヤー × 8人 = 144 bytes/tick",
          "× 30Hz = 約 4.3 KB/秒（十分軽量）",
        ],
      },
      {
        subtitle: "イベント駆動同期（発生時のみ）",
        items: [
          "ヒット確定通知: 攻撃者ID + 被弾者ID + ダメージ + リアクション種別",
          "ステート変更: キャラID + 新ステート + パラメータ",
          "投射物生成: 位置 + 方向 + 弾速 + 弾種",
          "拠点状態変化: 拠点ID + 新状態 + 制圧率",
          "撃破通知: 撃破者ID + 被撃破者ID",
          "ゲージ変動: プレイヤーID + ゲージ種別 + 現在値",
        ],
      },
      {
        subtitle: "低頻度同期（5〜10Hz）",
        items: [
          "NPC雑兵の位置・ステート（数が多いので低頻度）",
          "NPC武将の位置・ステート・HP",
          "各プレイヤーのバフ/デバフ状態",
          "スコアボード更新",
        ],
      },
    ],
  },
  {
    title: "レイテンシ別の体感品質設計",
    color: "#F59E0B",
    content: [
      {
        subtitle: "ターゲット品質",
        items: [
          "0〜30ms  (同一リージョン): ★★★★★ ほぼオフラインと同等",
          "30〜80ms (国内): ★★★★☆ 快適。予測補正ほぼ不要",
          "80〜150ms (東アジア圏): ★★★☆☆ プレイ可能。稀にズレを感じる",
          "150〜200ms: ★★☆☆☆ ガード/回避のタイミングが厳しい",
          "200ms超: ★☆☆☆☆ 非推奨。マッチング時に警告表示",
        ],
      },
      {
        subtitle: "遅延を感じさせない工夫",
        items: [
          "自キャラの操作: クライアント予測で遅延ゼロに見せる",
          "攻撃ヒット: 予測ヒットで即エフェクト → 体感ゼロ遅延",
          "被弾リアクション: サーバー確定後に再生（若干遅延あり → 許容）",
          "ガード判定: 入力をサーバーに即送信 + ローカル予測ガード",
          "回避無敵: ローカルで即発動 + サーバーで追認",
          "UI（HP変動等）: サーバー値を使用（正確性優先）",
        ],
      },
    ],
  },
];

/* ─── STATE MACHINE ─── */
const stateData = {
  states: [
    { name: "Idle", color: "#71717A", desc: "待機。全アクション入力受付", transitions: ["Move", "Attack", "Guard", "Dash", "Musou"] },
    { name: "Move", color: "#3B82F6", desc: "移動中。攻撃・ガード・回避受付", transitions: ["Idle", "Attack", "Guard", "Dash", "Musou"] },
    { name: "Attack", color: "#EF4444", desc: "攻撃モーション中。次段入力 or チャージ受付", transitions: ["Attack", "Charge", "Idle", "Hitstun"] },
    { name: "Charge", color: "#F59E0B", desc: "チャージ攻撃中。入力不可（一部キャンセル可）", transitions: ["Idle", "Hitstun"] },
    { name: "Guard", color: "#10B981", desc: "ガード中。回避でキャンセル可", transitions: ["Idle", "Move", "Dash", "Guardbreak"] },
    { name: "Dash", color: "#06B6D4", desc: "回避/ステップ中。無敵F→硬直", transitions: ["Idle", "Move"] },
    { name: "Musou", color: "#8B5CF6", desc: "無双乱舞中。無敵。入力不可", transitions: ["Idle"] },
    { name: "Hitstun", color: "#EC4899", desc: "のけぞり/よろめき。行動不能", transitions: ["Idle", "Launch", "Down"] },
    { name: "Launch", color: "#F97316", desc: "打ち上げ空中状態。行動不能", transitions: ["Down", "AirHitstun"] },
    { name: "AirHitstun", color: "#D946EF", desc: "空中被弾。追撃を受けている", transitions: ["Slam", "Down"] },
    { name: "Down", color: "#6B7280", desc: "ダウン状態。起き上がりに無敵", transitions: ["Idle", "Dead"] },
    { name: "Dead", color: "#1F2937", desc: "死亡。リスポーン待ち", transitions: ["Idle"] },
  ],
  serverAuthority: [
    "★ ステート遷移の最終決定権はサーバー",
    "★ クライアントは予測遷移するが、サーバーに否認されたら巻き戻る",
    "★ 無敵状態（Musou/Dash無敵F/起き上がり）はサーバーのみが管理",
    "★ 死亡判定は100%サーバー（HPが0以下 → Dead遷移命令）",
    "★ バフ/デバフによるステータス変更もサーバー計算",
  ],
};

/* ─── IMPLEMENTATION TIMELINE ─── */
const timeline = [
  {
    week: "Week 1-2",
    phase: "M0: 環境構築",
    color: "#6366F1",
    tasks: [
      "GitHub リポジトリ作成 + Git LFS",
      "Unity プロジェクト + ネットワークライブラリ導入",
      "Dedicated Server ビルド確認",
      "ParrelSync（マルチテスト環境）",
    ],
    milestone: "2人がサーバー経由で接続できる",
  },
  {
    week: "Week 3-8",
    phase: "M1: ネットワーク同期基盤 🔥最重要",
    color: "#EF4444",
    tasks: [
      "サーバー権威型の移動同期",
      "クライアント予測 + リコンシリエーション",
      "他プレイヤーの補間表示",
      "ラグコンペンセーション基盤",
      "遅延/パケロスシミュレーター",
    ],
    milestone: "2人が同じ空間を遅延なく走り回れる",
  },
  {
    week: "Week 9-14",
    phase: "M2-A: 近接コンボ + ヒット判定",
    color: "#EC4899",
    tasks: [
      "N1〜N6 コンボチェーン（大剣1種で実装）",
      "Hitbox/Hurtbox システム",
      "サーバー権威ヒット判定",
      "のけぞり/吹き飛ばしリアクション",
      "ヒットストップ（クライアント予測型）",
    ],
    milestone: "2人で殴り合える。ヒット判定がサーバー正",
  },
  {
    week: "Week 15-18",
    phase: "M2-B: チャージ攻撃 + ガード + 回避",
    color: "#F59E0B",
    tasks: [
      "C1〜C6 チャージ派生",
      "打ち上げ→空中追撃コンボ",
      "ガード＆ジャストガード（サーバー判定）",
      "回避ステップ（無敵F サーバー管理）",
      "スタミナ/ガードゲージ",
    ],
    milestone: "攻撃・防御・回避の駆け引きが成立",
  },
  {
    week: "Week 19-22",
    phase: "M2-C: 無双乱舞 + 遠隔攻撃",
    color: "#8B5CF6",
    tasks: [
      "無双ゲージ + 乱舞発動",
      "弓・投擲の投射物同期",
      "投射物のサーバー権威判定",
      "弓のラグコンペンセーション",
      "覚醒システム",
    ],
    milestone: "全攻撃手段が揃い4v4の原型が完成",
  },
  {
    week: "Week 23-28",
    phase: "M3: 4v4対戦モード",
    color: "#10B981",
    tasks: [
      "マッチメイキング（Firebase + Dedicated Server）",
      "合戦マップ（拠点5つ）",
      "雑兵AI（サーバー実行）",
      "武将AI",
      "勝利条件 + リザルト",
    ],
    milestone: "4v4で最後まで対戦が遊べる",
  },
  {
    week: "Week 29-38",
    phase: "M4: 武器種 + キャラ + 育成",
    color: "#8B5CF6",
    tasks: [
      "武器種 4〜6種の固有モーション",
      "キャラクター 8〜12体",
      "装備・育成・スキル",
      "Firestore 永続データ",
    ],
    milestone: "複数キャラ・武器で繰り返し遊べる",
  },
  {
    week: "Week 39-44",
    phase: "M5: インフラ + セキュリティ",
    color: "#06B6D4",
    tasks: [
      "Dedicated Server デプロイ（AWS/GCP）",
      "チート対策（サーバー検証強化）",
      "通信暗号化",
      "サーバー監視",
    ],
    milestone: "外部公開に耐えるセキュリティ",
  },
  {
    week: "Week 45-52",
    phase: "M6: ポリッシュ + α版",
    color: "#EC4899",
    tasks: [
      "エフェクト・サウンド・UI仕上げ",
      "負荷テスト（8人対戦安定）",
      "パフォーマンス最適化",
      "α版公開（Steam/itch.io）",
    ],
    milestone: "🎉 α版リリース！",
  },
];

export default function CombatDesign() {
  const [activeSection, setActiveSection] = useState("overview");
  const [expandedItems, setExpandedItems] = useState({});

  const toggle = (key) => setExpandedItems(prev => ({ ...prev, [key]: !prev[key] }));

  const renderContentBlock = (block, prefix, parentColor) => {
    const key = `${prefix}-${block.subtitle}`;
    const isOpen = expandedItems[key] !== false;
    return (
      <div key={key} style={{
        background: "#13131A",
        borderRadius: 8,
        border: "1px solid #27272A",
        marginBottom: 6,
        overflow: "hidden",
      }}>
        <div onClick={() => toggle(key)} style={{
          padding: "10px 14px",
          cursor: "pointer",
          display: "flex",
          alignItems: "center",
          gap: 8,
        }}>
          <div style={{ flex: 1, fontSize: 13, fontWeight: 600, color: "#E4E4E7" }}>{block.subtitle}</div>
          <span style={{ fontSize: 10, color: "#52525B", transform: isOpen ? "rotate(180deg)" : "", transition: "0.2s" }}>▼</span>
        </div>
        {isOpen && (
          <div style={{ padding: "0 14px 12px", borderTop: "1px solid #1E1E28" }}>
            {block.items.map((item, i) => {
              const isCrit = item.startsWith("★");
              const isIndented = item.startsWith("  ");
              return (
                <div key={i} style={{
                  fontSize: 12,
                  color: isCrit ? "#F59E0B" : "#A1A1AA",
                  fontWeight: isCrit ? 600 : 400,
                  marginTop: 5,
                  paddingLeft: isIndented ? 16 : 0,
                  display: "flex",
                  alignItems: "flex-start",
                  gap: 6,
                  lineHeight: 1.6,
                }}>
                  {!isIndented && (
                    <span style={{ color: isCrit ? "#EF4444" : parentColor, flexShrink: 0, marginTop: 2 }}>
                      {isCrit ? "★" : "·"}
                    </span>
                  )}
                  <span>{item.replace(/^★\s*/, "")}</span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };

  return (
    <div style={{
      minHeight: "100vh",
      background: "#0B0B10",
      color: "#E4E4E7",
      fontFamily: "'Noto Sans JP', sans-serif",
      padding: "20px 16px",
    }}>
      <link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@300;400;500;700;900&family=Orbitron:wght@500;700;900&display=swap" rel="stylesheet" />

      {/* Header */}
      <div style={{
        textAlign: "center", marginBottom: 20, padding: "24px 16px",
        background: "linear-gradient(160deg, #1a0510 0%, #0a1628 50%, #100a18 100%)",
        borderRadius: 14, border: "1px solid #ffffff10", position: "relative", overflow: "hidden",
      }}>
        <div style={{ position: "absolute", inset: 0, background: "radial-gradient(ellipse at 25% 50%, #EF444418 0%, transparent 50%), radial-gradient(ellipse at 75% 30%, #F59E0B12 0%, transparent 50%)" }} />
        <div style={{ position: "relative" }}>
          <div style={{ fontFamily: "Orbitron", fontSize: 10, letterSpacing: 4, color: "#EF4444", fontWeight: 700, marginBottom: 4 }}>COMBAT SYSTEM DESIGN</div>
          <h1 style={{ fontSize: 22, fontWeight: 900, margin: "0 0 4px", color: "#FAFAFA" }}>近接戦闘システム設計書</h1>
          <div style={{ fontSize: 11, color: "#71717A" }}>近接メイン（0〜5m）＋ 遠隔サブ（〜100m）× サーバー権威 × ラグ補正</div>
        </div>
      </div>

      {/* Section Tabs */}
      <div style={{ display: "flex", gap: 3, marginBottom: 16, overflowX: "auto", paddingBottom: 4 }}>
        {sections.map(s => (
          <button key={s.id} onClick={() => setActiveSection(s.id)} style={{
            padding: "7px 12px", borderRadius: 8, fontSize: 11, fontWeight: 600, cursor: "pointer",
            border: activeSection === s.id ? `1px solid ${s.color}60` : "1px solid transparent",
            background: activeSection === s.id ? s.color + "18" : "#16161C",
            color: activeSection === s.id ? s.color : "#71717A",
            fontFamily: "'Noto Sans JP', sans-serif", whiteSpace: "nowrap", flexShrink: 0, transition: "0.15s",
          }}>{s.icon} {s.label}</button>
        ))}
      </div>

      {/* ─── OVERVIEW ─── */}
      {activeSection === "overview" && (
        <div>
          {/* Concept */}
          <div style={{ background: "#16161C", borderRadius: 10, border: "1px solid #27272A", padding: 16, marginBottom: 10 }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA", marginBottom: 10 }}>{overviewData.concept.title}</div>
            {overviewData.concept.items.map((item, i) => (
              <div key={i} style={{ display: "flex", gap: 10, marginBottom: 6, fontSize: 12 }}>
                <span style={{ minWidth: 100, color: "#71717A", flexShrink: 0 }}>{item.label}</span>
                <span style={{ color: item.accent ? "#F59E0B" : "#D4D4D8", fontWeight: item.accent ? 700 : 400 }}>{item.value}</span>
              </div>
            ))}
          </div>
          {/* Range Diagram */}
          <div style={{ background: "#16161C", borderRadius: 10, border: "1px solid #27272A", padding: 16, marginBottom: 10 }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA", marginBottom: 12 }}>交戦距離レンジ</div>
            <div style={{ display: "flex", gap: 2, marginBottom: 8 }}>
              {overviewData.ranges.map((r, i) => (
                <div key={i} style={{
                  width: r.width, background: r.color + "25", borderRadius: 6, padding: "10px 8px",
                  border: `1px solid ${r.color}40`, textAlign: "center",
                }}>
                  <div style={{ fontSize: 11, fontWeight: 700, color: r.color }}>{r.range}</div>
                  <div style={{ fontSize: 10, color: "#A1A1AA", marginTop: 2 }}>{r.name}</div>
                </div>
              ))}
            </div>
            <div style={{ fontSize: 11, color: "#71717A", textAlign: "center" }}>← 近接メイン（90%の交戦） | 遠隔サブ →</div>
          </div>
          {/* Weapons */}
          <div style={{ background: "#16161C", borderRadius: 10, border: "1px solid #27272A", padding: 16 }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA", marginBottom: 12 }}>武器種一覧（初期想定）</div>
            {overviewData.weapons.map((w, i) => (
              <div key={i} style={{
                display: "flex", alignItems: "center", gap: 10, marginBottom: 8,
                padding: "8px 12px", background: "#13131A", borderRadius: 8, border: `1px solid ${w.color}20`,
              }}>
                <div style={{ fontSize: 14, fontWeight: 700, color: w.color, minWidth: 36 }}>{w.name}</div>
                <div style={{ fontSize: 11, color: "#71717A", minWidth: 45 }}>射程{w.range}</div>
                <div style={{ fontSize: 11, color: "#71717A", minWidth: 30 }}>{w.speed}</div>
                <div style={{ fontSize: 11, color: "#F59E0B", minWidth: 70 }}>{w.power}</div>
                <div style={{ fontSize: 11, color: "#A1A1AA" }}>{w.style}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ─── MELEE ─── */}
      {activeSection === "melee" && (
        <div>
          {meleeSystem.map((section, sIdx) => (
            <div key={sIdx} style={{
              background: "#16161C", borderRadius: 12, border: `1px solid ${section.color}25`, padding: 16, marginBottom: 10,
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <div style={{ fontSize: 15, fontWeight: 700, color: "#FAFAFA" }}>{section.title}</div>
                {section.critical && <span style={{ fontSize: 10, padding: "2px 8px", borderRadius: 4, background: "#EF444420", color: "#EF4444", fontWeight: 700 }}>ネットワーク重要</span>}
              </div>
              {section.content.map((block, bIdx) => renderContentBlock(block, `melee-${sIdx}`, section.color))}
            </div>
          ))}
        </div>
      )}

      {/* ─── RANGED ─── */}
      {activeSection === "ranged" && (
        <div>
          {rangedSystem.map((section, sIdx) => (
            <div key={sIdx} style={{
              background: "#16161C", borderRadius: 12, border: `1px solid ${section.color}25`, padding: 16, marginBottom: 10,
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <div style={{ fontSize: 15, fontWeight: 700, color: "#FAFAFA" }}>{section.title}</div>
                {section.critical && <span style={{ fontSize: 10, padding: "2px 8px", borderRadius: 4, background: "#10B98120", color: "#10B981", fontWeight: 700 }}>ネットワーク重要</span>}
              </div>
              {section.content.map((block, bIdx) => renderContentBlock(block, `ranged-${sIdx}`, section.color))}
            </div>
          ))}
        </div>
      )}

      {/* ─── NETCODE ─── */}
      {activeSection === "netcode" && (
        <div>
          {netcodeDetails.map((section, sIdx) => (
            <div key={sIdx} style={{
              background: "#16161C", borderRadius: 12, border: `1px solid ${section.color}25`, padding: 16, marginBottom: 10,
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                <div style={{ fontSize: 15, fontWeight: 700, color: "#FAFAFA" }}>{section.title}</div>
                {section.critical && <span style={{ fontSize: 10, padding: "2px 8px", borderRadius: 4, background: "#EF444420", color: "#EF4444", fontWeight: 700 }}>最重要</span>}
              </div>
              {section.content.map((block, bIdx) => renderContentBlock(block, `net-${sIdx}`, section.color))}
            </div>
          ))}
        </div>
      )}

      {/* ─── STATE MACHINE ─── */}
      {activeSection === "state" && (
        <div>
          <div style={{ background: "#16161C", borderRadius: 12, border: "1px solid #27272A", padding: 16, marginBottom: 10 }}>
            <div style={{ fontSize: 15, fontWeight: 700, color: "#FAFAFA", marginBottom: 12 }}>キャラクターステート一覧</div>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {stateData.states.map((st, i) => (
                <div key={i} style={{
                  padding: "10px 14px", background: "#13131A", borderRadius: 8,
                  borderLeft: `3px solid ${st.color}`,
                }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                    <span style={{ fontSize: 13, fontWeight: 700, color: st.color }}>{st.name}</span>
                    <span style={{ fontSize: 11, color: "#71717A" }}>{st.desc}</span>
                  </div>
                  <div style={{ fontSize: 10, color: "#52525B" }}>
                    → {st.transitions.join(" / ")}
                  </div>
                </div>
              ))}
            </div>
          </div>
          <div style={{
            background: "#16161C", borderRadius: 12, border: "1px solid #EF444425", padding: 16,
          }}>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#EF4444", marginBottom: 10 }}>★ サーバー権威ルール</div>
            {stateData.serverAuthority.map((rule, i) => (
              <div key={i} style={{ fontSize: 12, color: "#F59E0B", marginBottom: 5, lineHeight: 1.6 }}>{rule}</div>
            ))}
          </div>
        </div>
      )}

      {/* ─── TIMELINE ─── */}
      {activeSection === "timeline" && (
        <div>
          {timeline.map((t, i) => (
            <div key={i} style={{
              background: "#16161C", borderRadius: 10, border: `1px solid ${t.color}25`,
              padding: 16, marginBottom: 8, position: "relative",
            }}>
              {i < timeline.length - 1 && (
                <div style={{
                  position: "absolute", bottom: -9, left: 30, width: 2, height: 10,
                  background: `linear-gradient(${t.color}60, transparent)`,
                }} />
              )}
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
                <div style={{
                  padding: "4px 10px", background: t.color + "20", borderRadius: 6,
                  fontSize: 11, fontWeight: 700, color: t.color, flexShrink: 0,
                }}>{t.week}</div>
                <div style={{ fontSize: 14, fontWeight: 700, color: "#FAFAFA" }}>{t.phase}</div>
              </div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 4, marginBottom: 10 }}>
                {t.tasks.map((task, j) => (
                  <span key={j} style={{
                    padding: "4px 10px", background: "#13131A", borderRadius: 6,
                    fontSize: 11, color: "#A1A1AA", border: "1px solid #27272A",
                  }}>{task}</span>
                ))}
              </div>
              <div style={{
                padding: "8px 12px", background: t.color + "10", borderRadius: 6,
                fontSize: 12, color: t.color, fontWeight: 600, borderLeft: `3px solid ${t.color}`,
              }}>
                🏁 {t.milestone}
              </div>
            </div>
          ))}

          {/* Summary */}
          <div style={{
            marginTop: 12, padding: 16, background: "#16161C", borderRadius: 10,
            border: "1px solid #EF444430", fontSize: 12, lineHeight: 1.8,
          }}>
            <div style={{ fontWeight: 700, color: "#EF4444", marginBottom: 6, fontSize: 13 }}>
              🔥 近接対戦ゲームの鉄則
            </div>
            <div style={{ color: "#D4D4D8", marginBottom: 8 }}>
              <strong style={{ color: "#FAFAFA" }}>「殴る→当たる→リアクション」のサイクルを最初にネットワーク込みで作る。</strong><br />
              見た目が箱人間でも、この3拍子が気持ちよく同期すれば勝ち。
              逆にどれだけグラフィックが綺麗でも、ヒット判定がラグで崩壊したら即クソゲー評価。
            </div>
            <div style={{ color: "#F59E0B", fontWeight: 600 }}>
              M1（ネットワーク）→ M2-A（近接ヒット判定）を最優先で突破すること。
              ここが動けば残りは積み上げるだけ。
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
