#!/usr/bin/env python3
"""Generate immutable Github Release assets and a Watchdog Manifest."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from zipfile import ZipFile, is_zipfile

COMMIT = re.compile(r"[0-9a-f]{40}\Z")
REPO = re.compile(r"[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+\Z")
CLIENT, SERVER = "SS14.Client.zip", "SS14.Server_linux-x64.zip"

def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def prepare(repo, commit, engine, directory):
    if not REPO.fullmatch(repo) or not COMMIT.fullmatch(commit) or not engine or len(engine)>100:
        raise ValueError("Invalid repository, commit or engine")
    root = Path(directory)
    c, s = root / CLIENT, root / SERVER
    if not all(x.is_file() and is_zipfile(x) for x in (c,s)):
        raise ValueError("Missing client or server ZIP")
    base = f"https://github.com/{repo}/releases/download/sectorhex-{commit[:12]}/"
    chash = digest(c)
    with ZipFile(s, "a") as z:
        if "build.json" in z.namelist():
            raise ValueError("Archive already contains build.json")
        z.writestr("build.json", json.dumps({
            "download":base+CLIENT, "hash":chash.upper(), "version":commit,
            "engine_version":engine, "fork_id":"sector-hex"
        }, separators=(",",":")))
    return {
        "time": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00","Z"),
        "client":{"url":base+CLIENT,"sha256":chash},
        "server":{"linux-x64":{"url":base+SERVER,"sha256":digest(s)}}
    }

def merge(commit, entry, previous=None):
    past = {"builds":{}} if previous is None else previous
    if not isinstance(past,dict) or not isinstance(past.get("builds"),dict):
        raise ValueError("Corrupt previous manifest")
    data = dict(past["builds"]);data[commit]=entry
    for key,val in data.items():
        if not COMMIT.fullmatch(key) or not isinstance(val,dict) or not isinstance(val.get("time"),str):
            raise ValueError("Unsafe previous manifest")
    ordered = sorted(data.items(),key=lambda v:(v[1]["time"],v[0]),reverse=True)
    return {"builds":dict(ordered[:12])}

def main():
    ap=argparse.ArgumentParser()
    for arg in ("repository","commit","engine-version","output"):
        ap.add_argument("--"+arg,required=True)
    ap.add_argument("--release-dir",default="release")
    ap.add_argument("--previous")
    a=ap.parse_args()
    prev=json.loads(Path(a.previous).read_text("utf8")) if a.previous else None
    output=merge(a.commit,prepare(a.repository,a.commit,a.engine_version,a.release_dir),prev)
    Path(a.output).write_text(json.dumps(output,indent=2)+"\n",encoding="utf8")
    print(f"Prepared {len(output['builds'])} version(s) for Watchdog")

if __name__=="__main__":
    main()
