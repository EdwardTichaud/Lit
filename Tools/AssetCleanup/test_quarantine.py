import contextlib
import datetime
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

import quarantine
import restore


class QuarantineTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.project = self.base / 'Project'
        self.relative = 'Assets/0 - UnityPackages/Fab/Test/unused.png'
        self.asset = self.project / self.relative
        self.asset.parent.mkdir(parents=True)
        self.asset.write_bytes(b'asset bytes\x00' * 100)
        self.meta = Path(str(self.asset) + '.meta')
        self.guid = 'abcdef0123456789abcdef0123456789'
        self.meta.write_text('guid: ' + self.guid + '\n')
        for folder in ('Library', 'Packages', 'ProjectSettings'):
            (self.project / folder).mkdir()
        self.archive = self.base / 'Archive'

    def audit(self):
        settled = datetime.datetime.now(datetime.timezone.utc).timestamp() - 2
        for path in self.project.rglob('*'):
            if path.is_file():
                os.utime(path, (settled, settled))
        report = dict(project=str(self.project), scope='Assets/0 - UnityPackages/Fab/',
                      utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), preserved=[],
                      candidates=[dict(path=self.relative, guid=self.guid, bytes=self.asset.stat().st_size)])
        (self.project / 'Library/LitDownloadedAssetAudit.json').write_text(json.dumps(report))

    def apply(self, pack=None, resume=False):
        arguments = ['quarantine', '--project', str(self.project), '--archive', str(self.archive), '--apply']
        if pack:
            arguments += ['--pack', pack]
        if resume:
            arguments += ['--resume']
        with patch.object(sys, 'argv', arguments), contextlib.redirect_stdout(io.StringIO()):
            quarantine.run()

    def test_archive_restore_preserves_bytes_and_metadata(self):
        original = self.asset.read_bytes(), self.meta.read_bytes()
        self.audit()
        self.apply()
        self.assertFalse(self.asset.exists())
        self.assertFalse(self.meta.exists())
        with patch.object(sys, 'argv', ['restore', str(self.archive)]), contextlib.redirect_stdout(io.StringIO()):
            restore.run()
        self.assertEqual((self.asset.read_bytes(), self.meta.read_bytes()), original)

    def test_external_reference_refuses_removal(self):
        (self.project / 'Assets/Keep.prefab').write_text('m_Texture: {guid: ' + self.guid + '}')
        self.audit()
        with self.assertRaisesRegex(ValueError, 'Retained files reference'):
            self.apply()
        self.assertTrue(self.asset.exists())

    def test_changed_candidate_refuses_removal(self):
        self.audit()
        self.asset.write_bytes(b'changed asset')
        with self.assertRaisesRegex(ValueError, 'Candidate changed'):
            self.apply()
        self.assertTrue(self.asset.exists())

    def test_single_pack_keeps_references_from_other_candidate_packs(self):
        relative = 'Assets/0 - UnityPackages/Fab/Other/retained.prefab'
        other = self.project / relative
        other.parent.mkdir(parents=True)
        other.write_text('m_Texture: {guid: ' + self.guid + '}')
        Path(str(other) + '.meta').write_text('guid: 123456789abcdef0123456789abcdef0\n')
        self.audit()
        report_path = self.project / 'Library/LitDownloadedAssetAudit.json'
        report = json.loads(report_path.read_text())
        report['candidates'].append(dict(path=relative, guid='123456789abcdef0123456789abcdef0', bytes=other.stat().st_size))
        report_path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'Retained files reference'):
            self.apply(pack='Test')
        self.assertTrue(self.asset.exists())
        self.assertTrue(other.exists())

    def test_code_only_change_with_same_asset_references_is_allowed(self):
        source = self.project / 'Assets/Movement.cs'
        source.write_text('float speed = 1;')
        self.audit()
        report_path = self.project / 'Library/LitDownloadedAssetAudit.json'
        report = json.loads(report_path.read_text())
        report['codeReferences'] = [dict(path='Assets/Movement.cs', tokens=[])]
        report_path.write_text(json.dumps(report))
        source.write_text('float speed = 2;')
        os.utime(source, (datetime.datetime.now().timestamp() + 1,) * 2)
        self.apply()
        self.assertFalse(self.asset.exists())
        self.assertEqual(source.read_text(), 'float speed = 2;')

    def test_new_code_asset_reference_refuses_removal(self):
        source = self.project / 'Assets/Loader.cs'
        source.write_text('float speed = 1;')
        self.audit()
        report_path = self.project / 'Library/LitDownloadedAssetAudit.json'
        report = json.loads(report_path.read_text())
        report['codeReferences'] = [dict(path='Assets/Loader.cs', tokens=[])]
        report_path.write_text(json.dumps(report))
        source.write_text('string path = "' + self.relative + '";')
        os.utime(source, (datetime.datetime.now().timestamp() + 1,) * 2)
        with self.assertRaisesRegex(ValueError, 'Dependencies changed'):
            self.apply()
        self.assertTrue(self.asset.exists())

    def test_reuses_archive_without_recopying(self):
        self.audit()
        self.archive.mkdir()
        entries = []
        with zipfile.ZipFile(self.archive / 'assets.zip', 'w') as output:
            for path in (self.asset, self.meta):
                relative = path.relative_to(self.project).as_posix()
                output.write(path, relative)
                entries.append(dict(path=relative, sha256=quarantine.digest(path), bytes=path.stat().st_size))
        (self.archive / 'manifest.json').write_text(json.dumps(dict(project=str(self.project), files=entries, removed=[])))
        self.apply(resume=True)
        self.assertFalse(self.asset.exists())

    def test_restore_refuses_to_overwrite_new_work(self):
        self.audit()
        self.apply()
        self.asset.write_bytes(b'new work after cleanup')
        with patch.object(sys, 'argv', ['restore', str(self.archive)]):
            with self.assertRaisesRegex(ValueError, 'Refusing to overwrite changed asset'):
                restore.run()
        self.assertEqual(self.asset.read_bytes(), b'new work after cleanup')

    def test_top_level_downloaded_pack_is_supported(self):
        relative = 'Assets/Werewolf/unused.png'
        asset = self.project / relative
        asset.parent.mkdir(parents=True)
        self.asset.replace(asset)
        meta = Path(str(asset) + '.meta')
        self.meta.replace(meta)
        self.relative, self.asset, self.meta = relative, asset, meta
        self.audit()
        report_path = self.project / 'Library/LitDownloadedAssetAudit.json'
        report = json.loads(report_path.read_text())
        report.update(scope='downloaded-packs-v2', scopes=['Assets/Werewolf/'])
        report_path.write_text(json.dumps(report))
        self.apply()
        self.assertFalse(asset.exists())

    def test_unknown_scope_is_refused(self):
        self.audit()
        report_path = self.project / 'Library/LitDownloadedAssetAudit.json'
        report = json.loads(report_path.read_text())
        report.update(scope='downloaded-packs-v2', scopes=['Assets/Characters/'])
        report_path.write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError, 'Unexpected audit scope'):
            self.apply()
        self.assertTrue(self.asset.exists())

    def test_open_source_unity_project_refuses_removal(self):
        self.audit()
        (self.project / 'Temp').mkdir()
        (self.project / 'Temp/UnityLockfile').write_text('locked')
        with self.assertRaisesRegex(ValueError, 'Close Unity'):
            self.apply()
        self.assertTrue(self.asset.exists())

    def test_imported_file_with_pre_1980_timestamp_can_be_archived(self):
        self.audit()
        os.utime(self.asset, (0, 0))
        self.apply()
        self.assertFalse(self.asset.exists())

    def test_partial_archive_can_resume_after_copy_failure(self):
        self.audit()
        self.archive.mkdir()
        with zipfile.ZipFile(self.archive / 'assets.zip', 'w') as output:
            output.write(self.asset, self.relative)
        self.apply(resume=True)
        self.assertFalse(self.asset.exists())
        with zipfile.ZipFile(self.archive / 'assets.zip') as saved:
            self.assertIn(self.relative + '.meta', saved.namelist())


if __name__ == '__main__':
    unittest.main()
