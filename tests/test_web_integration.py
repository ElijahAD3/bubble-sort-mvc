import contextlib
import datetime
import http.cookiejar
import json
import os
import pathlib
import re
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = pathlib.Path(__file__).resolve().parents[1]


class OllamaStub(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.headers.get("Transfer-Encoding") == "chunked":
            chunks = []
            while True:
                size = int(self.rfile.readline().strip(), 16)
                if size == 0:
                    self.rfile.readline()
                    break
                chunks.append(self.rfile.read(size))
                self.rfile.read(2)
            raw = b"".join(chunks)
        else:
            raw = self.rfile.read(int(self.headers["Content-Length"]))
        body = json.loads(raw)
        if self.path == "/api/generate":
            values = json.loads(body["prompt"])
        elif self.path == "/v1/chat/completions":
            values = json.loads(body["messages"][-1]["content"])
        else:
            self.send_error(404)
            return
        if values == ["__invalid_classification__"]:
            kind = "unsupported"
        elif values == ["__unavailable__"]:
            self.send_error(503)
            return
        elif all(re.fullmatch(r"[+-]?[0-9]+(?:\.[0-9]+)?", v) for v in values):
            kind = "number"
        else:
            try:
                for value in values:
                    datetime.date.fromisoformat(value)
                kind = "date"
            except ValueError:
                kind = "text"
        classification = json.dumps({"type": kind})
        payload = json.dumps(
            {"response": classification} if self.path == "/api/generate"
            else {"choices": [{"message": {"content": classification}}]}
        ).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, *args):
        pass


