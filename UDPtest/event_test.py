import socket
import time
import json
import keyboard
import os

# --- 設定 ---
HOST = "127.0.0.1"
PORT = 5005
SEND_INTERVAL = 0.05  # 20Hz (0.05秒ごと)

# --- シミュレーション用パラメータ ---
current_temperature = 350.0  # (℃)
current_angle = 45.0         # (度)

# --- UDPセットアップ ---
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
server_address = (HOST, PORT)

def clear_console():
    """コンソールをクリアする"""
    os.system('cls' if os.name == 'nt' else 'clear')

def update_parameters():
    """キー入力に基づいてパラメータを更新する"""
    global current_temperature, current_angle
    
    # 温度の変更 (Up/Down)
    if keyboard.is_pressed('up arrow'):
        current_temperature += 2.0
    elif keyboard.is_pressed('down arrow'):
        current_temperature -= 2.0
        
    # 角度の変更 (Left/Right)
    if keyboard.is_pressed('right arrow'):
        current_angle += 1.0
    elif keyboard.is_pressed('left arrow'):
        current_angle -= 1.0

    # パラメータの範囲を制限
    current_temperature = max(150.0, min(500.0, current_temperature))
    current_angle = max(0.0, min(90.0, current_angle))
    
    # 接触状態の判定 (Space)
    if keyboard.is_pressed('space'):
        return "contact_ongoing"
    else:
        return "no_contact"

def print_status(event):
    """現在の状態をコンソールに表示する"""
    clear_console()
    print("--- Python Solder Simulator ---")
    print(f"Sending to {HOST}:{PORT} at {1/SEND_INTERVAL:.0f} Hz")
    print("\n--- Controls ---")
    print("[Spacebar] : Hold to Solder (接触)")
    print("[Up/Down]  : Change Temperature (温度)")
    print("[Left/Right]: Change Angle (角度)")
    print("\n--- Current Status ---")
    print(f"EVENT      : {event}")
    print(f"TEMPERATURE: {current_temperature:.1f} °C")
    print(f"ANGLE      : {current_angle:.1f} °")
    print("\nPress Ctrl+C to stop.")

# --- メインループ ---
print("Starting sender... (Hold Ctrl+C to stop)")
time.sleep(1)

try:
    while True:
        # 1. パラメータをキー入力で更新
        event_type = update_parameters()
        
        # 2. 状態をコンソールに表示
        print_status(event_type)
        
        # 3. 送信するJSONデータを構築
        data = {
            "event": event_type,
            "temperature": current_temperature,
            "angle": current_angle
        }
        
        # 4. JSONをエンコードして送信
        message = json.dumps(data)
        sock.sendto(message.encode('utf-8'), server_address)
        
        # 5. 指定間隔待機
        time.sleep(SEND_INTERVAL)

except KeyboardInterrupt:
    print("\nSender stopped by user.")
except Exception as e:
    print(f"An error occurred: {e}")
finally:
    # 終了時に "no_contact" を送信
    message = json.dumps({"event": "no_contact", "temperature": 0, "angle": 0})
    sock.sendto(message.encode('utf-8'), server_address)
    sock.close()
    print("Socket closed.")
