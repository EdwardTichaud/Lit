"""Quarantine a fresh Unity dependency audit; default operation is read-only."""
import argparse
import collections
import datetime
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile
from verify_references import verify


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(4 * 1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def run():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--pack', help='Optional single pack name (Fab or top-level)')
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--resume', action='store_true', help='Reuse a verified existing archive after refreshing the audit')
    args = parser.parse_args()
    project = args.project.resolve()
    report_path = project / 'Library/LitDownloadedAssetAudit.json'
    error_path = Path(str(report_path) + '.error.txt')
    if error_path.exists() and error_path.stat().st_mtime >= report_path.stat().st_mtime:
        raise ValueError('Unity audit failed; a new successful audit is required')
    report = json.loads(report_path.read_text(encoding='utf-8'))
    if Path(report['project']).resolve() != project:
        raise ValueError('Audit belongs to a different project')
    audited_at = datetime.datetime.fromisoformat(report['utc'].replace('Z', '+00:00')).timestamp()
    if datetime.datetime.now(datetime.timezone.utc).timestamp() - audited_at > 3600:
        raise ValueError('Audit older than one hour; rerun Unity audit')
    allowed_scopes = json.loads(Path(__file__).with_name('scopes.json').read_text(encoding='utf-8'))['roots']
    scopes = report.get('scopes') or [report['scope']]
    if not scopes or any(not scope.startswith('Assets/') or not scope.endswith('/') or '..' in scope
                         or not any(scope.startswith(allowed) for allowed in allowed_scopes) for scope in scopes):
        raise ValueError('Unexpected audit scope')
    def pack_name(relative):
        fab = 'Assets/0 - UnityPackages/Fab/'
        unity = 'Assets/0 - UnityPackages/UnityAssets/'
        if relative.startswith(fab):
            return relative[len(fab):].split('/')[0]
        if relative.startswith(unity):
            return 'UnityAssets/' + relative[len(unity):].split('/')[0]
        return relative.split('/')[1]
    preserved = set(report['preserved'])
    entries = [entry for entry in report['candidates']
               if not args.pack or pack_name(entry['path']) == args.pack]
    files = []
    sizes = collections.Counter()
    for entry in entries:
        relative = entry['path']
        matched_scopes = [scope for scope in scopes if relative.startswith(scope)]
        if relative in preserved or not matched_scopes:
            raise ValueError('Preserved or out-of-scope candidate: ' + relative)
        source = (project / relative).resolve()
        if not any(source.is_relative_to((project / scope).resolve()) for scope in matched_scopes):
            raise ValueError('Path escapes audited directories')
        meta = Path(str(source) + '.meta')
        if not source.is_file() or not meta.is_file():
            raise ValueError('Missing candidate or metadata: ' + relative)
        guid = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8'), re.M)
        if not guid or guid.group(1) != entry['guid'] or source.stat().st_size != entry['bytes']:
            raise ValueError('Candidate changed since audit: ' + relative)
        for path in (source, meta):
            if path.stat().st_mtime > audited_at:
                raise ValueError('Candidate edited after audit: ' + str(path))
            files.append(path)
        sizes[pack_name(relative)] += source.stat().st_size + meta.stat().st_size
    print(json.dumps({'files': len(files), 'bytes': sum(sizes.values()), 'packs': sizes}, indent=2), flush=True)
    if not args.apply or not files:
        return
    if args.archive is None:
        raise ValueError('--archive is required for --apply')
    if (project / 'Temp/UnityLockfile').exists():
        raise ValueError('Close Unity on the source project before removing assets')
    archive_dir = args.archive.resolve()
    if archive_dir.is_relative_to(project) or (archive_dir.exists() and not args.resume):
        raise ValueError('Archive must be a new directory outside the project')
    if shutil.disk_usage(archive_dir.parent).free < sum(sizes.values()) + 1024 ** 3:
        raise ValueError('Insufficient archive disk space')
    # Refuse stale analysis when a concurrent task changed serialized dependencies.
    dependency_extensions = {'.cs', '.meta', '.asset', '.prefab', '.unity', '.mat', '.controller',
                             '.overridecontroller', '.anim', '.json', '.shadergraph', '.shadersubgraph',
                             '.vfx', '.spriteatlas', '.terrainlayer', '.lighting', '.playable', '.mixer',
                             '.fbx', '.obj', '.dae', '.blend', '.gltf', '.glb'}
    code_references = {entry['path']: entry['tokens'] for entry in report.get('codeReferences', [])}
    def assert_fresh_dependencies():
        if (project / 'Temp/UnityLockfile').exists():
            raise ValueError('Close Unity on the source project before removing assets')
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            for path in (project / folder).rglob('*'):
                if path.is_file() and path.suffix.lower() in dependency_extensions and path.stat().st_mtime > audited_at:
                    relative = path.relative_to(project).as_posix()
                    if path.suffix.lower() == '.cs' and relative in code_references:
                        tokens = sorted({match.group(0) for match in re.finditer(r'"(Assets/[^"\r\n]+)"|[a-fA-F0-9]{32}', path.read_text(encoding='utf-8-sig'))})
                        if tokens == code_references[relative]:
                            continue
                    raise ValueError('Dependencies changed after audit; rerun audit: ' + str(path))
    assert_fresh_dependencies()
    verify(project, dict(report, candidates=entries))
    archive_dir.mkdir(exist_ok=args.resume)
    backup = archive_dir / 'assets.zip'
    manifest = {'project': str(project), 'audit': report, 'files': [], 'removed': []}
    if args.resume and (archive_dir / 'manifest.json').exists():
        prior = json.loads((archive_dir / 'manifest.json').read_text(encoding='utf-8'))
        if Path(prior['project']).resolve() != project or prior['removed']:
            raise ValueError('Resume requires an archive from this project with no prior removals')
        stored = {entry['path']: entry for entry in prior['files']}
        for path in files:
            relative = path.relative_to(project).as_posix()
            if relative not in stored:
                raise ValueError('New audit needs files absent from the archive: ' + relative)
            manifest['files'].append(stored[relative])
        print('Reusing verified archive; checking', len(files), 'current source files', flush=True)
    else:
        mode = 'a' if args.resume and backup.exists() else 'w'
        with zipfile.ZipFile(backup, mode, compression=zipfile.ZIP_STORED, allowZip64=True, strict_timestamps=False) as output:
            stored_names = set(output.namelist())
            for index, path in enumerate(files):
                relative = path.relative_to(project).as_posix()
                sha256 = digest(path)
                if relative in stored_names:
                    with output.open(relative) as stored:
                        if hashlib.file_digest(stored, 'sha256').hexdigest() != sha256:
                            raise ValueError('Partial archive differs from source: ' + relative)
                else:
                    output.write(path, relative)
                manifest['files'].append({'path': relative, 'sha256': sha256, 'bytes': path.stat().st_size})
                if index % 200 == 0:
                    print('Archived', index, '/', len(files), flush=True)
    # Verify the stored bytes before removing any source file.
    with zipfile.ZipFile(backup) as saved:
        for entry in manifest['files']:
            hasher = hashlib.sha256()
            with saved.open(entry['path']) as source:
                for block in iter(lambda: source.read(4 * 1024 * 1024), b''):
                    hasher.update(block)
            if hasher.hexdigest() != entry['sha256'] or digest(project / entry['path']) != entry['sha256']:
                raise ValueError('Archive or source verification failed: ' + entry['path'])
    manifest_path = archive_dir / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    assert_fresh_dependencies()
    verify(project, dict(report, candidates=entries))
    for entry in manifest['files']:
        (project / entry['path']).unlink()
        manifest['removed'].append(entry['path'])
        if len(manifest['removed']) % 100 == 0:
            manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('Quarantined', len(files), 'files; archive:', archive_dir, flush=True)


if __name__ == '__main__':
    run()
