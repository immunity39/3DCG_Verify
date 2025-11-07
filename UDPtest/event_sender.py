import socket
import time
import json

# ====== 設定 ======
UDP_IP = "127.0.0.1"  # Unityの実行環境IP（同一PCならlocalhostでOK）
UDP_PORT = 5005

# ====== ソケット初期化 ======
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# ====== 疑似イベント送信ループ ======
while True:
    # Startイベント送信
    start_event = {"event": "start", "timestamp": time.time()}
    sock.sendto(json.dumps(start_event).encode('utf-8'), (UDP_IP, UDP_PORT))
    print("Sent: START")
    time.sleep(2)  # 2秒間"接触中"

    # Endイベント送信
    end_event = {"event": "end", "timestamp": time.time()}
    sock.sendto(json.dumps(end_event).encode('utf-8'), (UDP_IP, UDP_PORT))
    print("Sent: END")
    time.sleep(2)  # 次の周期まで待機
