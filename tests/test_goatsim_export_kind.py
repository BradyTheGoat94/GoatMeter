import unittest
from tools.goatsim_evidence_export import build_export

class ExportKindTests(unittest.TestCase):
    def test_explicit_kind(self):
        self.assertTrue(callable(build_export))