class WebIntegrationTests(unittest.TestCase):
    provider = "Ollama"

    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.ollama = ThreadingHTTPServer(("127.0.0.1", 0), OllamaStub)
        cls.thread = threading.Thread(target=cls.ollama.serve_forever, daemon=True)
        cls.thread.start()
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            port = sock.getsockname()[1]
        cls.base = f"http://127.0.0.1:{port}"
        cls.db = str(pathlib.Path(cls.temp.name) / "app.db")
        env = os.environ.copy()
        env.update({
            "ASPNETCORE_ENVIRONMENT": "Development",
            "ASPNETCORE_URLS": cls.base,
            "ConnectionStrings__DefaultConnection": f"Data Source={cls.db}",
            "Ai__Provider": cls.provider,
            "Ai__Endpoint": f"http://127.0.0.1:{cls.ollama.server_port}" + ("/v1" if cls.provider == "OpenAICompatible" else ""),
            "Ai__Model": "test-model",
            "PythonExecutable": os.environ.get("TEST_PYTHON", os.sys.executable),
            "HOME": cls.temp.name,
        })
        cls.app = subprocess.Popen(
            [os.environ.get("DOTNET_EXE", "dotnet"), "run", "--no-build", "--no-launch-profile"],
            cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        for _ in range(150):
            if cls.app.poll() is not None:
                cls.tearDownClass()
                raise RuntimeError("The ASP.NET application exited during startup.")
            try:
                urllib.request.urlopen(cls.base, timeout=1).close()
                return
            except urllib.error.URLError:
                time.sleep(0.1)
        cls.tearDownClass()
        raise RuntimeError("The ASP.NET application did not start.")

    @classmethod
    def tearDownClass(cls):
        cls.app.terminate()
        try:
            cls.app.wait(timeout=10)
        except subprocess.TimeoutExpired:
            cls.app.kill()
            cls.app.wait()
        cls.ollama.shutdown()
        cls.ollama.server_close()
        cls.thread.join()
        cls.temp.cleanup()

    def client(self):
        return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def get(self, client, path):
        with client.open(self.base + path, timeout=10) as response:
            return response.geturl(), response.read().decode()

    def post(self, client, path, values, token_path=None):
        _, page = self.get(client, token_path or path)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        self.assertIsNotNone(token)
        data = urllib.parse.urlencode({**values, "__RequestVerificationToken": token.group(1)}).encode()
        with client.open(self.base + path, data=data, timeout=150) as response:
            return response.geturl(), response.read().decode()

    def upload(self, client, csv_content):
        _, page = self.get(client, "/Training")
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1)
        boundary = uuid.uuid4().hex
        data = (
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"__RequestVerificationToken\"\r\n\r\n{token}\r\n"
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"File\"; filename=\"sample.csv\"\r\n"
            f"Content-Type: text/csv\r\n\r\n{csv_content}\r\n--{boundary}--\r\n"
        ).encode()
        request = urllib.request.Request(self.base + "/Training", data=data,
                                         headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
        with client.open(request, timeout=30) as response:
            return response.geturl(), response.read().decode()

    def test_accounts_sorts_persistence_and_failures(self):
        alice, bob = self.client(), self.client()
        url, _ = self.get(alice, "/Sort")
        self.assertIn("/Account/Login", url)

        _, page = self.post(alice, "/Account/Register", {
            "Username": "alice", "Password": "Secure1!", "ConfirmPassword": "Secure1!"
        })
        self.assertIn("Sort values", page)

        for values, expected, kind in [
            ("10\n-2.5\n2", ["-2.5", "2", "10"], "number"),
            ("pear\nApple\nbanana", ["Apple", "banana", "pear"], "text"),
            ("2025-01-01\n2020-02-29", ["2020-02-29", "2025-01-01"], "date"),
        ]:
            _, page = self.post(alice, "/Sort", {"Values": values})
            self.assertIn(f"Sorted result ({kind})", page)
            result = re.search(r'<ol id="sort-result">(.*?)</ol>', page, re.S).group(1)
            self.assertEqual(re.findall(r"<li>(.*?)</li>", result), expected)

        for value in ("__invalid_classification__", "__unavailable__"):
            _, page = self.post(alice, "/Sort", {"Values": value})
            self.assertIn("classification or sorting service failed", page)

        _, page = self.post(alice, "/Sort", {"Values": "1\n\n2"})
        self.assertIn("nonempty values", page)
        _, history = self.get(alice, "/Sort/History")
        self.assertIn("pear", history)
        self.assertIn("2020-02-29", history)
        self.assertIn("-2.5", history)

        self.post(bob, "/Account/Register", {
            "Username": "bob", "Password": "Secure1!", "ConfirmPassword": "Secure1!"
        })
        _, history = self.get(bob, "/Sort/History")
        self.assertIn("no saved sorts", history)
        self.assertNotIn("pear", history)

        self.post(alice, "/Account/Logout", {}, token_path="/Sort")
        _, page = self.post(alice, "/Account/Login", {"Username": "alice", "Password": "incorrect"})
        self.assertIn("Unable to log in", page)
        _, page = self.post(alice, "/Account/Login", {"Username": "alice", "Password": "Secure1!"})
        self.assertIn("Sort values", page)
        _, history = self.get(alice, "/Sort/History")
        self.assertIn("pear", history)

        with contextlib.closing(sqlite3.connect(self.db)) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM SavedSorts").fetchone()[0], 3)
            password_hash = db.execute("SELECT PasswordHash FROM AspNetUsers WHERE UserName = 'alice'").fetchone()[0]
            self.assertNotEqual(password_hash, "Secure1!")

    def test_dataset_training_results_and_ownership(self):
        owner, other = self.client(), self.client()
        self.post(owner, "/Account/Register", {
            "Username": "trainer", "Password": "Secure1!", "ConfirmPassword": "Secure1!"
        })
        self.post(other, "/Account/Register", {
            "Username": "other", "Password": "Secure1!", "ConfirmPassword": "Secure1!"
        })
        csv_content = "value,color,label,target\n" + "".join(
            f"{i},{'red' if i % 2 else 'blue'},{'yes' if i % 2 else 'no'},{i * 2}\n"
            for i in range(60)
        )
        url, page = self.upload(owner, csv_content)
        self.assertIn("Select targets", page)
        dataset_id = int(urllib.parse.urlparse(url).path.rsplit("/", 1)[-1])
        with self.assertRaises(urllib.error.HTTPError) as forbidden:
            self.get(other, f"/Training/Download?id={dataset_id}")
        self.assertEqual(forbidden.exception.code, 404)
        _, downloaded = self.get(owner, f"/Training/Download?id={dataset_id}")
        self.assertEqual(downloaded, csv_content)

        _, page = self.post(owner, f"/Training/Train?id={dataset_id}", {
            "ClassificationTarget": "label", "RegressionTarget": "label"
        })
        self.assertIn("two different columns", page)
        url, page = self.post(owner, f"/Training/Train?id={dataset_id}", {
            "ClassificationTarget": "label", "RegressionTarget": "target"
        })
        self.assertIn("Random forest", page)
        self.assertIn("Linear regression", page)
        self.assertIn("Tabular transformer", page)
        run_id = int(urllib.parse.urlparse(url).path.rsplit("/", 1)[-1])
        with self.assertRaises(urllib.error.HTTPError) as forbidden:
            self.get(other, f"/Training/Result?id={run_id}")
        self.assertEqual(forbidden.exception.code, 404)
        _, page = self.get(owner, "/Training/History")
        self.assertIn("sample.csv", page)
        _, page = self.get(other, "/Training/History")
        self.assertNotIn("sample.csv", page)


class OpenAICompatibleIntegrationTests(WebIntegrationTests):
    provider = "OpenAICompatible"


if __name__ == "__main__":
    unittest.main()
