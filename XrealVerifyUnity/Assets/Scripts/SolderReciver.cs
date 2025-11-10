using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SolderReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 5005;

    [Header("Soldering Settings")]
    public float growthRate = 0.5f;   // 1秒間にどれだけスケールが大きくなるか
    public float maxSize = 3.0f;      // ハンダの最大サイズ
    public float shrinkRate = 1.0f;   // (オプション) 冷えて固まる速度

    // --- プライベート変数 ---
    private Thread receiveThread;
    private UdpClient client;
    
    // スレッド間での状態共有用
    // volatile: 複数のスレッドからアクセスされる変数が
    // 常に最新の値であることを保証します。
    private volatile bool isSoldering = false;

    // JSONパース用クラス
    [System.Serializable]
    private class UdpEvent
    {
        public string @event; // "contact_ongoing" または "no_contact"
    }

    private Transform solderTransform;

    void Start()
    {
        // 1. 自身のTransform（SolderPuddleのTransform）を取得
        solderTransform = this.transform;
        
        // 2. ハンダの初期サイズを0にする
        solderTransform.localScale = Vector3.zero;

        // 3. UDP受信スレッドを開始
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"Solder UDP Thread started on port {port}.");
    }

    void Update()
    {
        // 4. メインスレッドで描画処理を安全に行う
        bool currentlySoldering = isSoldering; // volatile変数をローカルにコピー

        if (currentlySoldering)
        {
            // 5. [接触中] -> ハンダを大きくする
            float currentSize = solderTransform.localScale.x;
            if (currentSize < maxSize)
            {
                // Time.deltaTimeをかけることで、フレームレートに関わらず
                // 1秒間に growthRate 分だけ成長する
                float newSize = currentSize + growthRate * Time.deltaTime;
                solderTransform.localScale = Vector3.one * Mathf.Min(newSize, maxSize);
            }
        }
        else
        {
            // 6. [未接触] -> (オプション) ハンダをゆっくり縮小（冷却）させる
            //    もし成長したまま保持したい場合は、この 'else' ブロック自体を削除
            
            float currentSize = solderTransform.localScale.x;
            if (currentSize > 0)
            {
                float newSize = currentSize - shrinkRate * Time.deltaTime;
                solderTransform.localScale = Vector3.one * Mathf.Max(newSize, 0);
            }
        }
    }

    private void ReceiveData()
    {
        client = null;
        try
        {
            client = new UdpClient(port);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (true) // スレッドはアプリ終了まで回り続ける
            {
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                // JSONをパース
                UdpEvent receivedEvent = JsonUtility.FromJson<UdpEvent>(text);

                if (receivedEvent != null)
                {
                    // 7. 受信スレッドからメインスレッドへ状態を渡す
                    if (receivedEvent.@event == "contact_ongoing")
                    {
                        isSoldering = true;
                    }
                    else if (receivedEvent.@event == "no_contact")
                    {
                        isSoldering = false;
                    }
                }
            }
        }
        catch (SocketException ex)
        {
            // アプリ終了時にUdpClientが閉じられると例外が出るが、正常な動作
            Debug.Log($"SocketException (likely normal on stop): {ex.Message}");
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
            Debug.Log("UDP Receive Thread stopped.");
        }
    }

    // アプリケーション終了時にスレッドを確実にとめる
    void OnApplicationQuit()
    {
        if (client != null)
        {
            client.Close();
        }
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort(); // スレッドを強制終了
        }
    }
}
