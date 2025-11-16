import datetime
import socket
import threading
import time
import traceback
import queue
from concurrent.futures import ThreadPoolExecutor

TCP_IP = '0.0.0.0'
TCP_PORT = 6000
UDP_PORT = 6001
HEARTBEAT_TIMEOUT = 11

class Client:
    def __init__(self, steam_id, conn, name, scene):
        self.steam_id = steam_id
        self.conn = conn
        self.name = name
        self.scene = scene
        self.udp_addr = None
        self.instance_id = None
        self.udp_last_seen = 0.0
        self.last_heartbeat = time.time()
        self.lock = threading.Lock()

clients = {}
udp_map = {}
clients_lock = threading.Lock()
udp_socket = None

udp_send_queue = queue.Queue()
udp_executor = ThreadPoolExecutor(max_workers=4) 

def log(msg):
    print(f"[{datetime.datetime.now().strftime('%H:%M:%S.%f')[:-3]}] {msg}")

def safe_send_tcp(conn, data):
    try:
        conn.send(data)
        return True
    except Exception:
        return False

def broadcast_clients():
    to_remove = []
    with clients_lock:
        client_list = [f"{sid}:{c.name}" for sid, c in clients.items()]
        msg = "CLIENTS|" + ",".join(client_list)
        snapshot = [(sid, c.conn) for sid, c in clients.items()]
    data = msg.encode('utf-8')
    for sid, conn in snapshot:
        if conn is None:
            to_remove.append(sid)
            continue
        ok = safe_send_tcp(conn, data)
        if not ok:
            to_remove.append(sid)
    for sid in set(to_remove):
        remove_client(sid, announce=False)
    if to_remove:
        with clients_lock:
            client_list = [f"{sid}:{c.name}" for sid, c in clients.items()]
            msg = "CLIENTS|" + ",".join(client_list)
            snapshot = [(sid, c.conn) for sid, c in clients.items()]
        data = msg.encode('utf-8')
        for _, conn in snapshot:
            if conn:
                try:
                    conn.send(data)
                except:
                    pass

def remove_client(steam_id, announce=True):
    with clients_lock:
        info = clients.get(steam_id)
        if not info:
            return
        conn = info.conn
        name = info.name
        udp_addr = info.udp_addr
        try:
            if conn:
                try:
                    conn.shutdown(socket.SHUT_RDWR)
                except:
                    pass
                try:
                    conn.close()
                except:
                    pass
        except Exception:
            pass
        if udp_addr:
            udp_map.pop(udp_addr, None)
        clients.pop(steam_id, None)
    log(f"[INFO] Клиент {name} ({steam_id}) отключен.")
    if announce:
        broadcast_clients()

def handle_tcp(conn, addr):
    log(f"[INFO] Новый TCP клиент: {addr}")
    steam_id = None
    conn.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
    try:
        while True:
            try:
                data = conn.recv(4096)
            except OSError:
                break
            if not data:
                break
            try:
                msg = data.decode('utf-8', errors='ignore')
            except:
                continue
            if msg.startswith("INFO|"):
                parts = msg.split('|')
                if len(parts) >= 4:
                    steam_id = parts[1]
                    name = parts[2]
                    scene = parts[3]
                    with clients_lock:
                        existing = clients.get(steam_id)
                        if existing:
                            try:
                                if existing.conn and existing.conn != conn:
                                    try:
                                        existing.conn.shutdown(socket.SHUT_RDWR)
                                    except:
                                        pass
                                    try:
                                        existing.conn.close()
                                    except:
                                        pass
                            except Exception:
                                pass
                        client = Client(steam_id, conn, name, scene)
                        prev = clients.get(steam_id)
                        if prev:
                            client.udp_addr = prev.udp_addr
                            client.instance_id = prev.instance_id
                            client.udp_last_seen = prev.udp_last_seen
                        clients[steam_id] = client
                    log(f"[INFO] INFO от клиента: {name} ({steam_id}) в сцене {scene}")
                    broadcast_clients()
            elif msg == "HEARTBEAT":
                if steam_id:
                    with clients_lock:
                        c = clients.get(steam_id)
                        if c:
                            c.last_heartbeat = time.time()
            elif msg.startswith("SPEAK_REQUEST"):
                try:
                    conn.send(b"SPEAK_OK")
                except:
                    pass
                with clients_lock:
                    scene = clients.get(steam_id).scene if clients.get(steam_id) else None
                    name = clients.get(steam_id).name if clients.get(steam_id) else None
                    snapshot = [(sid, c.conn) for sid, c in clients.items()]
                if scene is None:
                    continue
                msg_ = f"SPEAKING|{scene}|{name}|{steam_id}".encode('utf-8')
                for sid, other_conn in snapshot:
                    if sid == steam_id:
                        continue
                    if other_conn:
                        ok = safe_send_tcp(other_conn, msg_)
                        if not ok:
                            remove_client(sid, announce=False)
                broadcast_clients()
            elif msg.startswith("UDP_INFO|"):
                parts = msg.split("|")
                if len(parts) >= 3:
                    sid = parts[1]
                    try:
                        udp_port = int(parts[2])
                    except:
                        continue
                    instance_id = parts[3] if len(parts) >= 4 else None
                    client_ip = addr[0]
                    with clients_lock:
                        c = clients.get(sid)
                        if not c:
                            continue
                        old_addr = c.udp_addr
                        new_addr = (client_ip, udp_port)
                        c.udp_addr = new_addr
                        c.instance_id = instance_id
                        c.udp_last_seen = time.time()
                        udp_map.pop(old_addr, None)
                        udp_map[new_addr] = sid
                    log(f"[UDP] Привязка UDP: {sid}/{instance_id} → {new_addr}")
                    with clients_lock:
                        targets = [info.udp_addr for idd, info in clients.items() if idd != sid and info.udp_addr]
                    for target in targets:
                        try:
                            global udp_socket
                            if udp_socket:
                                udp_socket.sendto(b"", target)
                            else:
                                s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                                s.sendto(b"", target)
                                s.close()
                        except Exception as e:
                            log(f"[UDP ERROR] sending init packet to {target}: {e}")
            else:
                if steam_id:
                    with clients_lock:
                        c = clients.get(steam_id)
                        name = c.name if c else "unknown"
                    log(f"[TCP MSG] {name}: {msg}")
                    try:
                        conn.send(f"ACK|{msg}".encode('utf-8'))
                    except:
                        pass
    except Exception as e:
        tracebck = ''.join(traceback.format_exception(type(e), e, e.__traceback__))
        log(f"[TCP ERROR] {addr}:\n{tracebck}")
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
        with clients_lock:
            for steam_id, info in list(clients.items()):
                if now - info.last_heartbeat > HEARTBEAT_TIMEOUT:
                    to_remove.append(steam_id)
        for sid in to_remove:
            log(f"[INFO] Heartbeat timeout for {sid}")
            remove_client(sid)
        time.sleep(2)

