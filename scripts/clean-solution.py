#!/usr/bin/env python3
"""Removes MP Core's own projects from a solution file.

In Local mode (Directory.Build.targets) a framework package reference becomes a project reference, and
`dotnet sln add` then pulls the framework's projects into the solution. They do not belong there: a clone
without MP Core next to it could not load the solution. Run this after adding a project.

    scripts/clean-solution.py commerce/Storefront.Commerce.Backend.sln
"""
import re
import sys

for path in sys.argv[1:]:
    text = open(path, encoding="utf-8-sig").read()
    removed = set()

    def project(match):
        if match.group("name").startswith("MPCore."):
            removed.add(match.group("guid").upper())
            return ""
        return match.group(0)

    text = re.sub(
        r'Project\("\{[^}]+\}"\) = "(?P<name>[^"]+)", "[^"]+", "\{(?P<guid>[^}]+)\}"\r?\nEndProject\r?\n',
        project, text)
    lines = [line for line in text.splitlines(keepends=True)
             if not any("{" + guid + "}" in line.upper() for guid in removed)]
    open(path, "w", encoding="utf-8").write("".join(lines))
    print(f"{path}: removed {len(removed)} MP Core project(s)")
