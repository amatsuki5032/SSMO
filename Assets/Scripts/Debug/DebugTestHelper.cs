#if UNITY_EDITOR
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// デバッグ用テストヘルパー（Host限定、Editorのみ）
///
/// NetworkPlayer Prefab にアタッチして使用。
/// Host の自分プレイヤーオブジェクト上でのみ動作する。
///
/// キー操作（使用頻度順）:
///   F1:  相手を正面2mに瞬間移動
///   F2:  全員HP全回復 + Dead 復活
///   F3:  自分の無双ゲージ MAX
///   F4:  自分のHPを20%に（真無双テスト用）
///   F5:  相手にガード状態を強制トグル
///   F6:  相手を背面2mに移動（めくりテスト用）
///   F7:  相手を Hitstun トグル
///   F8:  相手を EG展開トグル（ゲージ補充付き、EG維持用）
///   F9:  相手を Launch トグル
///   F10: 相手のアーマー段階を1上げる（ループ）
///   F11: 自分の武器2を変更（ループ）
///   F12: 表示トグル
///   T:   俯瞰フリーカメラ トグル（CameraController）
/// </summary>
public class DebugTestHelper : NetworkBehaviour
{
    // ============================================================
    // 内部状態
    // ============================================================

    private bool _showGui = true;
    private string _lastAction = "";

    // ============================================================
    // 入力処理（Host の自プレイヤーのみ）
    // ============================================================

    private void Update()
    {
        if (!IsServer || !IsOwner) return;

        if (Input.GetKeyDown(KeyCode.F12)) _showGui = !_showGui;
        if (Input.GetKeyDown(KeyCode.F1))  DoTeleportTarget();
        if (Input.GetKeyDown(KeyCode.F2))  DoHealAll();
        if (Input.GetKeyDown(KeyCode.F3))  DoFillMusou();
        if (Input.GetKeyDown(KeyCode.F4))  DoSetHpLow();
        if (Input.GetKeyDown(KeyCode.F5))  DoToggleGuard();
        if (Input.GetKeyDown(KeyCode.F6))  DoTeleportTargetBehind();
        if (Input.GetKeyDown(KeyCode.F7))  DoToggleHitstun();
        if (Input.GetKeyDown(KeyCode.F8))  DoToggleTargetEGReady();
        if (Input.GetKeyDown(KeyCode.F9))  DoToggleLaunch();
        if (Input.GetKeyDown(KeyCode.F10)) DoCycleArmor();
        if (Input.GetKeyDown(KeyCode.F11)) DoCycleWeapon2();
    }

    // ============================================================
    // ターゲット検索
    // ============================================================

