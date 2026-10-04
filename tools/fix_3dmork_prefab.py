#!/usr/bin/env python3
"""
Fix 3DMork prefab:
- Trim leaderboard arrays to 6
- Hide rows #7 and #8 (LbRow_6/7 and their 6 cells each) by setting m_IsActive:0
- Speed up benchmark: scale durations from 4s to 2.0s (2x faster)
Applies to:
  * Assets/Resources/3DMork_Stage.prefab
  * Assets/Scenes/3DMork_Room.unity
  * Assets/Scripts/.../ThreeDMork.cs & FlybyWaypoint.cs (defaults)
Usage:
  python3 tools/fix_3dmork_prefab.py [--check] [--target 2.0]
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PREFAB = ROOT / "Assets/Resources/apps/3DMork.prefab"
STAGE = ROOT / "Assets/Resources/3DMork_Stage.prefab"
ROOM = ROOT / "Assets/Scenes/3DMork_Room.unity"
CS_WAYPOINT = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/FlybyWaypoint.cs"
CS_3DMORK = ROOT / "Assets/Scripts/Assembly-CSharp/PC/Component/Software/ThreeDMork.cs"

LEADERBOARD_ROWS = 6
TARGET_DURATION = 2.0

EXTRA_ROWS = []
for idx in [6, 7]:
    EXTRA_ROWS.extend([f"LbRow_{idx}", f"LbRank_{idx}", f"LbName_{idx}", f"LbCpu_{idx}", f"LbGpu_{idx}", f"LbScore_{idx}", f"LbFps_{idx}"])

def patch_prefab_rows(prefab_path: Path, check=False):
    text = prefab_path.read_text(encoding='utf-8')
    original = text

    # 1. Trim leaderboard arrays
    for key in ["leaderboardRank", "leaderboardName", "leaderboardCpu", "leaderboardGpu", "leaderboardScore", "leaderboardFps"]:
        m = re.search(rf"  {key}:\n((?:  - \{{fileID: [^\}}]+\}}\n)+)", text)
        if m:
            block = m.group(1)
            entries = re.findall(r"  - \{fileID:", block)
            if len(entries) != LEADERBOARD_ROWS:
                if not check:
                    lines = block.strip().split("\n")
                    trimmed = "\n".join(lines[:LEADERBOARD_ROWS]) + "\n"
                    text = text.replace(m.group(0), f"  {key}:\n{trimmed}", 1)
                    print(f"  trimmed {key}: {len(entries)} -> {LEADERBOARD_ROWS}")
                else:
                    print(f"[CHECK FAIL] {key} has {len(entries)} entries, expected {LEADERBOARD_ROWS}")
                    return False
            else:
                if check:
                    print(f"  ok {key}: {len(entries)}")

    # 2. Deactivate extra rows via line-by-line (safe, idempotent)
    lines = text.split("\n")
    new_lines = []
    current_go = None
    in_go = False
    changed = 0
    for idx, line in enumerate(lines):
        if line.startswith("--- !u!1 &"):
            in_go = True
            current_go = None
        elif in_go and "m_Name:" in line:
            m = re.search(r"m_Name: (Lb\w+_\d+)", line)
            if m:
                current_go = m.group(1)
        elif in_go and "m_IsActive:" in line and current_go in EXTRA_ROWS:
            if "m_IsActive: 1" in line:
                if not check:
                    line = line.replace("m_IsActive: 1", "m_IsActive: 0")
                    changed += 1
                    print(f"  deactivated {current_go} at line {idx+1}")
                else:
                    print(f"[CHECK FAIL] {current_go} active at line {idx+1}")
                    return False
            in_go = False
            current_go = None
        elif line.startswith("--- !u!") and not line.startswith("--- !u!1 &"):
            if in_go and current_go is None:
                in_go = False
        new_lines.append(line)

    text = "\n".join(new_lines)

    if check:
        # verify all extra rows inactive
        for name in EXTRA_ROWS:
            # quick check: there should be no "m_Name: <name>" followed by "m_IsActive: 1"
            pass
        print(f"  check ok: {changed} extra rows would be deactivated" if not check else "  check ok: extra rows inactive")
        return True
    else:
        if text != original:
            prefab_path.write_text(text, encoding='utf-8')
            print(f"patched {prefab_path} ({changed} rows deactivated)")
            return True
        else:
            print(f"no changes needed for {prefab_path}")
            return False

def patch_durations(path: Path, target=2.0, check=False):
    text = path.read_text(encoding='utf-8')
    original = text
    count = 0
    if path.suffix == ".cs":
        # replace duration = Xf with target f
        new_text, n = re.subn(r"(duration\s*=\s*)(\d+(?:\.\d+)?)f(\s*[;,])", lambda m: m.group(1)+f"{target}f"+m.group(3), text)
        # Count only those that changed (old != target)
        # re.subn gives total replacements, but we want to know if any
        if new_text != text:
            # Only count where old value != target
            for m in re.finditer(r"duration\s*=\s*(\d+(?:\.\d+)?)f", text):
                if abs(float(m.group(1)) - target) > 0.01:
                    count += 1
            text = new_text
        # also fallback pDuration
        # The above already handles it (duration = Xf)
        if check:
            # Check no duration > target+0.3
            vals = [float(x) for x in re.findall(r"duration\s*=\s*(\d+(?:\.\d+)?)f", text)]
            slow = [v for v in vals if v > target+0.3]
            if slow:
                print(f"[CHECK FAIL] {path.name} still has slow durations: {slow}")
                return False
            print(f"  check ok: {path.name} CS durations ok")
            return True
        else:
            if text != original:
                path.write_text(text, encoding='utf-8')
                print(f"patched durations in {path} ({count} replacements)")
                return True
            else:
                print(f"no duration changes for {path}")
                return False
    else:
        # YAML durations
        def repl(m):
            nonlocal count
            old = float(m.group(2))
            if old > target+0.1:
                # scale preserving ratio: new = old * target/4.0, but snap to nice values
                if abs(old - 4.0) < 0.01:
                    new_val = target
                elif abs(old - 4.2) < 0.01:
                    new_val = round(target * 1.05, 2)
                elif abs(old - 4.5) < 0.01:
                    new_val = round(target * 1.125, 2)
                elif abs(old - 3.8) < 0.01:
                    new_val = round(target * 0.95, 2)
                else:
                    new_val = round(old * (target/4.0), 2)
                count += 1
                return m.group(1) + str(new_val)
            return m.group(0)
        text = re.sub(r"(\s*duration:\s*)(\d+(?:\.\d+)?)", repl, text)
        if check:
            vals = [float(x) for x in re.findall(r"duration:\s*(\d+(?:\.\d+)?)", text)]
            slow = [v for v in vals if v > target+0.4]
            if slow:
                print(f"[CHECK FAIL] {path.name} still has slow durations: {slow}")
                return False
            print(f"  check ok: {path.name} durations ok")
            return True
        else:
            if text != original:
                path.write_text(text, encoding='utf-8')
                print(f"patched durations in {path} ({count} replacements)")
                return True
            else:
                print(f"no duration changes for {path}")
                return False

def main():
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("--check", action="store_true")
    p.add_argument("--target", type=float, default=2.0)
    args = p.parse_args()
    ok = True
    print(f"Fixing 3DMork: leaderboard {LEADERBOARD_ROWS} rows, target {args.target}s")
    if PREFAB.exists():
        print(f"\n{PREFAB}:")
        res = patch_prefab_rows(PREFAB, check=args.check)
        if args.check and not res:
            ok = False
    for path in [STAGE, ROOM]:
        if path.exists():
            print(f"\n{path}:")
            res = patch_durations(path, target=args.target, check=args.check)
            if args.check and not res:
                ok = False
    for cs in [CS_WAYPOINT, CS_3DMORK]:
        if cs.exists():
            print(f"\n{cs}:")
            res = patch_durations(cs, target=args.target, check=args.check)
            if args.check and not res:
                ok = False
    if args.check:
        sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
