import importlib.util
import math
import pathlib
import unittest

SCRIPT = pathlib.Path(__file__).resolve().parents[1] / "python" / "train_models.py"
spec = importlib.util.spec_from_file_location("train_models", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class TrainingTests(unittest.TestCase):
    def dataset(self):
        return "value,category,label,target\n" + "".join(
            f"{i},{'red' if i % 2 else 'blue'},{'high' if i % 2 else 'low'},{i * 2.5}\n"
            for i in range(60)
        )

    def test_training_returns_held_out_metrics(self):
        headers, rows = module.read_dataset(self.dataset())
        result = module.train(headers, rows, "label", "target")
        self.assertEqual(result["train_rows"] + result["test_rows"], 60)
        self.assertGreater(result["test_rows"], 0)
        for key in ("forest_accuracy", "transformer_accuracy"):
            self.assertTrue(0 <= result[key] <= 1)
        for key in ("linear_rmse", "transformer_rmse"):
            self.assertTrue(math.isfinite(result[key]) and result[key] >= 0)
        self.assertGreater(result["forest_accuracy"], 0.9)
        self.assertLess(result["linear_rmse"], 0.01)

    def test_invalid_datasets_and_targets(self):
        with self.assertRaisesRegex(ValueError, "30 to 2,000"):
            module.read_dataset("a,b,c\n1,2,3")
        with self.assertRaisesRegex(ValueError, "unique"):
            module.read_dataset("a,a,c\n" + "1,2,3\n" * 30)
        headers, rows = module.read_dataset(self.dataset())
        with self.assertRaisesRegex(ValueError, "two different columns"):
            module.train(headers, rows, "label", "label")
        with self.assertRaisesRegex(ValueError, "regression target"):
            module.train(headers, rows, "label", "category")


if __name__ == "__main__":
    unittest.main()
