"""Publish only the exact six assets produced and checked by this workflow."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
REPO = "g-kari/craftharbor"

def sha256(data):
    return hashlib.sha256(data).hexdigest()

def checked_files():
    request = json.loads((ROOT / "release.json").read_text(encoding="utf-8"))
    version = request["version"]
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[a-z0-9.-]+)?", version):
        raise RuntimeError("Invalid version")
    if type(request.get("prerelease")) is not bool:
        raise RuntimeError("prerelease must be an explicit boolean")
    source = os.environ["GITHUB_SHA"]
    if not re.fullmatch(r"[0-9a-f]{40}", source):
        raise RuntimeError("Invalid source commit")
    manifest = json.loads((ROOT / "dist/release-manifest.json").read_text(encoding="utf-8"))
    if manifest["source_commit"] != source or manifest["version"] != version:
        raise RuntimeError("Artifact does not belong to this workflow commit/version")
    archive = "CraftHarbor-" + version + "-windows-x64.zip"
    expected = {archive, archive + ".sha256", "setup.exe", "setup.exe.sha256",
                "uninstall.exe", "uninstall.exe.sha256"}
    entries = manifest["assets"]
    if len(entries) != 6 or {x["name"] for x in entries} != expected:
        raise RuntimeError("Unexpected asset allowlist")
    assets = {}
    for entry in entries:
        name = entry["name"]
        data = (ROOT / "dist" / name).read_bytes()
        if len(data) != entry["size"] or sha256(data) != entry["sha256"] or not data:
            raise RuntimeError("Artifact hash/size mismatch: " + name)
        assets[name] = {**entry, "data": data}
    for name, entry in assets.items():
        if not name.endswith(".sha256"):
            if assets[name + ".sha256"]["data"].decode("utf-8").split() != [entry["sha256"], name]:
                raise RuntimeError("SHA-256 sidecar mismatch: " + name)
    notes = (ROOT / "docs/releases" / ("v" + version + ".md")).read_text(encoding="utf-8")
    if not notes.strip():
        raise RuntimeError("Empty release notes")
    return request, source, assets, notes

def previous_version():
    old = subprocess.run(["git", "show", "HEAD^:release.json"], cwd=ROOT,
                         text=True, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    return json.loads(old.stdout).get("version") if old.returncode == 0 else None

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-only", action="store_true")
    options = parser.parse_args()
    request, source, assets, notes = checked_files()
    version = request["version"]
    tag = "v" + version
    if options.check_only:
        print("PASS: release publisher verified six exact artifact hashes, source commit and notes; no API writes.")
        return
    if (os.environ.get("GITHUB_REPOSITORY") != REPO or
        os.environ.get("GITHUB_EVENT_NAME") != "push" or
        os.environ.get("GITHUB_REF") != "refs/heads/main"):
        raise RuntimeError("Publishing is restricted to this repository's main-branch push")
    if previous_version() == version:
        print("Version unchanged; no release requested.")
        return
    token = os.environ["GH_TOKEN"]

    def api(method, path, body=None, raw=None, content_type="application/json", upload=False):
        host = "https://uploads.github.com" if upload else "https://api.github.com"
        data = raw if raw is not None else (json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None)
        req = urllib.request.Request(host + path, data=data, method=method, headers={
            "Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28", "Content-Type": content_type,
            "User-Agent": "CraftHarbor-release"})
        try:
            with urllib.request.urlopen(req, timeout=60) as response:
                result = response.read()
                return json.loads(result) if result else None
        except urllib.error.HTTPError as error:
            if method == "GET" and error.code == 404:
                return None
            raise RuntimeError("GitHub HTTP %s during %s %s" % (error.code, method, path)) from None

    base = "/repos/" + REPO
    existing_tag = api("GET", base + "/git/ref/tags/" + tag)
    if existing_tag and (existing_tag["object"]["type"] != "commit" or existing_tag["object"]["sha"] != source):
        raise RuntimeError("Existing tag points elsewhere; refusing to replace it")
    release = api("GET", base + "/releases/tags/" + tag)
    if release is None:
        # GitHub may omit unpublished drafts from the tag endpoint.
        release = next((x for x in api("GET", base + "/releases?per_page=100") if x["tag_name"] == tag), None)
    if release is not None and release["target_commitish"] != source:
        raise RuntimeError("Existing release has a different source; refusing to alter it")
    if release is None:
        release = api("POST", base + "/releases", {
            "tag_name": tag, "target_commitish": source,
            "name": "CraftHarbor " + tag + " — Harbor UI",
            "body": notes, "draft": True, "prerelease": request["prerelease"],
            "make_latest": "false"})
    release_path = base + "/releases/" + str(release["id"])
    existing_assets = api("GET", release_path + "/assets?per_page=100")
    if any(x["name"] not in assets for x in existing_assets):
        raise RuntimeError("Unexpected assets in existing release; review required")
    by_name = {x["name"]: x for x in existing_assets}
    for name, expected in assets.items():
        found = by_name.get(name)
        if found and found.get("digest") == "sha256:" + expected["sha256"] and found["size"] == expected["size"]:
            continue
        if not release["draft"]:
            raise RuntimeError("Published release differs; refusing to replace immutable downloads")
        if found:
            api("DELETE", base + "/releases/assets/" + str(found["id"]))
        kind = "application/zip" if name.endswith(".zip") else "application/octet-stream"
        uploaded = api("POST", release_path + "/assets?name=" + urllib.parse.quote(name),
                       raw=expected["data"], content_type=kind, upload=True)
        if uploaded.get("digest") != "sha256:" + expected["sha256"] or uploaded["size"] != expected["size"]:
            raise RuntimeError("GitHub upload digest mismatch: " + name)
    uploaded_assets = api("GET", release_path + "/assets?per_page=100")
    if len(uploaded_assets) != 6 or {x["name"] for x in uploaded_assets} != set(assets):
        raise RuntimeError("Release asset set is incomplete; leaving draft unpublished")
    for entry in uploaded_assets:
        if entry.get("digest") != "sha256:" + assets[entry["name"]]["sha256"]:
            raise RuntimeError("Final upload verification failed")
    if release["draft"]:
        release = api("PATCH", release_path, {"draft": False, "body": notes,
                      "prerelease": request["prerelease"], "make_latest": "false"})
    final_tag = api("GET", base + "/git/ref/tags/" + tag)
    if not final_tag or final_tag["object"]["sha"] != source:
        raise RuntimeError("Published tag verification failed")
    print("Published " + release["html_url"])
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as output:
            output.write("## Published release\n\n[" + tag + "](" + release["html_url"] + ")\n\n")
            output.write("Source: " + source + "\n\nAll six asset digests verified before publication.\n")

if __name__ == "__main__":
    main()
