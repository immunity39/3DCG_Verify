using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ContactReceiver : MonoBehaviour
{
    public int port = 5005;
    UdpClient client;
    Thread receiveThread;

    void Start()
    {
        client = new UdpClient(port);
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);
        while (true)
        {
            try
            {
                var data = client.Receive(ref remoteEP);
                var json = Encoding.UTF8.GetString(data);
                var ev = JsonUtility.FromJson<ContactEvent>(json);
                // enqueue to main thread
                MainThreadDispatcher.Instance.Enqueue(() => HandleEvent(ev));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    void HandleEvent(ContactEvent ev)
    {
        Vector3 worldPos = new Vector3(ev.pos[0], ev.pos[1], ev.pos[2]);
        // find or spawn blob at worldPos (simple: spawn new blob each start)
        if (ev.type == "start")
        {
            SolderBlob.Spawn(worldPos, ev.tip_temp_c);
        }
        else if (ev.type == "update")
        {
            SolderBlob.UpdateAt(worldPos, ev.contact_ms, ev.tip_temp_c);
        }
        else if (ev.type == "end")
        {
            SolderBlob.EndAt(worldPos, ev.contact_ms, ev.tip_temp_c);
        }
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        client?.Close();
    }
}

[Serializable]
public class ContactEvent
{
    public string type;
    public float[] pos;
    public int contact_ms;
    public float tip_temp_c;
    public double timestamp;
}
