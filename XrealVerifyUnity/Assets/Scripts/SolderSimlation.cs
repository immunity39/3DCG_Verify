using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SolderSimulator : MonoBehaviour
{
    // --- 1. UDP設定 ---
    [Header("UDP Settings")]
    public int port = 5005;

    // --- 2. ハンダ付け物理設定 (ステップ2) ---
    [Header("Soldering Physics")]
    public float baseGrowthRate = 1.0f; // ハンダの基本増加量 (体積/秒)
    
    [Tooltip("温度(X軸)と熱効率(Y軸, 0-1)のマッピング。例: 350℃で1.0")]
    public AnimationCurve temperatureCurve;
    
    [Tooltip("角度(0-90度)と面積効率(0-1)のマッピング。例: 45度で1.0")]
    public AnimationCurve angleCurve; // 角度もカーブで設定可能に

    // --- 3. 描画設定 (ステップ3) ---
    [Header("Rendering")]
    public Material solderMaterial; // 頂点変位シェーダーが適用されたマテリアル
    
    [Tooltip("シェーダーに渡す膨張スケール")]
    public float displacementScale = 0.1f;

    // --- 4. 判定設定 (ステップ4) ---
    [Header("Solder Judgment")]
    public float minRequiredVolume = 1.5f;
    public float maxAllowedVolume = 3.0f;
    public float minContactTime = 2.0f; // 最低限必要な接触時間 (秒)
    
    [Tooltip("全体の接触時間のうち、不適正な温度だった時間の許容割合 (0.0 - 1.0)")]
    public float maxBadTempRatio = 0.3f; // 30%

    // --- UDP受信スレッド関連 ---
    private Thread receiveThread;
    private UdpClient client;
    private volatile UdpEvent latestEventData = new UdpEvent { @event = "no_contact", temperature = 0, angle = 0 };

    // --- 判定用 統計データ ---
    private float totalSolderVolume = 0.0f;
    private float totalContactTime = 0.0f;
    private float timeInBadTempRange = 0.0f; // 不適正な温度だった累積時間
    private bool needsJudging = false; // 判定が必要かどうかのフラグ

    // JSONパース用クラス (Pythonのキーと一致させる)
    [System.Serializable]
    private class UdpEvent
    {
        public string @event;
        public float temperature;
        public float angle;
    }

    void Start()
    {
        // マテリアルが設定されていなければ、自身のRendererから取得
        if (solderMaterial == null)
        {
            solderMaterial = GetComponent<Renderer>().material;
        }
        
        // 初期化: ハンダ量を0に
        UpdateSolderVisuals();
        
        // 受信スレッド開始
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"UDP Solder Simulator started on port {port}.");
    }

    void Update()
    {
        // 1. スレッドから最新のイベントデータをコピー
        UdpEvent currentEvent = latestEventData;

        // 2. 接触状態に応じて処理を分岐
        if (currentEvent.@event == "contact_ongoing")
        {
            HandleSoldering(currentEvent.temperature, currentEvent.angle);
        }
        else // "no_contact"
        {
            HandleNotSoldering();
        }
        
        // 3. 描画を更新
        UpdateSolderVisuals();
    }

    // (ステップ2) 接触中の処理
    private void HandleSoldering(float temp, float angle)
    {
        // 判定フラグを立てる
        needsJudging = true;

        // 1. 温度係数をカーブから評価
        // (例: 350℃ -> 1.0, 200℃ -> 0.0, 450℃ -> 0.3)
        float tempFactor = temperatureCurve.Evaluate(temp);
        
        // 2. 角度(面積)係数をカーブから評価
        // (例: 45° -> 1.0, 0° -> 0.2, 90° -> 0.2)
        float angleFactor = angleCurve.Evaluate(angle);

        // 3. このフレームでの増加量を計算
        float currentGrowthRate = baseGrowthRate * tempFactor * angleFactor;
        
        // 4. 統計データを蓄積
        totalSolderVolume += currentGrowthRate * Time.deltaTime;
        totalContactTime += Time.deltaTime;
        
        // (ステップ4用) 温度が不適正か判定
        // (効率が 0.5 未満の領域を「不適正」とする)
        if (tempFactor < 0.5f)
        {
            timeInBadTempRange += Time.deltaTime;
        }
    }

    // (ステップ4) 非接触中の処理
    private void HandleNotSoldering()
    {
        // 接触が終了した瞬間に、一度だけ判定を実行
        if (needsJudging)
        {
            JudgeSoldering();
            needsJudging = false; // 判定フラグを下ろす
            
            // 次のハンダ付けのために統計情報をリセット
            totalSolderVolume = 0.0f;
            totalContactTime = 0.0f;
            timeInBadTempRange = 0.0f;
        }
    }

    // (ステップ3) 描画処理
    private void UpdateSolderVisuals()
    {
        if (solderMaterial != null)
        {
            // シェーダーの "_SolderAmount" プロパティに、
            // 計算した総ハンダ量に応じた変位スケールを渡す
            float displacement = totalSolderVolume * displacementScale;
            solderMaterial.SetFloat("_SolderAmount", displacement);
        }
    }

    // (ステップ4) ハンダ付け判定ロジック
    private void JudgeSoldering()
    {
        Debug.Log("--- Soldering Judgment ---");
        Debug.Log($"Total Volume: {totalSolderVolume:F2} (Target: {minRequiredVolume:F1}-{maxAllowedVolume:F1})");
        Debug.Log($"Total Time: {totalContactTime:F2}s (Min: {minContactTime:F1}s)");
        
        float badTempRatio = (totalContactTime > 0) ? (timeInBadTempRange / totalContactTime) : 0;
        Debug.Log($"Bad Temp Ratio: {badTempRatio * 100:F1}% (Max: {maxBadTempRatio * 100:F1}%)");

        // 失敗判定
        if (totalContactTime < minContactTime)
        {
            Debug.LogError($"[Failure: Poor Wetting] 接触時間が短すぎます (Minimum: {minContactTime:F1}s)");
        }
        else if (totalSolderVolume < minRequiredVolume)
        {
            Debug.LogError("[Failure: Insufficient Solder] ハンダの量が少なすぎます。");
        }
        else if (totalSolderVolume > maxAllowedVolume)
        {
            Debug.LogError("[Failure: Solder Bridging] ハンダの量が多すぎます（ブリッジの危険）。");
        }
        else if (badTempRatio > maxBadTempRatio)
        {
            Debug.LogError("[Failure: Cold/Burnt Joint] 不適正な温度での作業時間が長すぎます。");
        }
        else
        {
            // すべての条件をクリア
            Debug.LogAssertion("[Success!] 適切なハンダ付けです。");
        }
        Debug.Log("--------------------------");
    }

    // --- UDPスレッド ---
    private void ReceiveData()
    {
        client = null;
        try
        {
            client = new UdpClient(port);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                // JSONをパースしてスレッドセーフな変数に格納
                UdpEvent receivedEvent = JsonUtility.FromJson<UdpEvent>(text);
                if (receivedEvent != null)
                {
                    latestEventData = receivedEvent;
                }
            }
        }
        catch (SocketException)
        {
            // アプリ終了時の正常な停止
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UDP thread: {e.Message}");
        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }

    void OnApplicationQuit()
    {
        // アプリ終了時にスレッドとクライアントを確実に閉じる
        if (client != null)
        {
            client.Close();
        }
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
    }
}
