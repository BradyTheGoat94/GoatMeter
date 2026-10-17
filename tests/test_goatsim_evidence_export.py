"""Tests for the privacy-minimized GoatSim export contract."""
import unittest

from tools.goatsim_evidence_export import ExportError, build_export, preview_export, serialize_export


PROFILE = {
    "class": "Templar",
    "level": 45,
    "name": "Bradyboi",
    "account_id": "PRIVATE",
    "stats": {"attack": 1400, "critical_rate": 7.7, "unknown": None},
}
ENCOUNTERS = [{
    "start": "2026-10-17T10:00:00Z",
    "end": "2026-10-17T10:00:10Z",
    "chat": "PRIVATE",
    "events": [{
        "timestamp": "2026-10-17T10:00:01Z",
        "skill": "Punishing Strike",
        "damage": 1234,
        "source_role": "self",
        "target_role": "boss",
        "source_actor_id": 2005,
        "target_actor_id": 3000,
        "player_name": "PRIVATE",
        "ip": "127.0.0.1",
    }],
}]


class EvidenceExportTests(unittest.TestCase):
    def test_explicit_consent_required(self):
        with self.assertRaises(ExportError):
            build_export(PROFILE, ENCOUNTERS)

    def test_minimization(self):
        payload = serialize_export(build_export(PROFILE, ENCOUNTERS, consent=True))
        for secret in (b"Bradyboi", b"PRIVATE", b"source_actor_id", b"target_actor_id", b"127.0.0.1"):
            self.assertNotIn(secret, payload)
        self.assertIn(b"Punishing Strike", payload)

    def test_preview(self):
        result = build_export(PROFILE, ENCOUNTERS, consent=True)
        preview = preview_export(result)
        self.assertEqual(preview["events"], 1)
        self.assertEqual(preview["encounters"], 1)
        self.assertEqual(preview["class"], "Templar")

    def test_invalid_class(self):
        with self.assertRaises(ExportError):
            build_export({**PROFILE, "class": "Unknown"}, ENCOUNTERS, consent=True)

    def test_invalid_timestamp(self):
        with self.assertRaises(ExportError):
            build_export(PROFILE, [{**ENCOUNTERS[0], "start": "not-a-date"}], consent=True)

    def test_negative_damage(self):
        bad = [{**ENCOUNTERS[0], "events": [{**ENCOUNTERS[0]["events"][0], "damage": -1}]}]
        with self.assertRaises(ExportError):
            build_export(PROFILE, bad, consent=True)

    def test_event_cap(self):
        with self.assertRaises(ExportError):
            build_export(PROFILE, [{**ENCOUNTERS[0], "events": [ENCOUNTERS[0]["events"][0]] * 100001}], consent=True)


if __name__ == "__main__":
    unittest.main()
