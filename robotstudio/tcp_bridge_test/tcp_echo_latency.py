"""TCP echo server for RAPID-side latency measurement.

Run BEFORE starting the RAPID program:
    python tcp_echo_latency.py

Listens on 0.0.0.0:7000, accepts one client (the VC's RAPID SocketConnect),
echoes every frame back verbatim, prints local timing stats.

Pair with MetaMoveCoreTCPLatencyTest.mod which sends 200 frames.
"""
import socket
import time

HOST = "0.0.0.0"
PORT = 7000
FRAMES_EXPECTED = 200


def run():
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind((HOST, PORT))
    srv.listen(1)
    print(f"[echo] listening on {HOST}:{PORT}")
    print("[echo] start the RAPID MetaMoveCoreTCPLatencyTest now")

    conn, addr = srv.accept()
    print(f"[echo] client connected from {addr}")
    conn.settimeout(5.0)

    samples = []
    n = 0
    try:
        while True:
            recv_t = time.perf_counter()
            data = conn.recv(64)
            if not data:
                print("[echo] client closed")
                break
            conn.sendall(data)
            send_t = time.perf_counter()
            samples.append((send_t - recv_t) * 1000.0)
            n += 1
            if n % 50 == 0:
                print(f"[echo] {n} frames processed")
    except socket.timeout:
        print("[echo] recv timeout — assuming end of test")
    except Exception as e:
        print(f"[echo] error: {e}")
    finally:
        conn.close()
        srv.close()

    if samples:
        avg = sum(samples) / len(samples)
        print(
            f"[echo] server-side handling: n={len(samples)} avg={avg:.3f}ms "
            f"min={min(samples):.3f}ms max={max(samples):.3f}ms"
        )
        print("[echo] (RAPID-side roundtrip = above + 2x network leg)")


if __name__ == "__main__":
    run()
