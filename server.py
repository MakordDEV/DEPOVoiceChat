import datetime
import faulthandler
import os
import socket
import sys
import threading
import time
import traceback
import wave
import requests
from dotenv import load_dotenv

load_dotenv()
API_KEY = os.getenv("API_KEY_VSEGPT")

TCP_IP = '0.0.0.0'
TCP_PORT = 6000
UDP_PORT = 6001
HEARTBEAT_TIMEOUT = 11  

clients = {}  # {steam_id: {"conn": conn, "name": str, "scene": str, "udp_addr": (ip, port), "audio_buffer": bytes, "stt_timer": Timer, "last_heartbeat": float}}
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
                            "stt_timer": None,
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
                if len(parts) == 3:
                    steam_id = parts[1]
                    udp_port = int(parts[2])
                    with lock:
                        if steam_id in clients:
                            clients[steam_id]["udp_addr"] = (addr[0], udp_port)
                            log(f"[UDP] Привязка UDP: {steam_id} → {(addr[0], udp_port)}")

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

def trigger_stt(steam_id):
    start_time = time.time()
    log(f"[STT] Triggered for {steam_id}")

    with lock:
        info = clients.get(steam_id)
        if not info:
            log(f"[STT] {steam_id}: client info not found")
            return
        audio_data = info.get("audio_buffer", b"")
        info["audio_buffer"] = b""  # сбрасываем буфер после чтения
        info["stt_timer"] = None

    if not audio_data:
        log(f"[STT] {steam_id}: no audio data — skipping STT")
        return

    log(f"[STT] {steam_id}: collected {len(audio_data)} bytes ({len(audio_data)/96000:.2f}s)")

    filename = f"{steam_id}_{int(time.time())}.wav"
    try:
        with wave.open(filename, "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(48000)
            wf.writeframes(audio_data)
        log(f"[STT] {steam_id}: WAV file saved as {filename}")
    except Exception as e:
        log(f"[STT ERROR] {steam_id}: write failed: {e}")
        return

    files = {"file": open(filename, "rb")}
    data = {"model": "stt-openai/whisper-v3-turbo", "response_format": "json"}
    headers = {"Authorization": f"Bearer {API_KEY}"}

    try:
        log(f"[STT] {steam_id}: sending {len(audio_data)} bytes to STT API...")
        resp = requests.post("https://api.vsegpt.ru/v1/audio/transcriptions",
                             headers=headers, files=files, data=data, timeout=20)
        resp_json = resp.json()
        log(f"[STT] {steam_id}: response: {resp_json}")
    except Exception as e:
        log(f"[STT ERROR] {steam_id}: STT request failed: {e}")
    finally:
        files["file"].close()
        try:
            os.remove(filename)
            log(f"[STT] {steam_id}: temp file deleted ({time.time() - start_time:.2f}s total)")
        except Exception as e:
            log(f"[STT ERROR] {steam_id}: delete failed: {e}")


def udp_thread():
    udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp_sock.bind((TCP_IP, UDP_PORT))
    log(f"[UDP] Listening on {TCP_IP}:{UDP_PORT}")

    while True:
        try:
            data, addr = udp_sock.recvfrom(4096)
            packet_len = len(data)
            recv_time = time.time()
            steam_id = None
            other_clients = []

            with lock:
                steam_id = None
                for sid, info in clients.items():
                    if info["udp_addr"] == addr:
                        steam_id = sid
                        break

            if not steam_id:
                log(f"[UDP] ⚠ Не удалось определить steam_id для {addr}")
                continue  

            if steam_id:
                info = clients[steam_id]

                if info.get("stt_timer"):
                    info["stt_timer"].cancel()
                    info["stt_timer"] = None

                if len(info["audio_buffer"]) == 0:
                    log(f"[UDP] {steam_id}: start new capture session")

                info["audio_buffer"] += data
                cur_len = len(info["audio_buffer"])
                log(f"[UDP] {steam_id}: +{packet_len} bytes (total={cur_len}, from={addr})")

                t = threading.Timer(2.0, trigger_stt, args=(steam_id,))
                info["stt_timer"] = t
                t.daemon = True
                t.start()

                other_clients = [
                    inf["udp_addr"] for sid, inf in clients.items()
                    if inf.get("udp_addr") and inf["udp_addr"] != addr
                ]

            for other_addr in other_clients:
                try:
                    udp_sock.sendto(data, other_addr)
                    log(f"[UDP→] {packet_len} bytes from {addr} → {other_addr}")
                except Exception as e:
                    log(f"[UDP ERROR] to {other_addr}: {e}")

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
