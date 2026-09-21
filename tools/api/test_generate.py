"""Regression checks for contract evolution and non-writing CI validation."""
import contextlib
import copy
import io
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

import generate


class GeneratorTests(unittest.TestCase):
    def setUp(self):
        self.contract = json.loads((generate.ROOT / "api/rts.json").read_text())

    def test_new_method_generates_both_sides_and_cleanup(self):
        self.contract["methods"].append({
            "name": "Reset", "providerMethod": "ResetState", "returns": "void",
            "parameters": [], "summary": "Reset <state> & notify.",
        })
        outputs = generate.render(self.contract)
        client = outputs[generate.ROOT / "RelativeTopSpeed/RtsApi.cs"]
        backend = outputs[generate.ROOT / "RelativeTopSpeed/RtsApiBackend.cs"]
        self.assertIn("public void Reset() { EnsureReady(); _Reset.Invoke(); }", client)
        self.assertIn("_Reset = null;", client)
        self.assertIn('AssignMethod(dict, "Reset", ref boundReset);', client)
        self.assertLess(client.index('AssignMethod(dict, "Reset"'), client.index('_GetCruiseSpeed = bound'))
        self.assertIn("Reset &lt;state&gt; &amp; notify.", client)
        self.assertIn('["Reset"] = new Action(RTS.@ResetState)', backend)

    def test_invalid_contracts_fail_before_rendering(self):
        for field, value in (("name", "Load"), ("returns", "UnsupportedType")):
            contract = copy.deepcopy(self.contract)
            contract["methods"][0][field] = value
            with self.assertRaises(ValueError):
                generate.render(contract)
        self.contract["methods"].append(self.contract["methods"][0])
        with self.assertRaises(ValueError):
            generate.render(self.contract)

    def test_check_detects_stale_files_without_writing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copytree(generate.ROOT / "tools/api/templates", root / "tools/api/templates")
            (root / "api").mkdir()
            (root / "api/rts.json").write_text(json.dumps(self.contract))
            (root / "RelativeTopSpeed").mkdir()
            with patch.object(generate, "ROOT", root), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                with patch("sys.argv", ["generate.py"]):
                    self.assertEqual(0, generate.main())
                with patch("sys.argv", ["generate.py", "--check"]):
                    self.assertEqual(0, generate.main())
                    client = root / "RelativeTopSpeed/RtsApi.cs"
                    client.write_text("stale\n")
                    self.assertEqual(1, generate.main())
                    self.assertEqual("stale\n", client.read_text())


if __name__ == "__main__":
    unittest.main()
