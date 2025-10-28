import socket
import threading
import time

clients = {}  # conn: {"steam_id": ..., "name": ..., "scene": ..., "last_heartbeat": ...}
server_ip = '0.0.0.0'
server_port = 6000
HEARTBEAT_TIMEOUT = 11  

def broadcast_clients():
    client_strings = [f"{info['steam_id']}:{info['name']}" for info in clients.values() if info['steam_id']]
    msg = "CLIENTS|" + ",".join(client_strings)
    for c in clients.keys():
        try:
            c.send(msg.encode('utf-8'))
        except:
            pass

def client_thread(conn, addr):
    print(f"Новый клиент: {addr}")
    clients[conn] = {"steam_id": "", "name": str(addr), "scene": "", "last_heartbeat": time.time()}
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
                    clients[conn]["steam_id"] = parts[1]
                    clients[conn]["name"] = parts[2]
                    clients[conn]["scene"] = parts[3]
                    print(f"INFO от клиента: {parts[2]} ({parts[1]}) в сцене {parts[3]}")
                    broadcast_clients() 
            elif msg == "HEARTBEAT":
                clients[conn]["last_heartbeat"] = time.time()
            else:
                print(f"Сообщение от {clients[conn]['name']}: {msg}")

            conn.send(f"ACK|{msg}".encode('utf-8'))

    except ConnectionResetError:
        pass
    finally:
        print(f"Клиент отключился: {addr}")
        clients.pop(conn, None)
        conn.close()
        broadcast_clients()  

def heartbeat_check():
    while True:
        now = time.time()
        to_remove = []
        for c, info in clients.items():
            if now - info["last_heartbeat"] > HEARTBEAT_TIMEOUT:
                try:
                    c.send(f"DISCONNECT|No heartbeat received".encode('utf-8'))
                except:
                    pass
                print(f"Клиент {info['name']} отключен: отсутствие heartbeat > {HEARTBEAT_TIMEOUT}s")
                to_remove.append(c)
        for c in to_remove:
            c.close()
            clients.pop(c, None)
            broadcast_clients() 
        time.sleep(2)  

def main():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind((server_ip, server_port))
    s.listen(10)
    print(f"TCP сервер запущен на {server_ip}:{server_port}")

    threading.Thread(target=heartbeat_check, daemon=True).start()

    while True:
        conn, addr = s.accept()
        threading.Thread(target=client_thread, args=(conn, addr), daemon=True).start()

if __name__ == "__main__":
    main()
