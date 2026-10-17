"""Reference-model tests; run with python -m unittest discover -s tests -p test_goatsim_roster_filter.py"""
import unittest
from tools.goatsim_roster_filter import UnknownRosterError, visible_actor_ids, filtered_events


class RosterFilterTests(unittest.TestCase):
    def setUp(self):
        self.kw = dict(self_id=10, nearby_ids={10, 20, 30, 40},
                       party_ids={10, 20}, force_ids={10, 20, 30})
        self.events = [
            {"source_id": 10, "target_id": 100, "damage": 100},
            {"source_id": 20, "target_id": 100, "damage": 200},
            {"source_id": 30, "target_id": 100, "damage": 300},
            {"source_id": 40, "target_id": 100, "damage": 400},
            {"source_id": 10, "target_id": 200, "damage": 500},
            {"source_id": None, "target_id": 100, "damage": 999},
        ]

    def test_self_only(self):
        self.assertEqual(visible_actor_ids(scope="self", **self.kw), {10})

    def test_party_excludes_nearby_strangers(self):
        self.assertEqual(visible_actor_ids(scope="party", **self.kw), {10, 20})

    def test_force_includes_nonparty_members(self):
        self.assertEqual(visible_actor_ids(scope="force", **self.kw), {10, 20, 30})

    def test_nearby(self):
        self.assertEqual(visible_actor_ids(scope="nearby", **self.kw), {10, 20, 30, 40})

    def test_unknown_party_fails_closed(self):
        with self.assertRaises(UnknownRosterError):
            visible_actor_ids(scope="party", self_id=10, nearby_ids={10, 20})

    def test_unknown_force_fails_closed(self):
        with self.assertRaises(UnknownRosterError):
            visible_actor_ids(scope="force", self_id=10, nearby_ids={10, 20})

    def test_target_and_source_both_required_for_dummy(self):
        selected = filtered_events(self.events, scope="self", target_id=100, **self.kw)
        self.assertEqual([e["damage"] for e in selected], [100])

    def test_ambiguous_source_is_not_attributed(self):
        selected = filtered_events(self.events, scope="nearby", target_id=100, **self.kw)
        self.assertEqual(sum(e["damage"] for e in selected), 1000)

    def test_filter_does_not_mutate_events(self):
        before = list(self.events)
        filtered_events(self.events, scope="party", **self.kw)
        self.assertEqual(self.events, before)


if __name__ == "__main__":
    unittest.main()
