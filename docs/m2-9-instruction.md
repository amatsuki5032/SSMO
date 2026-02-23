# M2-9: アーマーシステム

## 概要
アーマー段階（5段階）と攻撃レベルの比較により、のけぞるかどうかを判定する。
アーマーはのけぞり無効化のみ。ダメージは常に通る。

## 事前確認
- ReactionSystem.cs を確認
- CharacterState.cs の ArmorLevel / AttackLevel enum を確認
- docs/combat-spec.md セクション11（アーマーシステム）を参照

---

## 1. アーマー判定ルール

| アーマー段階 | 耐性 |
|-------------|------|
| 1 (通常) | なし（全てのけぞる） |
| 2 (矢耐性) | 雑魚の矢でのけぞらない |
| 3 (N耐性) | 通常攻撃でのけぞらない |
| 4 (SA) | チャージでものけぞらない |
| 5 (HA) | 無双でものけぞらない |

| 攻撃レベル | 該当する攻撃 |
|-----------|------------|
| 1 | 雑魚の矢 |
| 2 | 通常攻撃 (N) |
| 3 | チャージ (C) / エボリューション (E) |
| 4 | 無双乱舞 |

### 判定
- 攻撃レベル > アーマー段階 → のけぞる（リアクション発生）
- 攻撃レベル ≤ アーマー段階 → のけぞらない（ダメージは受ける）

---

## 2. ArmorSystem.cs 新規作成（Assets/Scripts/Combat/）

### クラス設計
- NetworkBehaviour を継承

### NetworkVariable
- `NetworkVariable<byte> _armorLevel`: 現在のアーマー段階（デフォルト: 1）

### メソッド

#### `SetArmorLevel(byte level)` ★サーバー側
- アーマー段階を設定（装備・バフ・特定モーション中に変更）

#### `ShouldFlinch(AttackLevel attackLevel)` ★サーバー側
- (int)attackLevel > _armorLevel.Value → true（のけぞる）
- それ以外 → false（のけぞらない）

---

## 3. ReactionSystem.cs の修正

### ApplyReaction の前にアーマー判定を追加
1. ArmorSystem.ShouldFlinch(attackLevel) を呼ぶ
2. false → リアクションをスキップ（ダメージのみ適用）
3. true → 通常通りリアクション適用
4. Console ログ: `[Armor] アーマーでのけぞり無効` or 通常リアクション

### 無双中は無敵（アーマーとは別）
- 無双中は IsInvincible() == true → ダメージ自体を受けない
- アーマーはダメージを通す → 別の仕組み

---

## 4. HitboxSystem.cs の修正
- 攻撃種別に応じた AttackLevel を設定:
  - N1〜N4: AttackLevel.Normal (2)
  - C1〜C6: AttackLevel.Charge (3)
  - DashAttack: AttackLevel.Normal (2)
  - Musou: AttackLevel.Musou (4)

---

## 5. NetworkPlayer Prefab への追加
- ArmorSystem コンポーネントを追加

---

## 6. テスト内容
1. **アーマー1（デフォルト）+ N攻撃** → のけぞる
2. **アーマー3（N耐性）+ N攻撃** → のけぞらない（ダメージは通る）
3. **アーマー3 + C攻撃** → のけぞる
4. **アーマー4（SA）+ C攻撃** → のけぞらない
5. **アーマー変更テスト**: デバッグ用にキー（例: F1-F5）でアーマー段階を変更

---

## 7. 完了条件
- [ ] アーマー段階と攻撃レベルの比較が正しく動作
- [ ] アーマーでのけぞり無効時もダメージは通る
- [ ] 無敵とアーマーが区別されている
- [ ] サーバー権威でアーマー判定
- [ ] 既存動作が壊れていない
- [ ] git commit & push: "M2-9: アーマーシステム"
