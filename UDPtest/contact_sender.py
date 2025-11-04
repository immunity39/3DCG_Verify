import socket
import json
import time

UDP_IP = "192.168.1.100"   # Unity を動かすPCのIP（または 127.0.0.1）
UDP_PORT = 5005

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

def send_contact_event(x,y,z, contact_ms, tip_temp_c, event_type="update"):
    msg = {
        "type": event_type,   # "start", "update", "end"
        "pos": [x,y,z],
        "contact_ms": contact_ms,   # ms
        "tip_temp_c": tip_temp_c,
        "timestamp": time.time()
    }
    sock.sendto(json.dumps(msg).encode('utf-8'), (UDP_IP, UDP_PORT))

# Example: simulate contact at world pos (0.1, 0.02, 0.05)
if __name__ == "__main__":
    # start
    send_contact_event(0.1, 0.02, 0.05, 0, 350.0, event_type="start")
    # hold for 250 ms
    time.sleep(0.25)
    send_contact_event(0.1, 0.02, 0.05, 250, 350.0, event_type="update")
    # end
    send_contact_event(0.1, 0.02, 0.05, 250, 350.0, event_type="end")
