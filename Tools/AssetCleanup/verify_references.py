"""Cross-check Unity audit candidates against serialized GUID references."""
import argparse
import json
from pathlib import Path
import re
import subprocess


def verify(project, report):
    candidates = {entry['path'] for entry in report['candidates']}
    guids = {entry['guid'].lower(): entry['path'] for entry in report['candidates']}
    failures = []
    # Match GUID fields rather than raw hex blobs (font atlases can contain
    # megabytes of serialized hexadecimal image data on a single line).
    pattern = r'''(?i)(?:[a-z_]*guid[a-z_]*"?\s*:\s*"?|["'])[a-f0-9]{32}'''
    command = ['rg', '--no-ignore', '--json', '-o', pattern,
               'Assets', 'Packages', 'ProjectSettings']
    for extension in ('meta', 'unity', 'prefab', 'asset', 'mat', 'controller', 'overrideController',
                      'anim', 'json', 'shadergraph', 'shadersubgraph', 'vfx', 'cs', 'xml', 'txt'):
        command += ['-g', '*.' + extension]
    if not guids:
        return 0
    process = subprocess.Popen(command, cwd=project, stdout=subprocess.PIPE, encoding='utf-8')
    for line in process.stdout:
        event = json.loads(line)
        if event['type'] != 'match':
            continue
        path = event['data']['path']['text'].replace('\\', '/')
        owner = path[:-5] if path.endswith('.meta') else path
        if owner in candidates:
            continue
        for match in event['data']['submatches']:
            target = guids.get(match['match']['text'][-32:].lower())
            if target:
                failures.append({'owner': path, 'line': event['data']['line_number'], 'candidate': target})
    return_code = process.wait()
    process.stdout.close()
    if return_code not in (0, 1):
        raise RuntimeError('GUID cross-check failed')
    if failures:
        raise ValueError('Retained files reference candidates: ' + json.dumps(failures[:30]))
    return len(candidates)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    report = json.loads((args.project / 'Library/LitDownloadedAssetAudit.json').read_text(encoding='utf-8'))
    print('Serialized GUID cross-check passed:', verify(args.project, report), 'candidates')
