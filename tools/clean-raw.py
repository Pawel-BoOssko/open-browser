"""Clean a raw NDJSON file: decode SSE body, make readable.

Usage:
  python tools\clean-raw.py <input.ndjson> [output.txt]
"""
import json
import re
import sys
from pathlib import Path

src = Path(sys.argv[1])
dst = Path(sys.argv[2]) if len(sys.argv) > 2 else src.with_suffix(".clean.txt")

with open(src, "r", encoding="utf-8-sig") as f:
    lines = f.readlines()

out = []
for line in lines:
    line = line.strip()
    if not line:
        continue
    try:
        record = json.loads(line)
    except json.JSONDecodeError:
        out.append(line)
        continue

    raw = record.get("raw", "")
    if not raw:
        out.append(json.dumps(record, ensure_ascii=False))
        continue

    # Decode CDP body: {"base64Encoded":false,"body":"<SSE data>"}
    body = raw
    try:
        cdp = json.loads(raw) if isinstance(raw, str) else raw
        if isinstance(cdp, dict):
            body = cdp.get("body", raw)
            if cdp.get("base64Encoded"):
                import base64
                body = base64.b64decode(body).decode("utf-8", errors="replace")
    except Exception:
        body = raw

    # Unescape JSON inside body (SSE data lines)
    if isinstance(body, str):
        body = body.replace("\\n", "\n").replace("\\t", "\t")
        body = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), body)
        body = body.replace('\\"', '"').replace("\\\\", "\\")

    record["raw"] = body
    out.append(json.dumps(record, ensure_ascii=False))

dst.write_text("\n".join(out), encoding="utf-8")
print(f"Cleaned: {dst} ({len(out)} records)")
