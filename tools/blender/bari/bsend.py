# Send a Python file to the running Blender MCP addon (port 9877) and print the result.
import socket, json, sys
code = open(sys.argv[1], encoding="utf-8").read()
cmd = {"type": "execute_code", "params": {"code": code}}
if len(sys.argv) > 2 and sys.argv[2] == "--shot":
    cmd = {"type": "get_viewport_screenshot", "params": {"filepath": sys.argv[1].replace(".py", ".png"), "max_size": 1400, "format": "png"}}
s = socket.create_connection(("127.0.0.1", 9877), timeout=600)
s.sendall(json.dumps(cmd).encode())
buf = b""
while True:
    chunk = s.recv(65536)
    if not chunk: break
    buf += chunk
    try: json.loads(buf); break
    except Exception: continue
print(buf.decode()[:3000])
