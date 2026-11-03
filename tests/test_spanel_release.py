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
ENGINE="b"*40

def archives(tmp):
    for name in (m.CLIENT,m.SERVER):
        with ZipFile(tmp/name,"w") as z: z.writestr("test.txt","test")

def test_hashes_and_client_metadata(tmp_path):
    archives(tmp_path)
    x=m.prepare("darvin7531/Sector-Hex",SHA,"277.0.0",tmp_path,ENGINE)
    with ZipFile(tmp_path/m.SERVER) as z: metadata=json.loads(z.read("build.json"))
    assert metadata["fork_id"]=="sector-hex"
    assert metadata["engine_version"]=="277.0.0"
    assert metadata["version"]==SHA
    assert metadata["download"]==x["client"]["url"]
    assert x["engine_revision"]==ENGINE and x["content_revision"]==SHA
    assert metadata["hash"].lower()==x["client"]["sha256"]
    assert x["server"]["linux-x64"]["sha256"]==m.digest(tmp_path/m.SERVER)

def test_repeated_publish_forbidden(tmp_path):
    archives(tmp_path); m.prepare("darvin7531/Sector-Hex",SHA,"277.0.0",tmp_path,ENGINE)
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex",SHA,"277.0.0",tmp_path,ENGINE)

def test_commit_must_be_immutable(tmp_path):
    archives(tmp_path)
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex","main","277.0.0",tmp_path,ENGINE)

def test_history_and_invalid_input():
    old={"builds":{f"{i:040x}":{"time":f"2026-10-{i:02d}T00:00:00Z"} for i in range(1,14)}}
    result=m.merge(SHA,{"time":"2026-11-03T00:00:00Z"},old)
    assert len(result["builds"])==12 and SHA in result["builds"]
    with pytest.raises(ValueError):m.merge(SHA,{"time":"ok"},{"builds":{"unknown":{}}})

def test_previous_manifest_roundtrip_and_duplicate_rejected(tmp_path):
    archives(tmp_path)
    old_sha="c"*40
    old_entry={"time":"2026-01-01T00:00:00Z","client":{"url":"old","sha256":"d"*64}}
    previous=json.loads(json.dumps({"builds":{old_sha:old_entry}}))
    result=m.merge(SHA,m.prepare("darvin7531/Sector-Hex",SHA,"277.0.0",tmp_path,ENGINE),previous)
    assert result["builds"][old_sha]==old_entry
    assert list(result["builds"])[0]==SHA
    assert previous=={"builds":{old_sha:old_entry}}
    with pytest.raises(ValueError):m.merge(SHA,result["builds"][SHA],result)

def test_engine_must_be_pinned(tmp_path):
    archives(tmp_path)
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex",SHA,"277.0.0",tmp_path,"master")
    with pytest.raises(ValueError):m.prepare("darvin7531/Sector-Hex",SHA,"v277.0.0",tmp_path,ENGINE)