def handle_udp_packet(udp_sock, data, addr):
    steam_id = None
    with clients_lock:
        steam_id = udp_map.get(addr)
        if not steam_id:
            candidates = [
                (sid, info) for sid, info in clients.items()
                if info.udp_addr and info.udp_addr[0] == addr[0]
            ]
            if candidates:
                candidates.sort(key=lambda x: x[1].udp_last_seen or 0, reverse=True)
                sid, info = candidates[0]
                if time.time() - (info.udp_last_seen or 0) < 60:
                    steam_id = sid
                    old_addr = info.udp_addr
                    info.udp_addr = addr
                    info.udp_last_seen = time.time()
                    udp_map.pop(old_addr, None)
                    udp_map[addr] = steam_id
                    log(f"[UDP] (fallback) обновлена привязка {steam_id}/{info.instance_id} → {addr}")
    if not steam_id:
        log(f"[UDP] ⚠ Не удалось определить steam_id для {addr}")
        return

    with clients_lock:
        info = clients.get(steam_id)
        if not info:
            log(f"[UDP] internal: clients[{steam_id}] not found")
            return
        info.udp_last_seen = time.time()
        instance_id = info.instance_id
        other_clients = [inf.udp_addr for sid, inf in clients.items() if inf.udp_addr and sid != steam_id]

    for other_addr in other_clients:
        udp_send_queue.put((data, other_addr))

    log(f"[UDP] {steam_id}/{instance_id}: {len(data)} bytes from {addr} → {len(other_clients)} clients")

def udp_sender_loop(udp_sock):
    while True:
        try:
            data, target = udp_send_queue.get()
            if data is None:
                break
            try:
                udp_sock.sendto(data, target)
            except Exception as e:
                log(f"[UDP ERROR] to {target}: {e}")
        except Exception as e:
            log(f"[UDP SENDER EXCEPTION] {type(e).__name__}: {e}")

def udp_thread():
    global udp_socket
    udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_socket.bind((TCP_IP, UDP_PORT))
    udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 262144)
    udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 262144)
    log(f"[UDP] Listening on {TCP_IP}:{UDP_PORT}")

    for _ in range(4):
        udp_executor.submit(udp_sender_loop, udp_socket)

    while True:
        try:
            data, addr = udp_socket.recvfrom(8192)
            handle_udp_packet(udp_socket, data, addr)
        except Exception as e:
            log(f"[UDP EXCEPTION] {type(e).__name__}: {e}")

def tcp_server():
    tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    tcp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    tcp_sock.bind((TCP_IP, TCP_PORT))
    tcp_sock.listen(100)
    log(f"[INFO] TCP сервер запущен на {TCP_IP}:{TCP_PORT}")
    while True:
        try:
            conn, addr = tcp_sock.accept()
            t = threading.Thread(target=handle_tcp, args=(conn, addr), daemon=True)
            t.start()
        except Exception as e:
            log(f"[TCP ACCEPT ERROR] {e}")

def main():
    threading.Thread(target=udp_thread, daemon=True).start()
    threading.Thread(target=tcp_server, daemon=True).start()
    threading.Thread(target=heartbeat_check, daemon=True).start()
    log("[INFO] Сервер запущен")
    while True:
        time.sleep(10)

if __name__ == "__main__":
    main()
