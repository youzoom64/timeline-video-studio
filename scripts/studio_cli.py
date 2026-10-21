"""Control a running Timeline Video Studio without touching its GUI.

Examples:
  python scripts/studio_cli.py state
  python scripts/studio_cli.py command command.json
  python scripts/studio_cli.py frame preview.png
"""
import json
import argparse
import os
import sys
import urllib.request
from pathlib import Path

BASE = os.environ.get("TIMELINE_STUDIO_API_URL", "http://127.0.0.1:" + os.environ.get("TIMELINE_STUDIO_API_PORT", "18764"))


def request(path: str, data: bytes | None = None) -> bytes:
    req = urllib.request.Request(
        BASE + path,
        data=data,
        headers={"Content-Type": "application/json; charset=utf-8"} if data else {},
        method="POST" if data else "GET",
    )
    with urllib.request.urlopen(req, timeout=65) as response:
        return response.read()


def main() -> None:
    global BASE
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--api',default=BASE)
    parser.add_argument('action',choices=('state','command','frame'))
    parser.add_argument('file',nargs='?')
    args=parser.parse_args();BASE=args.api.rstrip('/')
    if args.action!='state' and not args.file:parser.error('command/frame requires a file path')
    if args.action == "state":
        print(json.dumps(json.loads(request("/state")), ensure_ascii=False, indent=2))
    elif args.action == "command":
        payload = json.loads(Path(args.file).read_text(encoding="utf-8"))
        print(json.dumps(json.loads(request("/command", json.dumps(payload, ensure_ascii=False).encode("utf-8"))), ensure_ascii=False, indent=2))
    elif args.action == "frame":
        Path(args.file).write_bytes(request("/frame"))
        print(str(Path(args.file).resolve()))
    else:
        raise SystemExit("Unknown action")


if __name__ == "__main__":
    main()