    /// <summary>自分以外の最初のプレイヤーを返す（見つからなければ null）</summary>
    private NetworkObject GetFirstOtherPlayer()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject != NetworkObject)
                return client.PlayerObject;
        }
        return null;
    }

    // ============================================================
    // F7: 相手を Hitstun トグル
    // ============================================================

    private void DoToggleHitstun()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var sm = target.GetComponent<CharacterStateMachine>();
        if (sm == null) return;

        if (sm.CurrentState == CharacterState.Hitstun)
        {
            sm.ForceState(CharacterState.Idle);
            _lastAction = "相手: Hitstun → Idle";
        }
        else
        {
            sm.ForceState(CharacterState.Hitstun);
            _lastAction = "相手: → Hitstun";
        }
    }

    // ============================================================
    // F9: 相手を Launch トグル
    // ============================================================

    private void DoToggleLaunch()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var sm = target.GetComponent<CharacterStateMachine>();
        if (sm == null) return;

        if (sm.CurrentState == CharacterState.Launch)
        {
            sm.ForceState(CharacterState.Idle);
            _lastAction = "相手: Launch → Idle";
        }
        else
        {
            sm.ForceState(CharacterState.Launch);
            _lastAction = "相手: → Launch";
        }
    }

    // ============================================================
    // F3: 自分の無双ゲージを MAX
    // ============================================================

    private void DoFillMusou()
    {
        var gauge = GetComponent<MusouGauge>();
        if (gauge == null) return;

        // AddGauge は内部で MAX にクランプする
        gauge.AddGauge(GameConfig.MUSOU_GAUGE_MAX);
        _lastAction = "自分: 無双ゲージ MAX";
    }

    // ============================================================
    // F8: 相手を EG展開状態にする（トグル）
    // ============================================================

    private void DoToggleTargetEGReady()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var sm = target.GetComponent<CharacterStateMachine>();
        var eg = target.GetComponent<EGSystem>();
        if (sm == null || eg == null) return;

        if (sm.CurrentState == CharacterState.EGReady)
        {
            // EG解除 + 強制維持フラグOFF
            eg.DebugForceEG = false;
            sm.ForceState(CharacterState.Idle);
            _lastAction = "相手: EG展開 → Idle";
        }
        else
        {
            // ゲージ補充 → EGReady 強制 → 強制維持フラグON
            var gauge = target.GetComponent<MusouGauge>();
            if (gauge != null)
                gauge.AddGauge(GameConfig.MUSOU_GAUGE_MAX);

            sm.ForceState(CharacterState.EGReady);
            eg.DebugForceEG = true;
            _lastAction = "相手: → EG展開 (強制維持)";
        }
    }

    // ============================================================
    // F1: 相手を自分の正面2mに瞬間移動
    // ============================================================

    private void DoTeleportTarget()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        Vector3 dest = transform.position + transform.forward * 2f;

        // CharacterController がある場合、直接 position 設定するために一時無効化
        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        target.transform.position = dest;
        if (cc != null) cc.enabled = true;

        _lastAction = "相手: 正面2mに移動";
    }

    // ============================================================
    // F5: 相手にガード状態を強制トグル
    // ============================================================

    private void DoToggleGuard()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var sm = target.GetComponent<CharacterStateMachine>();
        if (sm == null) return;

        bool isGuarding = sm.CurrentState == CharacterState.Guard
                       || sm.CurrentState == CharacterState.GuardMove;
        if (isGuarding)
        {
            sm.ForceState(CharacterState.Idle);
            _lastAction = "相手: Guard → Idle";
        }
        else
        {
            sm.ForceState(CharacterState.Guard);
            _lastAction = "相手: → Guard";
        }
    }

    // ============================================================
    // F4: 自分のHPを20%に設定（真無双テスト用）
    // ============================================================

    private void DoSetHpLow()
    {
        var hp = GetComponent<HealthSystem>();
        if (hp == null) return;

        // HP を 20% に設定（真無双閾値ギリギリ）
        int targetHp = Mathf.Max(1, Mathf.FloorToInt(hp.MaxHp * GameConfig.TRUE_MUSOU_HP_THRESHOLD));
        int damage = hp.CurrentHp - targetHp;
        if (damage > 0)
            hp.TakeDamage(damage);

        _lastAction = $"自分: HP → {targetHp} (20% 真無双テスト)";
    }

    // ============================================================
    // F6: 相手を自分の背面2mに移動（めくりテスト用）
    // ============================================================

    private void DoTeleportTargetBehind()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        // 自分の背面2mに移動（自分が攻撃者の背面にいる = めくり）
        Vector3 dest = transform.position - transform.forward * 2f;

        var cc = target.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        target.transform.position = dest;
        // 相手を自分の方に向かせる（正面から見てめくりの状況を作る）
        target.transform.rotation = Quaternion.LookRotation(transform.position - dest);
        if (cc != null) cc.enabled = true;

        _lastAction = "相手: 自分の背面2mに移動 (めくりテスト)";
    }

    // ============================================================
    // F2: 全員のHP全回復 + Dead 復活
    // ============================================================

    private void DoHealAll()
    {
        int count = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var go = client.PlayerObject.gameObject;
            var hp = go.GetComponent<HealthSystem>();
            var sm = go.GetComponent<CharacterStateMachine>();

            // Dead → Idle に強制復帰（ForceState は Dead からも遷移可能）
            if (sm != null && sm.CurrentState == CharacterState.Dead)
                sm.ForceState(CharacterState.Idle);

            if (hp != null)
                hp.FullHeal();

            count++;
        }
        _lastAction = $"全員HP全回復 ({count}人)";
    }

    // ============================================================
    // F10: 相手のアーマー段階を1上げる（ループ）
    // ============================================================

    private void DoCycleArmor()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var armor = target.GetComponent<ArmorSystem>();
        if (armor == null) return;

        // ArmorLevel: None(1) → ArrowResist(2) → NormalResist(3) → SA(4) → HA(5) → None(1)
        int current = (int)armor.CurrentArmorLevel;
        int next = (current % 5) + 1;
        armor.SetArmorLevel((ArmorLevel)next);
        _lastAction = $"相手: Armor → {(ArmorLevel)next}";
    }

    // ============================================================
    // F11: 自分の武器2を変更（ループ）
    // ============================================================

    private void DoCycleWeapon2()
    {
        var combo = GetComponent<ComboSystem>();
        if (combo == null) return;

        // WeaponType: GreatSword(0) → DualBlades(1) → Spear(2) → Halberd(3) → Fists(4) → Bow(5) → GreatSword(0)
        int current = (int)combo.Weapon2Type;
        int next = (current + 1) % 6;
        combo.SetWeapon2Type((WeaponType)next);
        _lastAction = $"自分: 武器2 → {(WeaponType)next}";
    }

    // ============================================================
    // OnGUI: コマンド一覧 + 現在状態を常時表示
    // ============================================================

    private void OnGUI()
    {
        if (!IsServer || !IsOwner || !_showGui) return;

        // プレイヤー数に応じた高さ計算（デバッグコマンド + 操作キー + ステータス）
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        float boxHeight = 440f + playerCount * 40f;

        float w = 340f;
        float x = Screen.width - w - 10f; // 右上に配置
        float y = 10f;

        GUI.Box(new Rect(x, y, w, boxHeight), "");

        GUIStyle header = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        GUIStyle separator = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = Color.gray }
        };

        // タイトル
        GUI.Label(new Rect(x + 8, y + 2, w, 20), "Debug Test Helper  [F12:非表示]", header);
        y += 22;

        // デバッグコマンド一覧
        string[] cmds =
        {
            "F1 : 相手を正面2mに移動",
            "F2 : 全員HP全回復 + 復活",
            "F3 : 自分 無双ゲージ MAX",
            "F4 : 自分 HP20%(真無双テスト)",
            "F5 : 相手 ガード トグル",
            "F6 : 相手を背面2mに移動(めくり)",
            "F7 : 相手 Hitstun トグル",
            "F8 : 相手 EG展開 トグル",
            "F9 : 相手 Launch トグル",
            "F10: 相手 アーマー+1(ループ)",
            "F11: 自分 武器2変更(ループ)",
            "T  : 俯瞰フリーカメラ トグル",
        };
        foreach (string cmd in cmds)
        {
            GUI.Label(new Rect(x + 12, y, w - 20, 18), cmd, label);
            y += 16;
        }

        // 俯瞰カメラ状態
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam != null && cam.IsDebugFreeCamera)
        {
            GUIStyle freeCamStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x + 12, y, w - 20, 18), "** 俯瞰カメラ ON **", freeCamStyle);
            y += 18;
        }

        // 最終操作
        y += 4;
        GUI.Label(new Rect(x + 12, y, w - 20, 18), $">> {_lastAction}", label);
        y += 20;

        // ── 区切り線 ──
        GUI.Label(new Rect(x + 8, y, w - 16, 16), "──── 通常操作キー ────", separator);
        y += 16;

        // 通常操作キー一覧（PlayerMovement.cs の実際のキーバインドに準拠）
        string[] controls =
        {
            "WASD           : 移動",
            "N / .          : ジャンプ",
            "J / 左クリック : 通常攻撃 (□)",
            "K / 右クリック : チャージ攻撃 (△)",
            "U / O / LShift : ガード (L1)",
            "ガード+K長押し : EG準備 → EG展開",
            "L / 中クリック : 無双 (○)",
            "H              : ブレイクチャージ (L2)",
            "I              : 仙箪強化 (R1)",
        };
        foreach (string ctrl in controls)
        {
            GUI.Label(new Rect(x + 12, y, w - 20, 18), ctrl, label);
            y += 16;
        }

        y += 6;

        // プレイヤー状態
        GUI.Label(new Rect(x + 8, y, w, 20), "--- Player Status ---", header);
        y += 20;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var go = client.PlayerObject.gameObject;
            bool isSelf = client.PlayerObject == NetworkObject;
            var sm = go.GetComponent<CharacterStateMachine>();
            var hp = go.GetComponent<HealthSystem>();
            var gauge = go.GetComponent<MusouGauge>();
            var armorSys = go.GetComponent<ArmorSystem>();

            string tag = isSelf ? "[自分]" : "[相手]";
            string state = sm != null ? sm.CurrentState.ToString() : "?";
            string hpStr = hp != null ? $"{hp.CurrentHp}/{hp.MaxHp}" : "?";
            string musouStr = gauge != null ? $"{gauge.CurrentGauge:F0}/{gauge.MaxGauge:F0}" : "?";
            string armorStr = armorSys != null ? armorSys.CurrentArmorLevel.ToString() : "?";
            var combo = go.GetComponent<ComboSystem>();
            string w2Str = combo != null ? combo.Weapon2Type.ToString() : "?";

            GUI.Label(new Rect(x + 12, y, w - 20, 18),
                $"{tag} {state}  HP:{hpStr}  Musou:{musouStr}  Armor:{armorStr}", label);
            y += 18;
            if (isSelf && combo != null)
            {
                GUI.Label(new Rect(x + 12, y, w - 20, 18),
                    $"     武器2:{w2Str}  BR:{combo.BreakRushStack}/{GameConfig.BREAK_RUSH_MAX_STACK}", label);
                y += 18;
            }
        }
    }
}
#endif
