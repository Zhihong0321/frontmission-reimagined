"""Map asset generator — a small local app around GPT-image-2.

Run:  python generator/server.py      (from D:\\FrontMission-MapLab)   then open http://127.0.0.1:5091

What it does
  * assembles prompts from a locked style block + a subject line (catalog.json)
  * calls GPT-image-2 through the proxy in the vault (GPT-IMAGE-2_KEY), up to 8 in parallel
  * post-processes every result into a clean top-down sprite: chroma key (or the API's own
    alpha), despill, trim, square canvas, 256 px sprite
  * keeps a library under art/gen/<category>/ with a JSON sidecar per image
  * writes art/manifest.json + art/manifest.js listing the APPROVED sprites, which the map
    demo loads on start
"""
import base64
import io
import json
import os
import sys
import threading
import time
import urllib.request
import urllib.error
from concurrent.futures import ThreadPoolExecutor
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path

try:
    from PIL import Image, ImageChops, ImageEnhance, ImageFilter
except ModuleNotFoundError:  # first run on a fresh interpreter: fetch Pillow, then continue
    import subprocess
    print("Pillow is missing for", sys.executable, "- installing it now ...")
    subprocess.run([sys.executable, "-m", "pip", "install", "--quiet", "pillow"], check=False)
    from PIL import Image, ImageChops, ImageEnhance, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
ART = ROOT / "art"
GEN = ART / "gen"
CATALOG = HERE / "catalog.json"
VAULT = Path("D:/Tools/my-vault/vault.json")
PORT = 5091
POOL = ThreadPoolExecutor(max_workers=8)
LOCK = threading.Lock()


# ── credentials ────────────────────────────────────────────────────────────
def credential():
    v = json.loads(VAULT.read_text("utf-8"))
    items = v if isinstance(v, list) else (v.get("items") or v.get("credentials") or v.get("entries") or list(v.values()))
    for e in items:
        if isinstance(e, dict) and e.get("name") == "GPT-IMAGE-2_KEY":
            return e["baseUrl"].rstrip("/"), e["secret"]
    raise RuntimeError("GPT-IMAGE-2_KEY not found in vault")


# ── prompt assembly ────────────────────────────────────────────────────────
def load_catalog():
    return json.loads(CATALOG.read_text("utf-8"))


def assemble(style, subject, bg_sentence, extra=""):
    parts = [style.get(k, "") for k in ("view", "medium", "world", "palette", "light", "isolation")]
    head = " ".join(p.strip() for p in parts if p and p.strip())
    out = head + "\n\nSUBJECT: " + subject.strip().rstrip(".") + "."
    if extra and extra.strip():
        out += "\nNOTES: " + extra.strip()
    if bg_sentence:
        out += "\n" + bg_sentence
    return out


