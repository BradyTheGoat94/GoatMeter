import copy
import json
import tempfile
import unittest
from pathlib import Path
from audit_public_scores import audit

class PublicScoreAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "fight.json"
        self.data = {"record": {"startTimeMs": 1791659393080, "actors": [
            {"actorId": 11551, "serverId": 2201, "nickname": "Example",
             "gearScore": 2115, "combatPower": 113952}]}, "parserVersion": "fixture"}
    def run_audit(self):
        self.path.write_text(json.dumps(self.data))
        return audit(self.path, "https://example.test/report")
    def test_historical_scores_never_authorize_live_join(self):
        result = self.run_audit()
        self.assertFalse(result["live_import_allowed"])
        self.assertFalse(result["decoder_independently_verified"])
        self.assertFalse(result["players"][0]["eligible_for_live_identity_join"])
        self.assertIsNone(result["players"][0]["stable_character_id"])
        self.assertEqual(result["fight_started_utc"], "2026-10-10T19:09:53.080000+00:00")
    def test_masked_names_and_zero_unknown_scores(self):
        self.data["record"]["actors"][0].update(nickname="Ex****e", gearScore=0, combatPower=0)
        row = self.run_audit()["players"][0]
        self.assertTrue(row["masked"])
        self.assertIsNone(row["gear_score"])
        self.assertIsNone(row["combat_power"])
    def test_invalid_values_rejected(self):
        original = copy.deepcopy(self.data)
        for field, value in [("gearScore", True), ("combatPower", -1),
                             ("gearScore", 50001), ("serverId", 0), ("actorId", "11551")]:
            with self.subTest(field=field, value=value):
                self.data = copy.deepcopy(original)
                self.data["record"]["actors"][0][field] = value
                with self.assertRaises(ValueError): self.run_audit()
    def test_duplicate_identity_rejected(self):
        self.data["record"]["actors"].append(copy.deepcopy(self.data["record"]["actors"][0]))
        with self.assertRaises(ValueError): self.run_audit()
    def test_oversized_input_rejected_before_parse(self):
        with self.path.open("wb") as file: file.truncate(5 * 1024 * 1024 + 1)
        with self.assertRaises(ValueError): audit(self.path, "https://example.test/report")

if __name__ == "__main__": unittest.main()
