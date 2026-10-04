#!/usr/bin/env python3
"""Local front for a Soil dev server, so a test player can reach it like production.

Forwards every request to the Soil server (default http://127.0.0.1:8000), adding
`CF-IPCountry` (default IR) as Cloudflare does in production, which is how the server
decides which campaigns a player sees. A fault switch, re-read on every request, lets
a test break the ad server without rebuilding anything:

    echo pass  > <mode file>   # forward (default)
    echo fail  > <mode file>   # ad group selection answers 503
    echo slow  > <mode file>   # ad group selection waits 20 s first

    python3 proxy.py <port> <mode file> [upstream] [country]
"""
import http.server
import os
import sys
import time
import urllib.error
import urllib.request

PORT = int(sys.argv[1])
MODE_FILE = sys.argv[2]
UPSTREAM = sys.argv[3] if len(sys.argv) > 3 else "http://127.0.0.1:8000"
COUNTRY = sys.argv[4] if len(sys.argv) > 4 else "IR"
HOP_BY_HOP = {"connection", "keep-alive", "transfer-encoding", "te", "trailer", "upgrade", "host", "content-length"}


def mode():
    try:
        with open(MODE_FILE) as f:
            return f.read().strip() or "pass"
    except OSError:
        return "pass"


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _forward(self):
        body = self.rfile.read(int(self.headers.get("Content-Length") or 0)) or None
        selecting = "/adgroups/select" in self.path
        current = mode()
        if selecting and current == "fail":
            self._reply(503, b'{"detail":"injected failure"}')
            return
        if selecting and current == "slow":
            time.sleep(20)

        headers = {k: v for k, v in self.headers.items() if k.lower() not in HOP_BY_HOP}
        headers["CF-IPCountry"] = COUNTRY
        request = urllib.request.Request(UPSTREAM + self.path, data=body, headers=headers, method=self.command)
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                self._reply(response.status, response.read(), response.headers)
        except urllib.error.HTTPError as e:
            self._reply(e.code, e.read(), e.headers)
        except Exception as e:  # upstream down
            self._reply(502, str(e).encode())

    def _reply(self, status, payload, headers=None):
        self.send_response(status)
        for key, value in (headers.items() if headers else []):
            if key.lower() not in HOP_BY_HOP:
                self.send_header(key, value)
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    do_GET = do_POST = do_PUT = do_PATCH = do_DELETE = _forward

    def log_message(self, fmt, *args):
        sys.stderr.write(f"{time.strftime('%H:%M:%S')} [{mode()}] {fmt % args}\n")


http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
