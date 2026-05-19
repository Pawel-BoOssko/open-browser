"""Clean a raw NDJSON file: unescape Unicode, remove double-escaping, make readable.

Usage:
  python tools\clean-raw.py <input.ndjson> [output.txt]
  python tools\clean-raw.py extracted\run_X_msg_Y_raw.ndjson clean.txt
"""
import sys
from pathlib import Path

src = Path(sys.argv[1])
dst = Path(sys.argv[2]) if len(sys.argv) > 2 else src.with_suffix(".clean.txt")

text = src.read_text(encoding="utf-8-sig")

# Three passes for readability
text = text.replace("\\u0022", '"')
text = text.replace("\\n", "\n")
text = text.replace("\\\\", "")

dst.write_text(text, encoding="utf-8")
print(f"Cleaned: {dst} ({len(text)} chars)")
