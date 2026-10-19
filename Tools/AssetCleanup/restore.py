"""Restore a quarantine archive without overwriting changed assets."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    archive = args.archive.resolve()
    manifest = json.loads((archive / 'manifest.json').read_text(encoding='utf-8'))
    project = Path(manifest['project']).resolve()
    with zipfile.ZipFile(archive / 'assets.zip') as saved:
        for entry in manifest['files']:
            target = (project / entry['path']).resolve()
            if not target.is_relative_to(project / 'Assets'):
                raise ValueError('Archive entry escapes Assets')
            if target.exists():
                with target.open('rb') as existing:
                    if hashlib.file_digest(existing, 'sha256').hexdigest() != entry['sha256']:
                        raise ValueError('Refusing to overwrite changed asset: ' + str(target))
        for entry in manifest['files']:
            target = project / entry['path']
            if target.exists():
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            temporary = target.with_name(target.name + '.restoring')
            with saved.open(entry['path']) as source, temporary.open('wb') as output:
                shutil.copyfileobj(source, output)
            with temporary.open('rb') as source:
                if hashlib.file_digest(source, 'sha256').hexdigest() != entry['sha256']:
                    temporary.unlink()
                    raise ValueError('Archive checksum mismatch: ' + entry['path'])
            temporary.replace(target)
    print('Restored', len(manifest['files']), 'verified files to', project)


if __name__ == '__main__':
    run()
