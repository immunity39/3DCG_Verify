using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading; // スレッド処理に必要

public class UdpReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 5005; // Python側で指定したポートと合わせる

    [Header("Cube Settings")]
    public Color startColor = Color.green; // 'start' イベントで変更する色
    public Color endColor = Color.red;     // 'end' イベントで変更する色

    // --- プライベート変数 ---
    
    // UDPリスニング用のスレッド
    private Thread receiveThread;
    
    // UDPクライアント
    private UdpClient client;

    // Cubeのマテリアルへの参照
    private Material cubeMaterial;

    // メインスレッド(Update)と受信スレッド(ReceiveData)で
    // データを安全にやり取りするための変数
    // 'volatile' は、複数のスレッドからアクセスされる変数が
    // 常に最新の値であることを保証します。
    private volatile string latestReceivedEvent = null;
    
    // スレッドを停止させるためのフラグ
    private volatile bool stopThread = false;

    // JSONデータをパースするためのヘルパークラス
    // Python側のJSONキー {"event": "..."} と一致させる
    [System.Serializable]
    private class UdpEvent
    {
        // 'event' はC#の予約語(キーワード)なため、
        // フィールド名として使う場合は @ を先頭につけます。
        public string @event;
    }

    // --- Unityライフサイクルメソッド ---

    void Start()
    {
        // 1. Cubeのマテリアルを取得
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("このオブジェクトにRendererがアタッチされていません。");
            return;
        }
        cubeMaterial = renderer.material;
        
        // 2. Cubeの初期色を設定 (endColorにしておく)
        cubeMaterial.color = endColor;

        // 3. UDP受信スレッドを開始
        StartReceiveThread();
    }

    void Update()
    {
        // 1. 受信スレッドから新しいイベントが届いていないか確認
        if (latestReceivedEvent != null)
        {
            // 2. データをローカル変数にコピーし、共有変数をnullに戻す
            //    (次のイベント受信に備える)
            string eventToProcess = latestReceivedEvent;
            latestReceivedEvent = null;

            Debug.Log($"MainThread Processing event: {eventToProcess}");

            // 3. イベント名に応じてCubeの色を変更
            //    (UnityのAPI操作は必ずメインスレッド＝Update内で行う)
            if (eventToProcess == "start")
            {
                cubeMaterial.color = startColor;
            }
            else if (eventToProcess == "end")
            {
                cubeMaterial.color = endColor;
            }
        }
    }

    // オブジェクトが破棄される時 (シーン終了時など) に呼ばれる
    void OnDestroy()
    {
        StopReceiveThread();
    }

    // アプリケーションが終了する時に呼ばれる
    void OnApplicationQuit()
    {
        StopReceiveThread();
    }

    // --- UDP受信処理 ---

    private void StartReceiveThread()
    {
        // 受信スレッドを初期化して開始
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true; // アプリケーション終了時に自動的にスレッドを終了する
        receiveThread.Start();
        Debug.Log($"UDP Receive Thread started on port {port}.");
    }

    private void ReceiveData()
    {
        client = null;
        
        try
        {
            // 1. 指定ポートでUDPクライアントを初期化
            client = new UdpClient(port);
            
            // 任意のIPアドレスからのデータを受信
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (!stopThread) // stopThreadフラグが立つまでループ
            {
                try
                {
                    // 2. データを受信 (データが来るまでここで待機 = ブロッキング)
                    byte[] data = client.Receive(ref anyIP);

                    // 3. バイトデータをUTF-8文字列にデコード
                    string text = Encoding.UTF8.GetString(data);
                    
                    // 4. JSONをパース
                    UdpEvent receivedEvent = JsonUtility.FromJson<UdpEvent>(text);

                    if (receivedEvent != null && !string.IsNullOrEmpty(receivedEvent.@event))
                    {
                        // 5. メインスレッド(Update)に処理を渡すため、
                        //    volatile変数に受信イベント名を格納
                        latestReceivedEvent = receivedEvent.@event;
                    }
                }
                catch (SocketException ex)
                {
                    // クライアントがClose()されると例外が発生するが、
                    // スレッド停止要求(stopThread=true)によるものなら正常
                    if (!stopThread)
                    {
                        Debug.LogWarning($"SocketException in ReceiveData: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UDP receive thread: {e.Message}");
        }
        finally
        {
            // 6. スレッド終了時にクライアントを閉じる (ポート解放)
            if (client != null)
            {
                client.Close();
            }
            Debug.Log("UDP Receive Thread stopped.");
        }
    }

    private void StopReceiveThread()
    {
        // 1. ループ停止フラグを立てる
        stopThread = true;

        // 2. クライアントを閉じると、Receive()のブロックが解除され、
        //    SocketExceptionが発生してスレッドのwhileループが終了する
        if (client != null)
        {
            client.Close();
            client = null;
        }

        // 3. スレッドが安全に終了するのを待つ (任意)
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500); // 最大0.5秒待機
        }
        Debug.Log("UDP Receiver stopped.");
    }
}
