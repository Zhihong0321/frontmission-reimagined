"""Batch client: generate every type of the given categories through the running server.
Usage: python generator/batch.py tree rock mountain [--n 1] [--bg chroma] [--quality low]
"""
import json
import sys
import threading
import urllib.request

args = []
opts = {"n": "1", "bg": "chroma", "quality": "low"}
argv = sys.argv[1:]
i = 0
while i < len(argv):
    if argv[i].startswith("--") and i + 1 < len(argv):
        opts[argv[i][2:]] = argv[i + 1]; i += 2
    else:
        args.append(argv[i]); i += 1
if not args:
    print(__doc__); sys.exit(1)

base = "http://127.0.0.1:5091"
def api(path, body=None):
    req = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json"}, method="POST" if body is not None else "GET")
    with urllib.request.urlopen(req, timeout=900) as r:
        return json.loads(r.read())

catalog = api("/api/catalog")
results = []
def one(cat, tid, subject):
    c = catalog["categories"][cat]
    try:
        r = api("/api/generate", {"category": cat, "type": tid, "subject": subject, "bg": opts["bg"], "quality": opts["quality"],
                                  "size": "1024x1024", "n": int(opts["n"]), "footprintKm": c["footprintKm"], "biomes": c["biomes"]})
        for item in r.get("results", []):
            results.append(item)
            print(("ERR  " if item.get("error") else "ok   ") + cat + "/" + tid + "  " + (item.get("error") or item.get("sprite", "")), flush=True)
    except Exception as e:
        print("FAIL " + cat + "/" + tid + "  " + str(e), flush=True)

def run_round(todo):
    threads = [threading.Thread(target=one, args=job) for job in todo]
    for t in threads: t.start()
    for t in threads: t.join()

todo = [(cat, tid, subject) for cat in args for tid, subject in catalog["categories"][cat]["types"].items()]
if opts.get("missing") == "1":  # --missing 1: skip types that already have an image
    have = {(e["category"], e["type"]) for e in api("/api/library")}
    todo = [j for j in todo if (j[0], j[1]) not in have]
    print(f"missing only: {len(todo)} types to generate", flush=True)
for rnd in range(1, 4):  # the proxy is flaky: re-run whatever failed, up to three rounds
    before = len(results)
    run_round(todo)
    got = {(r["category"], r["type"]) for r in results[before:] if not r.get("error")}
    todo = [j for j in todo if (j[0], j[1]) not in got]
    if not todo: break
    print(f"round {rnd}: {len(todo)} failed, retrying…", flush=True)
ok = sum(1 for r in results if not r.get("error"))
print(f"done: {ok} images, {len(todo)} types still failed")
