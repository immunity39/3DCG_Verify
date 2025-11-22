using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro; // (修正点3) TextMeshProを使用するために追加
using System.Text; // (修正点3) StringBuilderを使用するために追加

public class SolderSimulator : MonoBehaviour
{
    // --- 1. UDP設定 ---
    [Header("UDP Settings")]
    public int port = 5005;

    // --- 2. ハンダ付け物理設定 ---
    [Header("Soldering Physics")]
    public float baseGrowthRate = 1.0f;
    public AnimationCurve temperatureCurve;
    public AnimationCurve angleCurve; 

    // --- 3. 描画設定 ---
    [Header("Rendering")]
    public Material solderMaterial; 
    public float displacementScale = 0.1f;

    // --- 4. 判定設定 ---
    [Header("Solder Judgment")]
    public float minRequiredVolume = 1.5f;
    public float maxAllowedVolume = 3.0f;
    public float minContactTime = 2.0f;
    public float maxBadTempRatio = 0.3f;

    // --- 5. UI設定 (修正点3) ---
    [Header("UI")]
    [Tooltip("デバッグ情報を表示するTextMeshProのUI要素")]
    public TextMeshProUGUI debugText;

    // --- UDP受信スレッド関連 ---
    private Thread receiveThread;
    private UdpClient client;
    private volatile UdpEvent latestEventData = new UdpEvent { @event = "no_contact", temperature = 0, angle = 0 };

    // --- 判定用 統計データ ---
    private float totalSolderVolume = 0.0f;
    private float totalContactTime = 0.0f;
    private float timeInBadTempRange = 0.0f;
    private bool needsJudging = false; // (修正点2) 「一度でも接触したか」のフラグ

    // --- (修正点1) Renderer参照 ---
    private Renderer solderRenderer;
    
    // (修正点3) UI更新用
    private StringBuilder uiStringBuilder = new StringBuilder(256);

    // JSONパース用クラス
    [System.Serializable]
    private class UdpEvent
    {
        public string @event;
        public float temperature;
        public float angle;
    }

    void Start()
    {
        // (修正点1) Rendererを取得し、初期状態を非表示にする
        solderRenderer = GetComponent<Renderer>();
        if (solderRenderer == null)
        {
            Debug.LogError("Rendererが見つかりません。");
        }
        else
        {
            solderRenderer.enabled = false;
        }

        if (solderMaterial == null)
        {
            solderMaterial = solderRenderer.material;
        }
        
        // 初期化: ハンダ量を0に
        UpdateSolderVisuals();
        
        // (修正点3) UIの初期表示
        if (debugText != null)
        {
            UpdateDebugUI(latestEventData);
        }
        
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

        // 2. (修正点2) イベントに応じて処理を分岐
        if (currentEvent.@event == "contact_ongoing")
        {
            HandleSoldering(currentEvent.temperature, currentEvent.angle);
        }
        else if (currentEvent.@event == "no_contact")
        {
            HandleNotSoldering();
        }
        else if (currentEvent.@event == "judge")
        {
            HandleJudgment();
            // 判定後はデータをリセットするため、"no_contact"状態に戻す
            latestEventData = new UdpEvent { @event = "no_contact", temperature = currentEvent.temperature, angle = currentEvent.angle };
        }
        
        // 3. (修正点3) 毎フレームUIを更新
        if (debugText != null)
        {
            UpdateDebugUI(currentEvent);
        }
    }

    // 接触中の処理
    private void HandleSoldering(float temp, float angle)
    {
        // (修正点1) 最初の接触で表示を有効にする
        if (!solderRenderer.enabled)
        {
            solderRenderer.enabled = true;
        }

        // (修正点2) 判定フラグを立てる
        needsJudging = true;

        float tempFactor = temperatureCurve.Evaluate(temp);
        float angleFactor = angleCurve.Evaluate(angle);
        float currentGrowthRate = baseGrowthRate * tempFactor * angleFactor;
        
        totalSolderVolume += currentGrowthRate * Time.deltaTime;
        totalContactTime += Time.deltaTime;
        
        if (tempFactor < 0.5f)
        {
            timeInBadTempRange += Time.deltaTime;
        }
        
        // 接触中はリアルタイムで描画を更新
        UpdateSolderVisuals();
    }

    // 非接触中の処理
    private void HandleNotSoldering()
    {
        // (修正点2) 非接触時は何もしない（ハンダ量が保持される）
        // （将来的にここで自然冷却のロジックなどを追加可能）
    }

    // (修正点2) 判定実行の処理
    private void HandleJudgment()
    {
        // 一度も接触していない場合は判定しない
        if (!needsJudging)
        {
            Debug.LogWarning("No soldering contact detected. Judgment skipped.");
            return;
        }

        // 判定ロジック本体を実行
        JudgeSoldering();
        
        // 判定フラグと統計情報をリセット
        needsJudging = false;
        totalSolderVolume = 0.0f;
        totalContactTime = 0.0f;
        timeInBadTempRange = 0.0f;
        
        // 描画をリセット
        UpdateSolderVisuals();
        
        // (修正点1) 描画を非表示に戻す
        solderRenderer.enabled = false;
    }

    // 描画処理
    private void UpdateSolderVisuals()
    {
        if (solderMaterial != null)
        {
            float displacement = totalSolderVolume * displacementScale;
            solderMaterial.SetFloat("_SolderAmount", displacement);
        }
    }

    // (修正点3) デバッグUI更新ロジック
    private void UpdateDebugUI(UdpEvent currentEvent)
    {
        uiStringBuilder.Clear();
        uiStringBuilder.AppendLine("--- Solder Status (v2) ---");
        
        // 'judge'イベントは一瞬なので、表示上は直前の状態を保持
        if(currentEvent.@event == "judge")
        {
            uiStringBuilder.AppendLine($"STATUS: Judging...");
        }
        else
        {
            uiStringBuilder.AppendLine($"STATUS: {currentEvent.@event}");
        }
        
        uiStringBuilder.AppendLine($"TEMP: {currentEvent.temperature:F1} °C");
        uiStringBuilder.AppendLine($"ANGLE: {currentEvent.angle:F1} °");
        uiStringBuilder.AppendLine($"--- Statistics ---");
        uiStringBuilder.AppendLine($"VOLUME: {totalSolderVolume:F2}");
        uiStringBuilder.AppendLine($"TIME: {totalContactTime:F2} s");
        uiStringBuilder.AppendLine($"Bad Temp Time: {timeInBadTempRange:F2} s");

        debugText.text = uiStringBuilder.ToString();
    }

    // ハンダ付け判定ロジック (変更なし)
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

                UdpEvent receivedEvent = JsonUtility.FromJson<UdpEvent>(text);
                if (receivedEvent != null)
                {
                    latestEventData = receivedEvent;
                }
            }
        }
        catch (SocketException) { }
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
