"""Gera a pasta dist/ com o que vai para o GitHub Releases: manifest.json, zips dos componentes e options.txt.

Uso:
  python gerar_pacote.py --versao 1 --base https://github.com/DONO/REPO/releases/download/v1/
  python gerar_pacote.py --versao 1 --local      (URLs file:// para testar o instalador no próprio PC)
"""
import argparse, hashlib, json, os, shutil, zipfile

INST = r"C:\Users\User\curseforge\minecraft\Instances\Otherworld"
EXTRA = r"C:\Users\User\Downloads\otherworld-extra"
HERE = os.path.dirname(os.path.abspath(__file__))
DIST = os.path.join(HERE, "dist")
SERVER = {"name": "Otherworld dos Amigos", "ip": "178.92.162.111:25576"}

# id, nome mostrado, pasta no .minecraft, substituir a pasta inteira?
COMPONENTS = [
    ("config", "configurações dos mods", "config", True),
    ("defaultconfigs", "configurações padrão", "defaultconfigs", True),
    ("configureddefaults", "padrões do pack", "configureddefaults", True),
    ("resourcepacks", "resource packs e tradução PT-BR", "resourcepacks", False),
    ("shaderpacks", "shaders", "shaderpacks", False),
    ("journeymap", "mapa (JourneyMap)", "journeymap", False),
    ("datapacks", "datapacks", "datapacks", False),
]


def sha1(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""):
            h.update(b)
    return h.hexdigest()


def zip_dir(src, out, extra_files=(), skip=frozenset(), prefix=""):
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for r, _, fs in os.walk(src):
            for f in sorted(fs):
                p = os.path.join(r, f)
                rel = os.path.relpath(p, src).replace("\\", "/")
                if (prefix + rel).lower() in skip:
                    continue
                # data fixa para o zip (e o hash) só mudar quando o conteúdo mudar
                info = zipfile.ZipInfo(rel, date_time=(2026, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                with open(p, "rb") as fh:
                    z.writestr(info, fh.read())
        for rel, p in extra_files:
            info = zipfile.ZipInfo(rel, date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(p, "rb") as fh:
                z.writestr(info, fh.read())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--versao", required=True)
    ap.add_argument("--base", help="URL base dos arquivos do release (termina com /)")
    ap.add_argument("--local", action="store_true")
    a = ap.parse_args()
    if os.path.isdir(DIST):
        shutil.rmtree(DIST)
    os.makedirs(DIST)
    base = ("file:///" + DIST.replace("\\", "/") + "/") if a.local else a.base

    inst = json.load(open(os.path.join(INST, "minecraftinstance.json"), encoding="utf-8"))
    mods = []
    from_cf = set()  # caminhos relativos que o instalador baixa direto do CurseForge
    for ad in inst["installedAddons"]:
        d = ad["categorySection"]["path"]
        f = ad["installedFile"]
        h = next(x["value"] for x in f["hashes"] if x["type"] == 1)
        local = os.path.join(INST, d, ad["fileNameOnDisk"])
        assert os.path.exists(local) and sha1(local) == h, "arquivo diferente do CurseForge: " + d + "/" + ad["fileNameOnDisk"]
        mods.append({"file": ad["fileNameOnDisk"], "dir": d, "sha1": h, "url": f["downloadUrl"], "size": os.path.getsize(local)})
        from_cf.add((d + "/" + ad["fileNameOnDisk"]).lower())
    # mods soltos que não vieram do CurseForge não teriam link: avisa
    soltos = set(os.listdir(os.path.join(INST, "mods"))) - {m["file"] for m in mods if m["dir"] == "mods"}
    if soltos:
        print("AVISO: mods sem link do CurseForge (não entram):", soltos)

    comps = []
    for cid, label, target, replace in COMPONENTS:
        src = os.path.join(INST, target)
        extra = []
        if cid == "resourcepacks":
            extra.append(("Otherworld PT-BR.zip", os.path.join(EXTRA, "Otherworld PT-BR.zip")))
        if not os.path.isdir(src) and not extra:
            continue
        out = os.path.join(DIST, cid + ".zip")
        zip_dir(src if os.path.isdir(src) else os.devnull, out, extra, from_cf, target + "/")
        comps.append({"id": cid, "label": label, "target": target, "replace": replace,
                      "url": base + cid + ".zip", "sha1": sha1(out), "size": os.path.getsize(out)})
        print(f"{cid:20s} {os.path.getsize(out) >> 20:5d} MB")

    shutil.copy2(os.path.join(EXTRA, "options.txt"), os.path.join(DIST, "options.txt"))
    manifest = {
        "name": "Otherworld v8 HF2", "version": a.versao, "mcVersion": "1.20.1", "forge": "47.4.20",
        "server": SERVER, "mods": mods, "components": comps,
        "optionsUrl": base + "options.txt", "optionsSha1": sha1(os.path.join(DIST, "options.txt")),
    }
    json.dump(manifest, open(os.path.join(DIST, "manifest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"arquivos do CurseForge: {len(mods)} ({sum(m['size'] for m in mods) >> 20} MB)")
    print("dist pronto em", DIST)


if __name__ == "__main__":
    main()
