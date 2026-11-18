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

For production, configure HTTPS, secure persistent storage for the SQLite database and ASP.NET Core data-protection keys, restrict account registration, and enforce resource quotas and concurrency limits for training. This sample does not provide password recovery or email verification.

After building with `dotnet build`, run `python -m unittest discover -s tests` inside the virtual environment. The web integration tests use mock Ollama and OpenAI-compatible services; they do not verify a real model's classifications.
