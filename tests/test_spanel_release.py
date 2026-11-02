"""Offline release-generation checks."""
import importlib.util
import json
from pathlib import Path
from zipfile import ZipFile
import pytest
spec=importlib.util.spec_from_file_location("spanel_release",Path(__file__).resolve().parents[1]/"Tools"/"spanel_release.py")
m=importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)
SHA="a"*40

def archives(tmp):
    for name in (m.CLIENT,m.SERVER):
        with ZipFile(tmp/name,"w") as z: z.writestr("test.txt","test")

def test_hashes_and_client_metadata(tmp_path):
    archives(tmp_path)
    x=m.prepare("darvin7531/Sector-Hex",SHA,"1.0",tmp_path)
    with ZipFile(tmp_path/m.SERVER) as z: metadata=json.loads(z.read("build.json"))
    assert metadata["fork_id"]=="sector-hex"
    assert metadata["hash"].lower()==x["client"]["sha256"]
    assert x["server"]["linux-x64"]["sha256"]==m.digest(tmp_path/m.SERVER)

def test_repeated_publish_forbidden(tmp_path):
    archives(tmp_path); m.prepare("darvin7531/Sector-Hex",SHA,"1.0",tmp_path)
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex",SHA,"1.0",tmp_path)

def test_commit_must_be_immutable(tmp_path):
    archives(tmp_path)
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex","main","1.0",tmp_path)

def test_history_and_invalid_input():
    old={"builds":{f"{i:040x}":{"time":f"2026-10-{i:02d}T00:00:00Z"} for i in range(1,14)}}
    result=m.merge(SHA,{"time":"2026-11-03T00:00:00Z"},old)
    assert len(result["builds"])==12 and SHA in result["builds"]
    with pytest.raises(ValueError):m.merge(SHA,{"time":"ok"},{"builds":{"unknown":{}}})
