import json
import os
import sys

art, repo, dest, build_result, head_sha = sys.argv[1:6]
raw = "https://raw.githubusercontent.com/%s/ui-gallery/%s" % (repo, dest)
tree = "https://github.com/%s/tree/ui-gallery/%s" % (repo, dest)

lines = ["<!-- ui-gallery -->", "## UI gallery for %s" % head_sha[:7], ""]
lines.append("Build and test job result: **%s**. Logs and generated files: [%s](%s)." % (build_result, dest, tree))
lines.append("")

report_md = os.path.join(art, "ui-report", "report.md")
if os.path.exists(report_md):
    lines.append("```")
    lines.append(open(report_md, encoding="utf-8").read().strip())
    lines.append("```")
    lines.append("")

manifest = os.path.join(art, "shots", "manifest.json")
entries = []
if os.path.exists(manifest):
    entries = json.load(open(manifest, encoding="utf-8"))

by_name = {}
for e in entries:
    key = (e["name"], str(e["scale"]))
    by_name.setdefault(key, {})[e["theme"]] = e["file"]

diff_dir = os.path.join(art, "ui-report", "diff")

if not by_name:
    lines.append("No screenshots were produced. See the logs folder.")
else:
    lines.append("| Screen | Scale | Light | Dark | Diff vs baseline |")
    lines.append("|---|---|---|---|---|")
    for (name, scale) in sorted(by_name):
        files = by_name[(name, scale)]
        cells = []
        for theme in ("light", "dark"):
            f = files.get(theme)
            cells.append('<img src="%s/shots/%s" width="320">' % (raw, f) if f else "missing")
        diffs = []
        for theme in ("light", "dark"):
            f = files.get(theme)
            if f and os.path.exists(os.path.join(diff_dir, f)):
                diffs.append('<img src="%s/ui-report/diff/%s" width="200">' % (raw, f))
        lines.append("| %s | %s | %s | %s | %s |" % (name, scale, cells[0], cells[1], " ".join(diffs) or "none"))

print("\n".join(lines))
