import socket
import time
import json
import keyboard

# --- 設定 ---
HOST = "127.0.0.1"  # 送信先 (localhost)
PORT = 5005         # 送信ポート
SEND_INTERVAL = 0.05 # 送信間隔 (秒) ... 1秒間に20回更新
# --- ---

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
server_address = (HOST, PORT)

print(f"UDP Sender started.")
print(f"Sending to {HOST}:{PORT} at {1/SEND_INTERVAL:.0f} Hz.")
print("Hold [Spacebar] to simulate 'contact'.")
print("Press Ctrl+C to stop.")

last_event = "" # 最後のイベント状態を記憶
last_contact_flag = False
contact_flag = False

try:
    while True:
        # 1. スペースキーが押されているか判定
        if keyboard.is_pressed('space'):
            current_event = "contact_ongoing"
            contact_flag = True
        else:
            current_event = "no_contact"
            contact_flag = False
        
        if contact_flag != last_contact_flag:
            print(f"Contact state changed: {current_event}")
            last_contact_flag = contact_flag
            
        # 2. 状態が変化したか、"contact_ongoing"が継続している場合のみ送信
        #    (no_contactを送り続ける必要はないため)
        #    -> 変更：常時送信する（Unity側が途切れたことを検知できるように）
        # if current_event != last_event or current_event == "contact_ongoing":
        
        # 3. 送信するJSONデータを構築
        data = {"event": current_event}
        
        # 4. データをJSON文字列に変換し、送信
        message = json.dumps(data)
        sock.sendto(message.encode('utf-8'), server_address)

        # 状態を更新
        last_event = current_event
        
        # 5. 短い待機
        time.sleep(SEND_INTERVAL)

except KeyboardInterrupt:
    print("\nSender stopped by user.")
except Exception as e:
    print(f"An error occurred: {e}")
finally:
    # 終了時に "no_contact" (または "end") を送信
    message = json.dumps({"event": "no_contact"})
    sock.sendto(message.encode('utf-8'), server_address)
    sock.close()
    print("Socket closed.")
