import socket
import time
import json

# --- 設定 ---
HOST = "127.0.0.1"  # 送信先 (localhost)
PORT = 5005         # 送信ポート
INTERVAL = 7        # 送信間隔 (秒)
# --- ---

# UDPソケットの作成
# AF_INET = IPv4, SOCK_DGRAM = UDP
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

print(f"UDP Sender started.")
print(f"Sending to {HOST}:{PORT} every {INTERVAL} seconds.")
print("Press Ctrl+C to stop.")

event_type = "start"

try:
    while True:
        # 1. 送信するJSONデータを構築
        data = {"event": event_type}
        
        # 2. データをJSON文字列に変換
        message = json.dumps(data)
        
        # 3. データをUTF-8バイトにエンコードして送信
        sock.sendto(message.encode('utf-8'), (HOST, PORT))
        
        print(f"Sent: {message}")

        # 4. イベントタイプを切り替える
        if event_type == "start":
            event_type = "end"
        else:
            event_type = "start"

        # 5. 指定された間隔（7秒）待機
        time.sleep(INTERVAL)

except KeyboardInterrupt:
    print("\nSender stopped by user.")
except Exception as e:
    print(f"An error occurred: {e}")
finally:
    # スクリプト終了時にソケットを閉じる
    sock.close()
    print("Socket closed.")
