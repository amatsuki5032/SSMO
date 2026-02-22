using Unity.Netcode;
using UnityEngine;

/// <summary>
/// サーバー側ラグコンペンセーションマネージャー
///
/// 仕組み:
/// 1. 毎 FixedUpdate（60Hz）でワールドスナップショット（全プレイヤーの位置・回転）を記録
/// 2. 攻撃判定時、攻撃者が見ていた時刻まで全プレイヤーを巻き戻す
/// 3. 巻き戻し状態でヒット判定を実行（Physics.OverlapSphere 等）
/// 4. 判定後、全プレイヤーを元の位置に即座に復元
///
/// 使い方（サーバー側 ServerRpc 内）:
///   double viewTime = LagCompensationManager.Instance.EstimateViewTime(clientTimestamp);
///   using (LagCompensationManager.Instance.Rewind(viewTime))
///   {
///       int count = Physics.OverlapSphereNonAlloc(origin, radius, results, layerMask);
///   }
///   // using ブロックを抜けると自動的に元の位置に復元される
///
/// 参考: Valve "Source Multiplayer Networking" / GDC "Overwatch Netcode"
/// </summary>
public class LagCompensationManager : MonoBehaviour
{
    // ============================================================
    // シングルトン
    // ============================================================

    private static LagCompensationManager _instance;

    /// <summary>
    /// 遅延初期化シングルトン。初回アクセス時に自動生成する
    /// サーバーでのみ実質的に動作する（クライアントではスナップショット記録をスキップ）
    /// </summary>
    public static LagCompensationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[LagCompensationManager]");
                _instance = go.AddComponent<LagCompensationManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // ============================================================
    // プレイヤー追跡（固定スロット方式）
    // ============================================================

    // MAX_PLAYERS = 8 の固定スロット。GC 回避のため Dictionary を使わない
    private readonly ulong[] _slotClientIds = new ulong[GameConfig.MAX_PLAYERS];
    private readonly Transform[] _slotTransforms = new Transform[GameConfig.MAX_PLAYERS];

    // ============================================================
    // スナップショットリングバッファ（フラット配列）
    // ============================================================

    // GC 回避のため struct-of-arrays で管理
    // プレイヤーデータのインデックス = snapshotIdx * MAX_PLAYERS + slotIdx
    private readonly double[] _timestamps = new double[GameConfig.SNAPSHOT_BUFFER_SIZE];
    private readonly Vector3[] _positions =
        new Vector3[GameConfig.SNAPSHOT_BUFFER_SIZE * GameConfig.MAX_PLAYERS];
    private readonly float[] _rotationsY =
        new float[GameConfig.SNAPSHOT_BUFFER_SIZE * GameConfig.MAX_PLAYERS];

    private int _snapshotCount;
    private int _writeIndex;

    // ============================================================
    // 巻き戻し復元用バッファ（事前確保・再利用）
    // ============================================================

    // Rewind 時に現在位置を退避し、Dispose で復元する
    private readonly Vector3[] _savedPositions = new Vector3[GameConfig.MAX_PLAYERS];
    private readonly Quaternion[] _savedRotations = new Quaternion[GameConfig.MAX_PLAYERS];
    private bool _isRewound; // ネスト検出用フラグ

    // ============================================================
    // ライフサイクル
    // ============================================================

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// サーバーの FixedUpdate（60Hz）でワールドスナップショットを記録
    /// 128 スナップショット ÷ 60Hz ≒ 約2.1秒分の履歴を保持
    /// </summary>
    private void FixedUpdate()
    {
        // サーバーでのみ記録（クライアントではスキップ）
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        RecordSnapshot();
    }

    // ============================================================
    // プレイヤー登録 / 解除
    // ============================================================

