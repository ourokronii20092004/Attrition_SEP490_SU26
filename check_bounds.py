import re, io, glob, os

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                      "Attrition_Game", "Assets", "_Project", "Art", "Maps"))
maps = []
for f in sorted(glob.glob("*.asset")):
    s = io.open(f, encoding="utf-8", errors="replace").read()
    c = re.search(r"worldBounds:\s*\n\s*m_Center:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+)", s)
    e = re.search(r"m_Extent:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+)", s)
    scene = re.search(r"sceneName:\s*(.+)", s).group(1).strip()
    cx, cy, ex, ey = (float(c.group(1)), float(c.group(2)),
                      float(e.group(1)), float(e.group(2)))
    cps = [(m.group(1).strip(), float(m.group(2)), float(m.group(3)))
           for m in re.finditer(
               r"checkpointId:\s*(.+?)\s*\n\s*worldPos:\s*\{x:\s*([-\d.]+),\s*y:\s*([-\d.]+)", s)]
    maps.append((scene, cx - ex, cx + ex, cy - ey, cy + ey, cps))

print("=== checkpoint co nam trong bounds map cua no? ===")
bad = 0
for scene, x0, x1, y0, y1, cps in maps:
    print("%-24s x[%7.0f,%7.0f] y[%6.0f,%6.0f]  %d cp" % (scene, x0, x1, y0, y1, len(cps)))
    for cid, px, py in cps:
        ok = x0 <= px <= x1 and y0 <= py <= y1
        if not ok:
            bad += 1
        print("    %s %-30s (%7.1f,%7.1f)" % ("OK " if ok else "OUT", cid, px, py))
print("TONG cp ngoai bounds:", bad)

print()
print("=== toa do map A co the LOT vao bounds map B khong? (nguon loi duoi long dat) ===")
for a, _, _, _, _, cps in maps:
    for cid, px, py in cps:
        hits = [b for b, x0, x1, y0, y1, _ in maps
                if b != a and x0 <= px <= x1 and y0 <= py <= y1]
        if hits:
            print("  '%s' (%.0f,%.0f) cua %s => CUNG nam trong %s" % (cid, px, py, a, hits))
