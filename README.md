# Bubble Sort MVC

An ASP.NET Core MVC application with username/password registration and login. An AI model classifies a list as numbers, dates, or text; Python then uses bubble sort, and each successful sort is saved privately to the user's account.

## Requirements

- .NET 10 SDK
- Python 3.10–3.12 available on the application server
- An AI service: [Ollama](https://ollama.com/) or an OpenAI-compatible chat-completions API with JSON output

## Run locally

Create a Python virtual environment and install the CPU version of PyTorch and the remaining dependencies (the PyTorch command below is for Linux/Windows; see [PyTorch](https://pytorch.org/get-started/locally/) for other platforms):

```sh
python3 -m venv .venv
. .venv/bin/activate
python -m pip install --index-url https://download.pytorch.org/whl/cpu torch==2.8.0
python -m pip install -r requirements.txt
export PythonExecutable="$PWD/.venv/bin/python"
```

On Windows, activate `.venv\Scripts\activate` and set `PythonExecutable` to the environment's Python executable instead. Both sorting and training use this interpreter.

For the default free local AI setup, start Ollama and download the model with `ollama pull llama3.2:1b`. Then run:

```sh
dotnet restore
dotnet run
```

Open the printed URL, register, and enter up to 100 values, one per line. Numbers use a decimal point; dates must be valid ISO dates (`YYYY-MM-DD`); text is sorted case-insensitively. The AI chooses the type; numeric and date values are checked before sorting, and AI classifications can be mistaken. Lists are stored with their original and sorted values in SQLite. They are only displayed to the owning account.

The SQLite database is automatically migrated as `app.db` in the application's working directory. Accounts use ASP.NET Core Identity's password hashing and lockout.

Configure the interpreter using `PythonExecutable` (for example `python` on Windows). Python runs on the server, not in the browser. The Python script is included in published output.

The appearance controls default to the browser's system light/dark setting. Users can choose light or dark and a blue, purple, or teal accent; choices are stored in that browser, not in the account.

## AI providers and free models

The default provider is Ollama at `http://127.0.0.1:11434` using `llama3.2:1b`. This model can be downloaded and run locally without a per-request API charge; another free-to-download choice is `qwen2.5:0.5b`. Set `Ai__Model` to the installed model name and run `ollama pull` for that name first. Model licenses and hardware requirements vary; review the model's license before use. Small models can misclassify data.

To use an OpenAI-compatible service such as a local LM Studio or llama.cpp server, start its chat-completions server with a loaded model and set:

```sh
export Ai__Provider=OpenAICompatible
export Ai__Endpoint=http://127.0.0.1:1234/v1
export Ai__Model=your-loaded-model-name
dotnet run
```

The endpoint must support `/chat/completions` and `response_format: {"type":"json_object"}`. For Ollama, set `Ai__Provider=Ollama`, `Ai__Endpoint` to its base URL (without `/api/generate`), and optionally `Ai__Model`. For an OpenAI-compatible provider requiring a key, set `Ai__ApiKey` as an environment variable or use .NET user secrets; never commit keys. Remote providers may charge fees or have limited free tiers.

**Privacy:** entered values are sent to the configured AI endpoint. The default is local; using a remote endpoint sends user data to that provider. Configure only a service you trust. AI is advisory and may classify values incorrectly; numeric and date classifications are validated before sorting.

## Dataset training

The **Train models** page works independently of the AI provider. Upload a UTF-8 CSV with a header, 30–2,000 rows, 3–20 columns, and at most 1 MB. Select different columns for the categorical classification target and numeric regression target; all other columns become features. The classification target must have 2–20 classes, each represented at least twice, and the regression target must contain finite numeric values. Categorical features support at most 32 distinct values each. Numeric values are limited to ±1e12.

The local Python pipeline uses the same deterministic, stratified holdout for all models. Preprocessing is fitted only on training rows. It trains a random forest classifier and a linear regression baseline, plus a two-head tabular transformer optimized with AdamW. The results compare classification accuracy and regression RMSE on the held-out rows. This is a small educational baseline, not a guarantee of predictive quality. Training is limited to two minutes per request.

Uploaded CSV data and metrics are stored in SQLite and can be retrieved from the account's training history. Model weights are not saved. Dataset contents never go to the configured sorting AI provider. SQLite is not encrypted by default; do not upload sensitive data unless the deployment has appropriate safeguards. Opening a downloaded CSV in spreadsheet software can execute formulas embedded in untrusted data.

## Server and Cloudflare preparation

The included Docker Compose setup runs the ASP.NET/Python app, a local Ollama server, and an optional Cloudflare Tunnel. Cloudflare provides the HTTPS entry point; it does **not** run the ASP.NET app, Python training, or SQLite database. You need a Linux VPS or server with Docker Compose, a Cloudflare account, and a domain managed by Cloudflare. A starting point for the included CPU-only setup is 4 vCPUs, 8 GB RAM, and 25 GB disk; actual requirements depend on datasets, model size, and traffic.

On the server, clone the repository, then:

```sh
cp .env.example .env
chmod 600 .env
docker compose up -d --build
docker compose exec ollama ollama pull llama3.2:1b
```

In the Cloudflare dashboard, create a **remotely managed tunnel** and configure a public hostname whose service URL is `http://app:8080`. Put the tunnel token in `CLOUDFLARE_TUNNEL_TOKEN` in `.env`, then start it:

```sh
docker compose --profile cloudflare up -d
docker compose logs -f app cloudflared
```

Open the configured **HTTPS** hostname. The app port is bound only to the server's loopback interface; it is not intended as a public HTTP entry point. Production cookies require HTTPS. The tunnel container is the only proxy whose forwarded headers the app trusts, at `172.30.193.2` on the Compose network. If that subnet conflicts with your server, change both the Compose subnet/address and `ReverseProxy__KnownProxy` together. Keep the token and `.env` private; never commit them.

The `app_data` volume holds both the SQLite database and ASP.NET data-protection keys, and `ollama_data` holds downloaded models. Back up both the database and keys securely; stop the app before copying the SQLite files for a consistent backup. Do not remove the volumes if you need to retain users and data. For an external AI provider, set `AI_PROVIDER`, `AI_ENDPOINT`, `AI_MODEL`, and, if required, `AI_API_KEY` in `.env`; entered sorting data will be sent to that provider.

This is a deployment starting point, not a fully hardened public service. The app rate-limits login/registration by IP and allows one training job at a time per app process. For real public use, add authentication and registration protections such as Cloudflare Access, durable job processing, stronger quotas, monitoring, backups, and appropriate security/privacy controls. Long synchronous training requests may exceed Cloudflare's request timeout. The sample does not provide password recovery or email verification, and its SQLite data-protection keys are not encrypted at rest by default.

After building with `dotnet build`, run `python -m unittest discover -s tests` inside the virtual environment and `node --test tests/appearance.test.cjs` for appearance controls. The web integration tests use mock Ollama and OpenAI-compatible services; they do not verify a real model's classifications.
