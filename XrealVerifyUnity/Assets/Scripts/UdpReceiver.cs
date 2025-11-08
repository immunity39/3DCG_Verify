using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UdpReceiver : MonoBehaviour
{
    public int listenPort = 5005;
    private UdpClient udpClient;
    private Thread receiveThread;
    private string lastMessage = "";

    private Renderer objRenderer;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
        udpClient = new UdpClient(listenPort);

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log($"[UDP] Listening on port {listenPort}");
    }

    private void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);

        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data);
                lastMessage = json;
            }
            catch (Exception e)
            {
                Debug.LogError("[UDP Error] " + e.ToString());
            }
        }
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(lastMessage))
        {
            try
            {
                var msg = JsonUtility.FromJson<EventMessage>(lastMessage);
                if (msg.eventName == "start")
                {
                    objRenderer.material.color = Color.red;
                }
                else if (msg.eventName == "end")
                {
                    objRenderer.material.color = Color.white;
                }
            }
            catch { /* JSON parse失敗時は無視 */ }

            lastMessage = "";
        }
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        udpClient?.Close();
    }

    [Serializable]
    public class EventMessage
    {
        public string eventName;
        public double timestamp;
    }
}
