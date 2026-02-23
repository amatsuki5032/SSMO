#if UNITY_EDITOR
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// デバッグ用テストヘルパー（Host限定、Editorのみ）
///
/// NetworkPlayer Prefab にアタッチして使用。
/// Host の自分プレイヤーオブジェクト上でのみ動作する。
///
/// キー操作:
///   F1:  相手を Hitstun トグル
///   F2:  相手を Launch トグル
///   F3:  自分の無双ゲージ MAX
///   F4:  相手を EG Ready トグル（ゲージ補充付き）
///   F5:  相手を自分の正面2mに瞬間移動
///   F6:  相手にガード状態を強制トグル
///   F9:  全員のHP全回復 + Dead 復活
///   F10: 相手のアーマー段階を1上げる（ループ）
///   F12: 表示トグル
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
        if (Input.GetKeyDown(KeyCode.F1))  DoToggleHitstun();
        if (Input.GetKeyDown(KeyCode.F2))  DoToggleLaunch();
        if (Input.GetKeyDown(KeyCode.F3))  DoFillMusou();
        if (Input.GetKeyDown(KeyCode.F4))  DoToggleTargetEGReady();
        if (Input.GetKeyDown(KeyCode.F5))  DoTeleportTarget();
        if (Input.GetKeyDown(KeyCode.F6))  DoToggleGuard();
        if (Input.GetKeyDown(KeyCode.F9))  DoHealAll();
        if (Input.GetKeyDown(KeyCode.F10)) DoCycleArmor();
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
    // F1: 相手を Hitstun トグル
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
    // F2: 相手を Launch トグル
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
    // F4: 相手を EG Ready 状態にする（トグル）
    // ============================================================

    private void DoToggleTargetEGReady()
    {
        var target = GetFirstOtherPlayer();
        if (target == null) { _lastAction = "対象なし"; return; }

        var sm = target.GetComponent<CharacterStateMachine>();
        if (sm == null) return;

        if (sm.CurrentState == CharacterState.EGReady)
        {
            sm.ForceState(CharacterState.Idle);
            _lastAction = "相手: EGReady → Idle";
        }
        else
        {
            // EG維持に無双ゲージが必要なため、先にゲージを満タンにする
            var gauge = target.GetComponent<MusouGauge>();
            if (gauge != null)
                gauge.AddGauge(GameConfig.MUSOU_GAUGE_MAX);

            sm.ForceState(CharacterState.EGReady);
            _lastAction = "相手: → EGReady (ゲージ補充済)";
        }
    }

    // ============================================================
    // F5: 相手を自分の正面2mに瞬間移動
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
    // F6: 相手にガード状態を強制トグル
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
    // F9: 全員のHP全回復 + Dead 復活
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
    // OnGUI: コマンド一覧 + 現在状態を常時表示
    // ============================================================

    private void OnGUI()
    {
        if (!IsServer || !IsOwner || !_showGui) return;

        // プレイヤー数に応じた高さ計算（デバッグコマンド + 操作キー + ステータス）
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        float boxHeight = 390f + playerCount * 20f;

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
            "F1 : 相手 Hitstun トグル",
            "F2 : 相手 Launch トグル",
            "F3 : 自分 無双ゲージ MAX",
            "F4 : 相手 EG Ready トグル",
            "F5 : 相手を正面2mに移動",
            "F6 : 相手 ガード トグル",
            "F9 : 全員HP全回復 + 復活",
            "F10: 相手 アーマー+1(ループ)",
        };
        foreach (string cmd in cmds)
        {
            GUI.Label(new Rect(x + 12, y, w - 20, 18), cmd, label);
            y += 16;
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
            "WASD       : 移動",
            "Space      : ジャンプ",
            "左クリック : 通常攻撃 (□)",
            "右クリック : チャージ攻撃 (△)",
            "LShift     : ガード (L1)",
            "LShift+右長押し : EG準備 → EG完成",
            "Q / 中クリック  : 無双 (○)",
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

            GUI.Label(new Rect(x + 12, y, w - 20, 18),
                $"{tag} {state}  HP:{hpStr}  Musou:{musouStr}  Armor:{armorStr}", label);
            y += 18;
        }
    }
}
#endif
