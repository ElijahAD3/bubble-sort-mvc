import importlib.util
import json
import pathlib
import subprocess
import sys
import unittest

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "python" / "bubble_sort.py"
spec = importlib.util.spec_from_file_location("bubble_sort", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class BubbleSortTests(unittest.TestCase):
    def test_sorts_each_type_stably_without_mutating_input(self):
        cases = [
            ("number", ["10", "-2.5", "2", "2.0"], ["-2.5", "2", "2.0", "10"]),
            ("date", ["2025-01-01", "2020-02-29", "2023-12-31"], ["2020-02-29", "2023-12-31", "2025-01-01"]),
            ("text", ["pear", "Apple", "apple", "Banana"], ["Apple", "apple", "Banana", "pear"]),
        ]
        for kind, values, expected in cases:
            with self.subTest(kind=kind):
                original = values[:]
                self.assertEqual(module.sort_request({"type": kind, "values": values}), expected)
                self.assertEqual(values, original)

    def test_command_protocol_and_invalid_input(self):
        request = {"type": "number", "values": ["3", "-1", "3"]}
        result = subprocess.run([sys.executable, SCRIPT], input=json.dumps(request), text=True, capture_output=True, check=True)
        self.assertEqual(json.loads(result.stdout), ["-1", "3", "3"])
        invalid = subprocess.run([sys.executable, SCRIPT], input='{"type":"unknown","values":["1"]}', text=True, capture_output=True)
        self.assertNotEqual(invalid.returncode, 0)


if __name__ == "__main__":
    unittest.main()
