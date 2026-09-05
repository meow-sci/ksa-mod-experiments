"""Reject feature-to-feature project references and missing workspace participants."""
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
shared = {'ksa-abstractions.lib', 'ksa-rings.lib', 'ksa-lights.lib', 'unscience-contracts.lib'}
projects = [root / item.attrib['Path'] for item in ET.parse(root / 'ksa-mod-experiments.slnx').getroot().iter('Project')]
features = {p.parent.name for p in projects if p.parent.name.endswith('.lib')} - shared
assert len(features) == 25, f'Expected 25 retained feature libraries, found {len(features)}'
for project in projects:
    assert project.is_file(), f'Missing project {project}'
    if project.parent.name not in features:
        continue
    for reference in ET.parse(project).getroot().iter('ProjectReference'):
        dependency = Path(reference.attrib['Include'].replace('\\', '/')).parent.name
        assert dependency in shared, f'{project.parent.name} references feature {dependency}'
    source = '\n'.join(p.read_text() for p in project.parent.glob('*.Workspace.cs'))
    assert 'CaptureDraft' in source and 'PrepareRestore' in source, f'{project.parent.name} lacks workspace contract'
    assert (project.parent / 'README.md').exists(), f'{project.parent.name} lacks README'
print(f'PASS: {len(features)} independent feature projects; shared dependencies only.')

for feature in features:
    source = "\n".join(p.read_text() for p in (root / feature).glob("*.cs"))
    assert "public void ReleaseLiveState()" in source, f"{feature} lacks explicit runtime release"
print("PASS: all 25 features own an explicit runtime release path.")
