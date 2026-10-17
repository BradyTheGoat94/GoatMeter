"""Local-only GoatSim evidence export preview CLI.

Usage:
  python tools/goatsim_export_cli.py input.json --preview
  python tools/goatsim_export_cli.py input.json --output evidence.json --consent

Input JSON must contain "profile" and "encounters". Nothing is uploaded.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    from tools.goatsim_evidence_export import ExportError, build_export, preview_export, serialize_export
except ModuleNotFoundError:
    from goatsim_evidence_export import ExportError, build_export, preview_export, serialize_export


def main(argv=None):
    parser = argparse.ArgumentParser(description="Local GoatSim evidence preview/export; never uploads")
    parser.add_argument("input", type=Path, help="JSON with profile and encounters")
    parser.add_argument("--preview", action="store_true", help="Show only a summary; no export saved")
    parser.add_argument("--output", type=Path, help="Output file path")
    parser.add_argument("--consent", action="store_true", help="Explicitly authorize local export")
    args = parser.parse_args(argv)

    if args.preview and args.output:
        parser.error("--preview cannot be combined with --output")
    if not args.preview and (not args.output or not args.consent):
        parser.error("saving requires both --output and --consent")
    try:
        data = json.loads(args.input.read_text(encoding="utf-8"))
        if not isinstance(data, dict):
            raise ExportError("input must be a JSON object")
        # Preview is generated locally, without authorizing any transfer or save.
        export = build_export(data.get("profile"), data.get("encounters"), consent=True)
        print(json.dumps(preview_export(export), indent=2))
        if not args.preview:
            destination = args.output
            if destination.exists():
                raise ExportError("refusing to overwrite existing file")
            destination.write_bytes(serialize_export(export))
            print(f"Saved locally to {destination}")
        return 0
    except (OSError, UnicodeError, ValueError, KeyError, TypeError, ExportError) as exc:
        print(f"Export failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
