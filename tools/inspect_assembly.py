#!/usr/bin/env python3
"""List types/methods/parameters in a .NET assembly (e.g. Wobbly Life's Assembly-CSharp.dll)
to find network-manager patch targets. Works on Mono assemblies; for IL2CPP use Il2CppDumper first.

    pip install dnfile
    python3 -I tools/inspect_assembly.py "<game>/Wobbly Life_Data/Managed/Assembly-CSharp.dll" \
        --type-filter NetworkManager --method-filter HostServer --method-filter MaxPlayer
"""
import argparse
import dnfile


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dll")
    ap.add_argument("--type-filter", action="append", default=[], help="substring of type name (repeatable, OR)")
    ap.add_argument("--method-filter", action="append", default=[], help="substring of method name (repeatable, OR)")
    a = ap.parse_args()

    pe = dnfile.dnPE(a.dll)
    t = pe.net.mdtables
    tf = [s.lower() for s in a.type_filter]
    mf = [s.lower() for s in a.method_filter]

    methods = list(t.MethodDef)
    for td in t.TypeDef:
        full = f"{td.TypeNamespace}.{td.TypeName}".strip(".")
        if tf and not any(s in full.lower() for s in tf):
            continue
        rows = []
        for mi in td.MethodList:
            m = mi.row
            if mf and not any(s in str(m.Name).lower() for s in mf):
                continue
            params = [str(p.row.Name) for p in (m.ParamList or []) if p.row is not None and p.row.Sequence > 0]
            rows.append(f"    {m.Name}({', '.join(params)})")
        if rows:
            print(full)
            print("\n".join(rows))


if __name__ == "__main__":
    main()
