import unittest
from tools.goatsim_log_audit import audit


class CombatLogAuditTests(unittest.TestCase):
    def test_actor_totals_and_identity(self):
        lines = [
            "2026-10-17T18:00:00Z|tag=identityMap|entity=42|name=Example|class=Templar\n",
            "2026-10-17T18:00:01Z|tag=damage|src=42|tgt=100|skill=Hit|amount=100|raw=abc\n",
            "2026-10-17T18:00:03Z|tag=damage|src=42|tgt=100|skill=Hit|amount=200|raw=def\n",
            "2026-10-17T18:00:02Z|tag=damage|src=99|tgt=100|skill=Hit|amount=900|raw=ghi\n",
        ]
        result = audit(lines, actor="42")
        self.assertEqual(len(result["actors"]), 1)
        actor = result["actors"][0]
        self.assertEqual(actor["name"], "Example")
        self.assertEqual(actor["damage"], 300)
        self.assertEqual(actor["events"], 2)
        self.assertEqual(actor["first_to_last_dps"], 150)

    def test_repeat_candidate_is_not_automatically_removed(self):
        line = "2026-10-17T18:00:01Z|tag=damage|src=42|tgt=100|skill=Hit|amount=100|raw=abc\n"
        result = audit([line, line])
        self.assertEqual(result["exact_repeat_candidates"], 1)
        self.assertEqual(result["actors"][0]["damage"], 200)

    def test_bad_records_are_ignored(self):
        result = audit(["bad\n", "2026-10-17T18:00:01Z|tag=damage|src=42|amount=-10\n"])
        self.assertEqual(result["actors"], [])


if __name__ == "__main__":
    unittest.main()
