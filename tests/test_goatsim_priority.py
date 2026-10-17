"""Tests for the experimental GoatSim priority scheduler.

Run from repository root: python -m unittest discover -s tests -p 'test_goatsim_priority.py'
"""
import unittest
from tools.goatsim_priority import Skill, choose_skill, simulate_priority


class PriorityTests(unittest.TestCase):
    def test_first_ready_skill_wins(self):
        skills = [Skill("high", 3000, 300), Skill("low", 0, 100)]
        self.assertEqual(choose_skill(skills, 0, {}), skills[0])
        self.assertEqual(choose_skill(skills, 100, {"high": 3000}), skills[1])

    def test_conditions_block_skill(self):
        skills = [Skill("proc", 1000, 500, "judgment"), Skill("fallback", 1000, 100)]
        self.assertEqual(choose_skill(skills, 0, {}), skills[1])
        self.assertEqual(choose_skill(skills, 0, {}, {"judgment"}), skills[0])

    def test_default_delay_and_damage(self):
        result = simulate_priority([Skill("strike", 1000, 150)], 3000)
        self.assertEqual(result["delay_ms"], 10)
        self.assertEqual([e["time_ms"] for e in result["events"]], [0, 1000, 2000])
        self.assertEqual(result["estimated_damage"], 450)

    def test_twenty_ms_preset(self):
        self.assertEqual(simulate_priority([Skill("hit", 0, 1)], 100, delay_ms=20,
                                           action_lock_ms=20)["estimated_damage"], 5)

    def test_reject_unsupported_delay(self):
        with self.assertRaises(ValueError):
            simulate_priority([], 1000, delay_ms=50)

    def test_reject_negative_duration(self):
        with self.assertRaises(ValueError):
            simulate_priority([], -1)

    def test_reject_duplicate_skill_names(self):
        with self.assertRaises(ValueError):
            simulate_priority([Skill("same", 100), Skill("same", 200)], 1000)

    def test_cooldown_and_action_lock(self):
        result = simulate_priority([Skill("big", 3000, 300), Skill("filler", 0, 50)],
                                   5000, action_lock_ms=1000)
        self.assertEqual([e["skill"] for e in result["events"]],
                         ["big", "filler", "filler", "big", "filler"])

    def test_no_auto_input_side_effect(self):
        result = simulate_priority([], 1000)
        self.assertEqual(result["events"], [])
        self.assertEqual(result["model"], "unvalidated-priority-baseline")


if __name__ == "__main__":
    unittest.main()
