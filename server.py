import datetime
import os
import socket
import threading
import time
import traceback
from dotenv import load_dotenv

load_dotenv()
API_KEY = os.getenv("API_KEY_VSEGPT")

TCP_IP = '0.0.0.0'
TCP_PORT = 6000
UDP_PORT = 6001
HEARTBEAT_TIMEOUT = 11  

clients = {} 
udp_map = {} 
lock = threading.Lock()

def broadcast_clients():
    with lock:
        client_list = [f"{steam_id}:{c['name']}" for steam_id, c in clients.items()]
        msg = "CLIENTS|" + ",".join(client_list)
        conns = [c['conn'] for c in clients.values()]
    for conn in conns:
        try:
            conn.send(msg.encode('utf-8'))
        except:
            pass

def remove_client(steam_id):
    with lock:
        if steam_id in clients:
            try:
                clients[steam_id]["conn"].shutdown(socket.SHUT_RDWR)
                clients[steam_id]["conn"].close()
            except:
                pass
            print(f"[INFO] Клиент {clients[steam_id]['name']} отключен.")
            clients.pop(steam_id)
    broadcast_clients()

def handle_tcp(conn, addr):
    print(f"[INFO] Новый TCP клиент: {addr}")
    steam_id = None

    try:
        while True:
            data = conn.recv(4096)
            if not data:
                break
            msg = data.decode('utf-8')

            if msg.startswith("INFO|"):
                parts = msg.split('|')
                if len(parts) >= 4:
                    steam_id = parts[1]
                    with lock:
                        clients[steam_id] = {
                            "conn": conn,
                            "name": parts[2],
                            "scene": parts[3],
                            "udp_addr": None,
                            "audio_buffer": b"",
                            "last_heartbeat": time.time()
                        }

                    print(f"[INFO] INFO от клиента: {parts[2]} ({parts[1]}) в сцене {parts[3]}")
                    broadcast_clients()  

            elif msg == "HEARTBEAT":
                if steam_id:
                    with lock:
                        clients[steam_id]["last_heartbeat"] = time.time()

            elif msg.startswith("UDP_REQUEST"):
                try:
                    conn.send(b"UDP_OK")
                except:
                    pass

            elif msg.startswith("UDP_INFO|"):
                parts = msg.split("|")
                if len(parts) >= 3:
                    steam_id = parts[1]
                    try:
                        udp_port = int(parts[2])
                    except:
                        continue

                    instance_id = parts[3] if len(parts) >= 4 else None

                    with lock:
                        if steam_id in clients:
                            client_ip = addr[0]
                            clients[steam_id]["udp_addr"] = (client_ip, udp_port)
                            clients[steam_id]["instance_id"] = instance_id
                            clients[steam_id]["udp_last_seen"] = time.time()
                            udp_map[(client_ip, udp_port)] = steam_id

                            log(f"[UDP] Привязка UDP: {steam_id}/{instance_id} → {(client_ip, udp_port)}")

            else:
                if steam_id:
                    with lock:
                        print(f"[TCP MSG] {clients[steam_id]['name']}: {msg}")
                    try:
                        conn.send(f"ACK|{msg}".encode('utf-8'))
                    except:
                        pass

    except ConnectionResetError:
        pass
    except Exception as e:
        tracebck = ''.join(traceback.format_exception(type(e), e, e.__traceback__))
        print(f"[TCP ERROR] {addr}:\n{tracebck}")
    finally:
        if steam_id:
            remove_client(steam_id)
        else:
            try:
                conn.close()
            except:
                pass

def heartbeat_check():
    while True:
        now = time.time()
        to_remove = []
        with lock:
            for steam_id, info in list(clients.items()):
                conn = info['conn']
                if conn is None:
                    if now - info["last_heartbeat"] > HEARTBEAT_TIMEOUT:
                        print(f"[INFO] Удаляем старый отвязанный клиент {info['name']}")
                        to_remove.append(steam_id)
                elif now - info["last_heartbeat"] > HEARTBEAT_TIMEOUT:
                    try:
                        conn.send(b"DISCONNECT|No heartbeat")
                    except:
                        pass
                    info["conn"] = None
                    info["last_heartbeat"] = now
        for sid in to_remove:
            clients.pop(sid)
            broadcast_clients()
        time.sleep(2)

def log(msg):
    print(f"[{datetime.datetime.now().strftime('%H:%M:%S.%f')[:-3]}] {msg}")

def handle_udp_packet(udp_sock, data, addr):
    with lock:
        steam_id = udp_map.get(addr)

        if not steam_id:
            candidates = [
                (sid, info) for sid, info in clients.items()
                if info.get("udp_addr") and info["udp_addr"][0] == addr[0]
            ]
            if candidates:
                candidates.sort(key=lambda x: x[1].get("udp_last_seen", 0), reverse=True)
                sid, info = candidates[0]
                if time.time() - info.get("udp_last_seen", 0) < 60:
                    steam_id = sid
                    old_addr = info["udp_addr"]
                    info["udp_addr"] = addr
                    info["udp_last_seen"] = time.time()
                    udp_map.pop(old_addr, None)
                    udp_map[addr] = steam_id
                    log(f"[UDP] (fallback) обновлена привязка {steam_id}/{info.get('instance_id')} → {addr}")

        if not steam_id:
            log(f"[UDP] ⚠ Не удалось определить steam_id для {addr}")
            return

        info = clients.get(steam_id)
        if not info:
            log(f"[UDP] internal: clients[{steam_id}] not found")
            return

        info["udp_last_seen"] = time.time()
        instance_id = info.get("instance_id")

    with lock:
        other_clients = [
            inf["udp_addr"]
            for sid, inf in clients.items()
            if inf.get("udp_addr") and sid != steam_id
        ]

    for other_addr in other_clients:
        try:
            udp_sock.sendto(data, other_addr)
        except Exception as e:
            log(f"[UDP ERROR] to {other_addr}: {e}")

    log(f"[UDP] {steam_id}/{instance_id}: {len(data)} bytes from {addr} → {len(other_clients)} clients")


def udp_thread():
    udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_sock.bind((TCP_IP, UDP_PORT))
    log(f"[UDP] Listening on {TCP_IP}:{UDP_PORT}")

    while True:
        try:
            data, addr = udp_sock.recvfrom(8192)
            handle_udp_packet(udp_sock, data, addr)
        except Exception as e:
            log(f"[UDP EXCEPTION] {type(e).__name__}: {e}")

def tcp_server():
    tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    tcp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    tcp_sock.bind((TCP_IP, TCP_PORT))
    tcp_sock.listen(10)
    print(f"[INFO] TCP сервер запущен на {TCP_IP}:{TCP_PORT}")

    while True:
        conn, addr = tcp_sock.accept()
        threading.Thread(target=handle_tcp, args=(conn, addr), daemon=True).start()


def main():
    threading.Thread(target=tcp_server, daemon=True).start()
    threading.Thread(target=udp_thread, daemon=True).start()
    threading.Thread(target=heartbeat_check, daemon=True).start()

    print("[INFO] Сервер запущен")
    while True:
        time.sleep(10)


if __name__ == "__main__":
    main()
