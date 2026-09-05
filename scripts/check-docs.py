#!/usr/bin/env python3
"""Check maintained project documentation and local links in current first-party guides.

Historical plans, archived scope, vendored decompilation and copied third-party manuals
are deliberately excluded: their source paths describe their recorded version.
"""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
# Ignore leftover build directories from retired projects, just as a clean CI
# checkout does. Include new, nonignored files so unstaged docs can be checked.
source_files = subprocess.check_output(
    ['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=root
).decode().split('\0')
source_paths = set()
for name in filter(None, source_files):
    path = root / name
    if path.is_file():
        source_paths.add(path)
        source_paths.update(path.parents)
errors = []
index = (root / 'REPOSITORY_INDEX.md').read_text()
projects = sorted(p for folder in root.iterdir() if folder.is_dir() and not folder.name.startswith('.')
                  and folder.name not in ('decomp', 'plans', 'docs', 'scope')
                  for p in folder.glob('*.csproj'))
for project in projects:
    readme = project.parent / 'README.md'
    if not readme.is_file():
        errors.append(f'{project.relative_to(root)}: missing README.md')
    if str(readme.relative_to(root)) not in index:
        errors.append(f'{project.relative_to(root)}: not indexed')

files = set(root.glob('*.md')) | set(root.glob('*.lib/README.md'))
files |= {p.parent / 'README.md' for p in projects}
files |= set((root / 'scope').glob('*.md'))
files |= {root / 'docs/WORKSPACE.md', root / 'plans/README.md'}
files |= {root / '.agents/skills' / name for name in ('ksa/SKILL.md', 'ksa/lifecycle.md', 'ksa/lights.md', 'ksa/debug.md', 'mod-impl/SKILL.md', 'rpc/SKILL.md')}
for file in sorted(files):
    if not file.exists():
        continue
    text = re.sub(r'```.*?```', '', file.read_text(), flags=re.S)
    for raw in re.findall(r'\]\(([^)]+)\)', text):
        target = raw.split(' "', 1)[0].strip('<>')
        if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', target) or target.startswith('#'):
            continue
        path = target.split('#', 1)[0]
        if path and (file.parent / path).resolve() not in source_paths:
            errors.append(f'{file.relative_to(root)}: broken link {target}')
    if file.parent == root / 'scope' and 'where they conflict with this section' in text:
        errors.append(f'{file.relative_to(root)}: superseded ownership disclaimer in current scope')

if errors:
    print('\n'.join(errors), file=sys.stderr)
    sys.exit(1)
print(f'PASS: {len(projects)} maintained projects indexed with READMEs; local links in {len(files)} current guides resolve.')
