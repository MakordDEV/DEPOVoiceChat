import socket
import threading
import time

TCP_IP = '0.0.0.0'
TCP_PORT = 6000
UDP_PORT = 6001
HEARTBEAT_TIMEOUT = 11  

clients = {}  # {steam_id, name, scene, udp_addr, last_heartbeat}

lock = threading.Lock()

def broadcast_clients():
    with lock:
        client_list = [f"{c['steam_id']}:{c['name']}" for c in clients.values() if c['steam_id']]
        msg = "CLIENTS|" + ",".join(client_list)
        for conn in clients.keys():
            try:
                conn.send(msg.encode('utf-8'))
            except:
                pass

def remove_client(conn):
    with lock:
        if conn in clients:
            print(f"[INFO] Клиент {clients[conn]['name']} отключен.")
            clients.pop(conn)
            conn.close()
            broadcast_clients()

def handle_tcp(conn, addr):
    print(f"[INFO] Новый TCP клиент: {addr}")
    with lock:
        clients[conn] = {"steam_id":"", "name":str(addr), "scene":"", "udp_addr":None, "last_heartbeat":time.time()}

    broadcast_clients()

    try:
        while True:
            data = conn.recv(4096)
            if not data:
                break
            msg = data.decode('utf-8')

            if msg.startswith("INFO|"):
                parts = msg.split('|')
                if len(parts) >= 4:
                    with lock:
                        clients[conn]["steam_id"] = parts[1]
                        clients[conn]["name"] = parts[2]
                        clients[conn]["scene"] = parts[3]
                    print(f"[INFO] INFO от клиента: {parts[2]} ({parts[1]}) в сцене {parts[3]}")
                    broadcast_clients()

            elif msg == "HEARTBEAT":
                with lock:
                    if conn in clients:
                        clients[conn]["last_heartbeat"] = time.time()

            elif msg.startswith("UDP_REQUEST"):
                try:
                    conn.send(b"UDP_OK")
                except:
                    pass

            else:
                print(f"[TCP MSG] {clients[conn]['name']}: {msg}")
                try:
                    conn.send(f"ACK|{msg}".encode('utf-8'))
                except:
                    pass

    except ConnectionResetError:
        pass
    finally:
        remove_client(conn)

def heartbeat_check():
    while True:
        now = time.time()
        to_remove = []
        with lock:
            for conn, info in clients.items():
                if now - info["last_heartbeat"] > HEARTBEAT_TIMEOUT:
                    try:
                        conn.send(b"DISCONNECT|No heartbeat")
                        conn.close()
                    except:
                        pass
                    to_remove.append(conn)
        for conn in to_remove:
            remove_client(conn)
        time.sleep(2)

def udp_thread():
    udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_sock.bind((TCP_IP, UDP_PORT))
    print(f"[INFO] UDP сервер запущен на {TCP_IP}:{UDP_PORT}")

    while True:
        try:
            data, addr = udp_sock.recvfrom(4096)
            sender_conn = None
            with lock:
                for conn, info in clients.items():
                    if info["udp_addr"] is None:
                        info["udp_addr"] = addr
                    if info["udp_addr"] == addr:
                        sender_conn = conn
                        break

            with lock:
                for conn, info in clients.items():
                    if info["udp_addr"] and info["udp_addr"] != addr:
                        try:
                            udp_sock.sendto(data, info["udp_addr"])
                        except:
                            pass
        except Exception as e:
            print(f"[ERROR] UDP: {e}")

def tcp_server():
    tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
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