    /// <summary>
    /// プレイヤーをラグコンペンセーション対象に登録する
    /// PlayerMovement.OnNetworkSpawn（サーバー側）から呼ばれる
    /// </summary>
    public void RegisterPlayer(ulong clientId, Transform playerTransform)
    {
        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] == null)
            {
                _slotClientIds[i] = clientId;
                _slotTransforms[i] = playerTransform;
                return;
            }
        }
        Debug.LogWarning(
            $"[LagComp] スロット不足: clientId={clientId} を登録できません（MAX_PLAYERS={GameConfig.MAX_PLAYERS}）");
    }

    /// <summary>
    /// プレイヤーをラグコンペンセーション対象から解除する
    /// PlayerMovement.OnNetworkDespawn（サーバー側）から呼ばれる
    /// </summary>
    public void UnregisterPlayer(ulong clientId)
    {
        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] != null && _slotClientIds[i] == clientId)
            {
                _slotClientIds[i] = 0;
                _slotTransforms[i] = null;
                return;
            }
        }
    }

    // ============================================================
    // スナップショット記録
    // ============================================================

    /// <summary>
    /// 現在の全プレイヤー位置をスナップショットとして記録する
    /// </summary>
    private void RecordSnapshot()
    {
        _timestamps[_writeIndex] = NetworkManager.Singleton.ServerTime.Time;

        int baseIdx = _writeIndex * GameConfig.MAX_PLAYERS;
        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] != null)
            {
                _positions[baseIdx + i] = _slotTransforms[i].position;
                _rotationsY[baseIdx + i] = _slotTransforms[i].eulerAngles.y;
            }
        }

        _writeIndex = (_writeIndex + 1) % GameConfig.SNAPSHOT_BUFFER_SIZE;
        if (_snapshotCount < GameConfig.SNAPSHOT_BUFFER_SIZE)
            _snapshotCount++;
    }

    // ============================================================
    // 巻き戻し / 復元
    // ============================================================

    /// <summary>
    /// 指定時刻まで全プレイヤーを巻き戻す（using スコープで使用）
    /// スコープを抜けると自動的に元の位置に復元される（IDisposable パターン）
    ///
    /// 最大補正時間（150ms）を超える巻き戻しは自動的にクランプされる
    /// </summary>
    public RewindScope Rewind(double timestamp)
    {
        Debug.Assert(!_isRewound, "[LagComp] ネストされた巻き戻しは非対応");
        _isRewound = true;

        // 最大補正時間の制限（高レイテンシプレイヤーの過度な巻き戻しを防ぐ）
        double now = NetworkManager.Singleton.ServerTime.Time;
        double maxRewindSec = GameConfig.MAX_LAG_COMPENSATION_MS / 1000.0;
        double minAllowed = now - maxRewindSec;
        if (timestamp < minAllowed)
        {
            timestamp = minAllowed;
        }

        // 巻き戻し前の位置を退避（復元用）
        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] != null)
            {
                _savedPositions[i] = _slotTransforms[i].position;
                _savedRotations[i] = _slotTransforms[i].rotation;
            }
        }

        // 過去のスナップショットから補間位置を計算して適用
        ApplyRewind(timestamp);

        // コライダー位置を即座に更新（Physics.Overlap 等が正しい位置で判定されるように）
        Physics.SyncTransforms();

        return new RewindScope(this);
    }

    /// <summary>
    /// 指定時刻のプレイヤー位置を計算し、transform に適用する
    /// 2つのスナップショット間を線形補間して正確な位置を求める
    /// </summary>
    private void ApplyRewind(double timestamp)
    {
        if (_snapshotCount == 0) return;

        // リングバッファの最古エントリから走査
        int oldestIdx = (_writeIndex - _snapshotCount + GameConfig.SNAPSHOT_BUFFER_SIZE)
                        % GameConfig.SNAPSHOT_BUFFER_SIZE;

        int beforeIdx = -1;
        int afterIdx = -1;

        // timestamp を挟む2つのスナップショットを探す
        for (int i = 0; i < _snapshotCount - 1; i++)
        {
            int currIdx = (oldestIdx + i) % GameConfig.SNAPSHOT_BUFFER_SIZE;
            int nextIdx = (oldestIdx + i + 1) % GameConfig.SNAPSHOT_BUFFER_SIZE;

            if (_timestamps[currIdx] <= timestamp && timestamp <= _timestamps[nextIdx])
            {
                beforeIdx = currIdx;
                afterIdx = nextIdx;
                break;
            }
        }

        // 対象区間が見つからない場合: 最古 or 最新のスナップショットにフォールバック
        if (beforeIdx < 0)
        {
            int fallbackIdx;
            if (timestamp < _timestamps[oldestIdx])
            {
                // 要求時刻が最古より前 → 最古を使用（これ以上巻き戻せない）
                fallbackIdx = oldestIdx;
            }
            else
            {
                // 要求時刻が最新より後 → 最新を使用（通常ありえないが安全策）
                fallbackIdx = (_writeIndex - 1 + GameConfig.SNAPSHOT_BUFFER_SIZE)
                              % GameConfig.SNAPSHOT_BUFFER_SIZE;
            }

            int baseIdx = fallbackIdx * GameConfig.MAX_PLAYERS;
            for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
            {
                if (_slotTransforms[i] != null)
                {
                    _slotTransforms[i].position = _positions[baseIdx + i];
                    _slotTransforms[i].rotation = Quaternion.Euler(0f, _rotationsY[baseIdx + i], 0f);
                }
            }
            return;
        }

        // 2スナップショット間を線形補間して正確な巻き戻し位置を求める
        double duration = _timestamps[afterIdx] - _timestamps[beforeIdx];
        float t = (duration > 0.0)
            ? Mathf.Clamp01((float)((timestamp - _timestamps[beforeIdx]) / duration))
            : 1f;

        int beforeBase = beforeIdx * GameConfig.MAX_PLAYERS;
        int afterBase = afterIdx * GameConfig.MAX_PLAYERS;

        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] != null)
            {
                _slotTransforms[i].position = Vector3.Lerp(
                    _positions[beforeBase + i],
                    _positions[afterBase + i],
                    t
                );
                _slotTransforms[i].rotation = Quaternion.Slerp(
                    Quaternion.Euler(0f, _rotationsY[beforeBase + i], 0f),
                    Quaternion.Euler(0f, _rotationsY[afterBase + i], 0f),
                    t
                );
            }
        }
    }

    /// <summary>
    /// 巻き戻し前の位置に全プレイヤーを復元する
    /// RewindScope.Dispose() から呼ばれる
    /// </summary>
    internal void RestoreFromRewind()
    {
        for (int i = 0; i < GameConfig.MAX_PLAYERS; i++)
        {
            if (_slotTransforms[i] != null)
            {
                _slotTransforms[i].position = _savedPositions[i];
                _slotTransforms[i].rotation = _savedRotations[i];
            }
        }
        Physics.SyncTransforms();
        _isRewound = false;
    }

    // ============================================================
    // ヘルパーメソッド
    // ============================================================

    /// <summary>
    /// 攻撃者の推定表示時刻を計算する
    /// クライアントが報告したサーバー時刻から補間遅延を差し引いて、
    /// 攻撃者が画面上で見ていた他プレイヤーの時刻を求める
    ///
    /// clientReportedTime: クライアントが攻撃時に取得した NetworkManager.ServerTime.Time
    /// （NGO が RTT を考慮して推定したサーバー時刻）
    /// </summary>
    public double EstimateViewTime(double clientReportedTime)
    {
        double viewTime = clientReportedTime - GameConfig.INTERPOLATION_DELAY;

        // 最大補正時間の制限
        double now = NetworkManager.Singleton.ServerTime.Time;
        double maxRewindSec = GameConfig.MAX_LAG_COMPENSATION_MS / 1000.0;
        double minAllowed = now - maxRewindSec;
        if (viewTime < minAllowed)
        {
            viewTime = minAllowed;
        }

        return viewTime;
    }

    /// <summary>
    /// 巻き戻し状態でスフィアオーバーラップを実行する便利メソッド（NonAlloc版）
    /// M2 の Hitbox/Hurtbox システムから使用する想定
    /// </summary>
    public int RewindOverlapSphere(
        double timestamp,
        Vector3 origin, float radius,
        Collider[] results, int layerMask)
    {
        using (Rewind(timestamp))
        {
            return Physics.OverlapSphereNonAlloc(origin, radius, results, layerMask);
        }
    }

    // ============================================================
    // 巻き戻しスコープ（IDisposable パターン）
    // ============================================================

    /// <summary>
    /// using ブロックで使用する巻き戻しスコープ
    /// スコープを抜けると Dispose() が呼ばれ、全プレイヤーが元の位置に復元される
    /// readonly struct により GC アロケーションを回避
    /// </summary>
    public readonly struct RewindScope : System.IDisposable
    {
        private readonly LagCompensationManager _manager;

        internal RewindScope(LagCompensationManager manager)
        {
            _manager = manager;
        }

        public void Dispose()
        {
            _manager.RestoreFromRewind();
        }
    }
}
