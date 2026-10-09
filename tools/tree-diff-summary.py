#!/usr/bin/env python3
"""Kratke shrnuti zmen ve Tree.cls: pribyle / upravene / smazane uzly.

Cte dve verze Tree.cls a porovnava uzly podle elementId. S --oneline vypise jeden
anglicky radek pro commit zpravu (tools/tree-sync-task.ps1), bez nej markdown prehled:

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


def oneline(old, new, added, changed, removed, limit=6):
    """Commit zprava je jeden radek, proto jen prvnich `limit` uzlu a pocet zbytku."""
    groups = [
        ("adds", [f"{new[k][0]} ({k})" for k in added]),
        ("changes", [f"{change_label(old, new, k)} ({k})" for k in changed]),
        ("removes", [f"{old[k][0]} ({k})" for k in removed]),
    ]
    parts, shown = [], 0
    for verb, items in groups:
        take = items[: max(0, limit - shown)]
        if take:
            parts.append(f"{verb} {', '.join(take)}")
            shown += len(take)
    if not parts:
        return "no node changed, only the source fingerprint"
    rest = len(added) + len(changed) + len(removed) - shown
    return "; ".join(parts) + (f" and {rest} more" if rest else "")


def change_label(old, new, k):
    # Prejmenovani je nejcastejsi zmena, tak at je videt z ceho na co.
    return new[k][0] if old[k][0] == new[k][0] else f"{old[k][0]} -> {new[k][0]}"


def main():
    # Nazvy uzlu maji diakritiku; Windows python pise defaultne v cp1252 a spadl by.
    sys.stdout.reconfigure(encoding="utf-8")
    args = [a for a in sys.argv[1:] if a != "--oneline"]
    old, new = parse(args[0]), parse(args[1])
    added = sorted(set(new) - set(old), key=lambda k: new[k][0])
    removed = sorted(set(old) - set(new), key=lambda k: old[k][0])
    changed = sorted(
        (k for k in set(old) & set(new) if old[k][1] != new[k][1]),
        key=lambda k: new[k][0],
    )

    if "--oneline" in sys.argv[1:]:
        print(oneline(old, new, added, changed, removed))
        return

    out = []
    if added:
        out.append(f"**Pribylo ({len(added)}):**")
        out += [f"- `{k}` {new[k][0]}" for k in added]
        out.append("")
    if changed:
        out.append(f"**Upraveno ({len(changed)}):**")
        out += [f"- `{k}` {change_label(old, new, k)}" for k in changed]
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
