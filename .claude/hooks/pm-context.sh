#!/usr/bin/env bash
# SessionStart: if the working directory is a ProjectMan project (.project/),
# print a compact status summary so every session opens with context.
# stdout from a SessionStart hook is added to Claude's context.
#
# The same script is used globally (~/.claude/hooks/session-start.sh) and as a
# project-local copy (.claude/hooks/pm-context.sh, written by /setup-project so
# teammates without the global setup still get context). When both are wired,
# a per-session marker makes the second invocation a no-op.
input=$(cat)
cwd=$(printf '%s' "$input" | jq -r '.cwd // ""' 2>/dev/null)
sid=$(printf '%s' "$input" | jq -r '.session_id // "nosession"' 2>/dev/null)
[ -z "$cwd" ] && cwd="$PWD"
proj="$cwd/.project"
[ -f "$proj/config.yaml" ] || exit 0

run="${XDG_RUNTIME_DIR:-/tmp}/claude-notify-${USER:-u}"; mkdir -p "$run" 2>/dev/null
marker="$run/pm-context-$sid"
if [ -f "$marker" ] && [ $(( $(date +%s) - $(stat -c %Y "$marker" 2>/dev/null || stat -f %m "$marker" 2>/dev/null || echo 0) )) -lt 30 ]; then
  exit 0   # already printed for this session moments ago
fi
touch "$marker" 2>/dev/null

# Prefer the pipx venv python (has PyYAML); fall back to any python3 with yaml.
py=""
for cand in "${PIPX_HOME:-$HOME/.local/share/pipx}/venvs/projectman/bin/python" \
            "${PIPX_HOME:-$HOME/.local/pipx}/venvs/projectman/bin/python" \
            "$(command -v python3 2>/dev/null)"; do
  [ -x "$cand" ] && "$cand" -c 'import yaml' 2>/dev/null && { py="$cand"; break; }
done

echo "## ProjectMan context ($proj)"
if [ -z "$py" ]; then
  echo "ProjectMan project detected but no python with PyYAML found for the summary."
else
  "$py" - "$proj" <<'PY'
import sys, yaml, pathlib, collections
p = pathlib.Path(sys.argv[1])
cfg = yaml.safe_load((p / "config.yaml").read_text()) or {}
name = cfg.get("name", p.parent.name); prefix = cfg.get("prefix", "")
if cfg.get("hub"):
    projs = cfg.get("projects") or []
    print(f"Hub **{name}** with {len(projs)} project(s): " + ", ".join(
        (x.get("name") if isinstance(x, dict) else str(x)) for x in projs))
idx = p / "index.yaml"
entries = (yaml.safe_load(idx.read_text()) or {}).get("entries", []) if idx.exists() else []
by = collections.defaultdict(collections.Counter)
pts = done = 0
for e in entries:
    t, s = e.get("type", "?"), e.get("status", "?")
    by[t][s] += 1
    if t == "story" and e.get("points"):
        pts += e["points"]
        if s in ("done", "completed", "archived"): done += e["points"]
print(f"Project **{name}** ({prefix})")
for t, plural in (("epic", "epics"), ("story", "stories"), ("task", "tasks")):
    if by[t]:
        total = sum(by[t].values())
        detail = ", ".join(f"{k} {v}" for k, v in sorted(by[t].items(), key=lambda kv: -kv[1]))
        print(f"- {plural}: {total} ({detail})")
if pts:
    print(f"- points: {done}/{pts} ({round(done*100/pts)}%)")
active = [e for e in entries if e.get("type") == "task" and e.get("status") in ("in_progress", "in-progress", "active", "grabbed")]
for e in active[:5]:
    print(f"- in progress: {e['id']} {e.get('title','')}")
sp = p / "sprints"
if sp.is_dir():
    for f in sorted(sp.glob("*.md")):
        txt = f.read_text(errors="ignore")
        if "status: active" in txt:
            print(f"- active sprint: {f.stem}")
            break
h = p / "HANDOFF.md"
if h.exists():
    print(f"- last handoff: {h} (read it before starting)")
PY
fi
echo "Use pm_context for full context, /pm-status for the dashboard, /pm board for what to pick up."
exit 0