# ── API call ───────────────────────────────────────────────────────────────
def generate_png(prompt, size, quality, transparent):
    base, key = credential()
    body = {"model": "gpt-image-2", "prompt": prompt, "n": 1, "size": size, "quality": quality, "output_format": "png"}
    if transparent:
        body["background"] = "transparent"
    req = urllib.request.Request(base + "/images/generations", data=json.dumps(body).encode("utf-8"),
                                 headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"}, method="POST")
    # The proxy is cheap but flaky: retry generously on any transient failure.
    last = None
    attempts = 6
    for attempt in range(attempts):
        try:
            with urllib.request.urlopen(req, timeout=300) as res:
                payload = json.loads(res.read().decode("utf-8"))
            d = payload["data"][0]
            if d.get("b64_json"):
                return base64.b64decode(d["b64_json"]), payload.get("usage")
            if d.get("url"):
                with urllib.request.urlopen(d["url"], timeout=120) as r2:
                    return r2.read(), payload.get("usage")
            raise RuntimeError("no image in response")
        except urllib.error.HTTPError as e:
            txt = e.read().decode("utf-8", "replace")[:500]
            last = f"HTTP {e.code}: {txt}"
            if e.code in (400, 401, 403) and "content" not in txt.lower():
                raise RuntimeError(last)          # our fault, retrying will not help
        except (urllib.error.URLError, TimeoutError, ConnectionError, json.JSONDecodeError, KeyError, IndexError) as e:
            last = str(e) or type(e).__name__
        if attempt < attempts - 1:
            time.sleep(3 + attempt * 4)
    raise RuntimeError(f"gave up after {attempts} attempts: {last}")


# ── post-processing ────────────────────────────────────────────────────────
def chroma_key(img, lo=40, hi=120, spill=12):
    """Pure-green background -> alpha. greenness = G - max(R,B); soft threshold lo..hi."""
    img = img.convert("RGB")
    r, g, b = img.split()
    max_rb = ImageChops.lighter(r, b)
    greenness = ImageChops.subtract(g, max_rb)
    lut = [255 if v <= lo else 0 if v >= hi else int(255 * (hi - v) / (hi - lo)) for v in range(256)]
    alpha = greenness.point(lut)
    # despill: green may not exceed max(R,B) + spill on kept pixels
    cap = max_rb.point(lambda v: min(255, v + spill))
    g2 = ImageChops.darker(g, cap)
    out = Image.merge("RGBA", (r, g2, b, alpha))
    return out


def clean_alpha(img):
    """API transparency: kill faint fringe pixels and tighten the matte by one pixel."""
    img = img.convert("RGBA")
    r, g, b, a = img.split()
    a = a.point(lambda v: 0 if v < 24 else v)
    a = a.filter(ImageFilter.MinFilter(3))
    return Image.merge("RGBA", (r, g, b, a))


def trim_square(img, pad=0.04):
    a = img.getchannel("A").point(lambda v: 255 if v > 16 else 0)
    box = a.getbbox()
    if not box:
        return img
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    side = int(max(w, h) * (1 + 2 * pad))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img.crop(box), ((side - w) // 2, (side - h) // 2))
    return canvas


# Sprites sit on the chart's ground, so they are pulled toward it: desaturated, darkened,
# and cooled. "tint" is a per-channel multiplier — warm reds down, blues up — and it is what
# makes the whole set read dark blue-grey no matter what hue the model happened to paint.
GRADE = {"color": 0.66, "brightness": 0.86, "contrast": 0.98, "tint": [0.88, 0.96, 1.14]}


def grade_rgb(rgb):
    """The chart's look, applied to an RGB image: desaturate, darken, then cool."""
    rgb = ImageEnhance.Color(rgb).enhance(GRADE["color"])
    rgb = ImageEnhance.Brightness(rgb).enhance(GRADE["brightness"])
    rgb = ImageEnhance.Contrast(rgb).enhance(GRADE["contrast"])
    tint = GRADE.get("tint") or [1, 1, 1]
    if tint != [1, 1, 1]:
        rgb = Image.merge("RGB", [ch.point(lambda v, m=m: min(255, int(v * m)))
                                  for ch, m in zip(rgb.split(), tint)])
    return rgb


def make_sprite(full):
    """256 px map sprite from the keyed full-size image, colour-graded to match the chart."""
    sp = full.resize((256, 256), Image.LANCZOS) if full.size[0] > 256 else full.copy()
    r, g, b, a = sp.split()
    r, g, b = grade_rgb(Image.merge("RGB", (r, g, b))).split()
    return Image.merge("RGBA", (r, g, b, a))


def process_texture(png_bytes):
    """Ground texture: no key, no trim. Mirror-tile to a 1024 seamless tile, 256 preview."""
    img = Image.open(io.BytesIO(png_bytes)).convert("RGB").resize((512, 512), Image.LANCZOS)
    tile = Image.new("RGB", (1024, 1024))
    tile.paste(img, (0, 0))
    tile.paste(img.transpose(Image.FLIP_LEFT_RIGHT), (512, 0))
    tile.paste(img.transpose(Image.FLIP_TOP_BOTTOM), (0, 512))
    tile.paste(img.transpose(Image.FLIP_LEFT_RIGHT).transpose(Image.FLIP_TOP_BOTTOM), (512, 512))
    return tile.convert("RGBA"), tile.resize((256, 256), Image.LANCZOS).convert("RGBA")


def process(png_bytes, bg_mode):
    img = Image.open(io.BytesIO(png_bytes))
    if bg_mode == "chroma":
        img = chroma_key(img)
    else:
        img = clean_alpha(img) if img.mode == "RGBA" else chroma_key(img)
    img = trim_square(img)
    return img, make_sprite(img)


def reprocess_all():
    """Rebuild every .s256 sprite from its full image with the current grade."""
    n = 0
    for meta in library():
        full = ROOT / meta["file"]
        if full.exists():
            make_sprite(Image.open(full).convert("RGBA")).save(ROOT / meta["sprite"])
            n += 1
    return n


# ── library and manifest ───────────────────────────────────────────────────
def library():
    items = []
    if GEN.exists():
        for p in sorted(GEN.rglob("*.json")):
            try:
                items.append(json.loads(p.read_text("utf-8")))
            except Exception:
                pass
    items.sort(key=lambda e: e.get("created", ""), reverse=True)
    return items


def rebuild_manifest():
    everything = [e for e in library() if e.get("approved")]
    approved = [e for e in everything if e.get("kind", "sprite") != "texture"]
    cat = load_catalog()["categories"]
    # approved textures become art/tex-<type>.png (the map multiplies them over that biome);
    # the newest approved one per type wins, and a type with none approved loses its file
    latest = {}
    for e in sorted(everything, key=lambda e: e.get("created", "")):
        if e.get("kind") == "texture":
            latest[e["type"]] = e
    for old in ART.glob("tex-*.png"):
        if old.stem[4:] not in latest:
            old.unlink()
    # Textures take the same grade as the sprites — the tile the map multiplies over a biome
    # is written graded, while the stored tile stays raw, so a grade change re-cools the
    # ground the moment the manifest is rebuilt.
    for typ, e in latest.items():
        src = ROOT / e["file"]
        dst = ART / f"tex-{typ}.png"
        if src.exists():
            grade_rgb(Image.open(src).convert("RGB")).save(dst)
    def weight(e):
        if e.get("weight") is not None:
            return e["weight"]
        return (cat.get(e["category"], {}).get("weights") or {}).get(e["type"], 1)
    manifest = {
        "version": 2,
        "generated": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "categories": {k: {"share": v.get("spriteShare", 1.0), "stepKm": v.get("stepKm", 17)} for k, v in cat.items()},
        "sprites": [
            {"id": e["id"], "category": e["category"], "type": e["type"], "file": e["sprite"], "full": e["file"],
             "footprintKm": e.get("footprintKm", 12),
             # biomes come from the CURRENT catalog so retagging a category retags its sprites
             "biomes": cat.get(e["category"], {}).get("biomes", e.get("biomes", [])),
             "rotate": e.get("rotate", True), "weight": weight(e)}
            for e in approved
        ],
    }
    ART.mkdir(exist_ok=True)
    (ART / "manifest.json").write_text(json.dumps(manifest, indent=2), "utf-8")
    (ART / "manifest.js").write_text("window.MANIFEST = " + json.dumps(manifest) + ";\n", "utf-8")
    return manifest


def run_job(job):
    """One image: generate, process, save. Returns the sidecar dict or {'error': ...}."""
    cat, typ = job["category"], job["type"]
    is_texture = job.get("kind") == "texture"
    bg_mode = "none" if is_texture else job.get("bg", "chroma")
    try:
        png, usage = generate_png(job["prompt"], job.get("size", "1024x1024"), job.get("quality", "low"), bg_mode == "transparent")
        full, sprite = process_texture(png) if is_texture else process(png, bg_mode)
    except Exception as e:
        return {"error": str(e), "category": cat, "type": typ}
    stamp = time.strftime("%Y%m%d-%H%M%S")
    with LOCK:
        idx = job.get("k", 0)
        ident = f"{typ}-{stamp}-{idx}"
        folder = GEN / cat
        folder.mkdir(parents=True, exist_ok=True)
        full_rel = f"art/gen/{cat}/{ident}.png"
        sprite_rel = f"art/gen/{cat}/{ident}.s256.png"
        full.save(ROOT / full_rel)
        sprite.save(ROOT / sprite_rel)
        meta = {
            "id": ident, "category": cat, "type": typ, "file": full_rel, "sprite": sprite_rel,
            "prompt": job["prompt"], "subject": job.get("subject", ""), "styleVersion": job.get("styleVersion"),
            "bg": bg_mode, "size": job.get("size"), "quality": job.get("quality"), "usage": usage,
            "footprintKm": job.get("footprintKm", 12), "biomes": job.get("biomes", []), "rotate": job.get("rotate", True),
            "kind": "texture" if is_texture else "sprite",
            "approved": False, "created": time.strftime("%Y-%m-%dT%H:%M:%S"), "px": full.size[0],
        }
        (folder / f"{ident}.json").write_text(json.dumps(meta, indent=2), "utf-8")
    return meta


# ── HTTP ───────────────────────────────────────────────────────────────────
class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(ROOT), **kw)

    def log_message(self, fmt, *args):
        sys.stdout.write("%s %s\n" % (time.strftime("%H:%M:%S"), fmt % args))

    def _json(self, obj, status=200):
        data = json.dumps(obj).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def _body(self):
        n = int(self.headers.get("Content-Length") or 0)
        return json.loads(self.rfile.read(n).decode("utf-8")) if n else {}

    def do_GET(self):
        if self.path in ("/", "/index.html"):
            self.path = "/generator/index.html"
            return super().do_GET()
        if self.path == "/api/catalog":
            return self._json(load_catalog())
        if self.path == "/api/library":
            return self._json(library())
        if self.path == "/api/status":
            try:
                credential(); ok = True; err = None
            except Exception as e:
                ok = False; err = str(e)
            return self._json({"key": ok, "error": err, "count": len(library()), "approved": sum(1 for e in library() if e.get("approved"))})
        return super().do_GET()

    def do_POST(self):
        try:
            body = self._body()
            if self.path == "/api/catalog":
                CATALOG.write_text(json.dumps(body, indent=2, ensure_ascii=False), "utf-8")
                return self._json({"ok": True})
            if self.path == "/api/preview":
                cat = load_catalog()
                c = cat["categories"].get(body.get("category", ""), {})
                style = c.get("style") or body.get("style") or cat["style"]
                bg = "" if c.get("kind") == "texture" else cat["backgrounds"].get(body.get("bg", "chroma"), "")
                return self._json({"prompt": assemble(style, body["subject"], bg, body.get("extra", ""))})
            if self.path == "/api/generate":
                cat = load_catalog()
                c = cat["categories"].get(body.get("category", ""), {})
                style = c.get("style") or body.get("style") or cat["style"]
                bg_mode = body.get("bg", "chroma")
                bg = "" if c.get("kind") == "texture" else cat["backgrounds"].get(bg_mode, "")
                prompt = body.get("prompt") or assemble(style, body["subject"], bg, body.get("extra", ""))
                n = max(1, min(8, int(body.get("n", 1))))
                jobs = [dict(body, prompt=prompt, k=k, kind=c.get("kind", "sprite"), styleVersion=cat.get("styleVersion")) for k in range(n)]
                results = list(POOL.map(run_job, jobs))
                return self._json({"results": results})
            if self.path == "/api/update":
                p = GEN / body["category"] / (body["id"] + ".json")
                meta = json.loads(p.read_text("utf-8"))
                for k in ("approved", "footprintKm", "biomes", "rotate", "weight"):
                    if k in body:
                        meta[k] = body[k]
                p.write_text(json.dumps(meta, indent=2), "utf-8")
                rebuild_manifest()
                return self._json({"ok": True, "item": meta})
            if self.path == "/api/reprocess":
                if body.get("grade"):
                    GRADE.update(body["grade"])
                n = reprocess_all()
                rebuild_manifest()
                return self._json({"ok": True, "reprocessed": n, "grade": GRADE})
            if self.path == "/api/delete":
                p = GEN / body["category"]
                for suffix in (".json", ".png", ".s256.png"):
                    f = p / (body["id"] + suffix)
                    if f.exists():
                        f.unlink()
                rebuild_manifest()
                return self._json({"ok": True})
            if self.path == "/api/manifest":
                return self._json(rebuild_manifest())
            return self._json({"error": "unknown endpoint"}, 404)
        except Exception as e:
            return self._json({"error": str(e)}, 500)


if __name__ == "__main__":
    ART.mkdir(exist_ok=True)
    GEN.mkdir(exist_ok=True)
    rebuild_manifest()
    try:
        server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    except OSError:
        print(f"The generator is already running at http://127.0.0.1:{PORT} - using that one.")
        sys.exit(0)
    print(f"Map asset generator on http://127.0.0.1:{PORT}  (art -> {ART})")
    server.serve_forever()
