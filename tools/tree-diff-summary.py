#!/usr/bin/env python3
"""Kratke shrnuti zmen ve Tree.cls pro telo PR: pribyle / upravene / smazane uzly.

Cte dve verze Tree.cls a porovnava uzly podle elementId. Pouziva ho
.github/workflows/tree-sync.yml; samostatne:

    git show HEAD:<cesta>/Tree.cls > old.cls
    python3 tools/tree-diff-summary.py old.cls <cesta>/Tree.cls

Proc podle elementId a ne podle celeho radku: prejmenovani uzlu ma vyjit jako
"upraveno", ne jako smazano + pribylo.
"""

import re
import sys

# ..AddExe("nazev", "exe", "fqn", "id", ...) / ..AddFamily("nazev", expanded, "id", ...)
# Uvozovka v ObjectScriptu se escapuje zdvojenim, proto ((?:[^"]|"")*).
STR = r'"((?:[^"]|"")*)"'
RX_EXE = re.compile(r"\.\.AddExe\(\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR + r"\s*,\s*" + STR)
RX_FAM = re.compile(r"\.\.AddFamily\(\s*" + STR + r"\s*,\s*[^,]+,\s*" + STR)


def unquote(s):
    return s.replace('""', '"')


def parse(path):
    """elementId -> (nazev, mnozina radku). Prazdne id (synteticke slozky) se ignoruji."""
    nodes = {}
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            m = RX_EXE.search(line)
            if m:
                eid, name = m.group(4), unquote(m.group(1))
            else:
                m = RX_FAM.search(line)
                if not m:
                    continue
                eid, name = m.group(2), unquote(m.group(1))
            if not eid:
                continue
            entry = nodes.setdefault(eid, [name, set()])
            entry[1].add(line)
    return nodes


def main():
    # Nazvy uzlu maji diakritiku; Windows python pise defaultne v cp1252 a spadl by.
    sys.stdout.reconfigure(encoding="utf-8")
    old, new = parse(sys.argv[1]), parse(sys.argv[2])
    added = sorted(set(new) - set(old), key=lambda k: new[k][0])
    removed = sorted(set(old) - set(new), key=lambda k: old[k][0])
    changed = sorted(
        (k for k in set(old) & set(new) if old[k][1] != new[k][1]),
        key=lambda k: new[k][0],
    )

    out = []
    if added:
        out.append(f"**Pribylo ({len(added)}):**")
        out += [f"- `{k}` {new[k][0]}" for k in added]
        out.append("")
    if changed:
        out.append(f"**Upraveno ({len(changed)}):**")
        for k in changed:
            # Prejmenovani je nejcastejsi zmena, tak at je videt z ceho na co.
            label = new[k][0] if old[k][0] == new[k][0] else f"{old[k][0]} -> {new[k][0]}"
            out.append(f"- `{k}` {label}")
        out.append("")
    if removed:
        out.append(f"**Smazano ({len(removed)}):**")
        out += [f"- `{k}` {old[k][0]}" for k in removed]
        out.append("")
    if not (added or changed or removed):
        out.append("Zadna zmena uzlu - lisi se jen fingerprint zdroju.")

    print("\n".join(out).rstrip())


if __name__ == "__main__":
    main()
