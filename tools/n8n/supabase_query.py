"""Run a SQL query against Supabase/Postgres and print results as JSON.

Usage:
  python supabase_query.py <sql>
  python supabase_query.py "select * from ania_schema.a_jobs_core limit 5"
  python supabase_query.py                                          # diagnostic query

Requires: pg8000 (pip install pg8000)
Config: config/local/n8n/supabase_config.env  (SUPABASE_DB_URL=postgresql://...)
"""
import json
import ssl
import sys
from pathlib import Path
from urllib.parse import urlparse, unquote

import pg8000.native

env_path = Path("config/local/n8n/supabase_config.env")
env = env_path.read_text(encoding="utf-8-sig").strip()
url = env.split("=", 1)[1] if env.startswith("SUPABASE_DB_URL=") else env

u = urlparse(url)
ctx = ssl._create_unverified_context()

con = pg8000.native.Connection(
    user=unquote(u.username or ""),
    password=unquote(u.password or ""),
    host=u.hostname or "",
    port=u.port or 5432,
    database=(u.path or "/postgres").lstrip("/") or "postgres",
    ssl_context=ctx,
    timeout=30,
)

sql = " ".join(sys.argv[1:]).strip()
if not sql:
    sql = (
        "select current_database() as db, current_schema() as schema, "
        "now()::text as ts"
    )

rows = con.run(sql)
print(json.dumps(rows, ensure_ascii=False, default=str))
con.close()
