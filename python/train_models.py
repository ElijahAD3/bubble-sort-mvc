import csv
import io
import json
import math
import sys


def read_dataset(content):
    reader = csv.reader(io.StringIO(content, newline=""), strict=True)
    try:
        rows = list(reader)
    except csv.Error as error:
        raise ValueError(f"Invalid CSV: {error}") from error
    if not rows:
        raise ValueError("The CSV is empty.")
    headers = [header.strip() for header in rows[0]]
    if not 3 <= len(headers) <= 20 or any(not name or len(name) > 100 for name in headers):
        raise ValueError("Use 3 to 20 nonempty column names of at most 100 characters.")
    if len(set(headers)) != len(headers):
        raise ValueError("Column names must be unique.")
    data = rows[1:]
    if not 30 <= len(data) <= 2000:
        raise ValueError("The CSV must contain 30 to 2,000 data rows.")
    if any(len(row) != len(headers) or any(len(cell) > 256 for cell in row) for row in data):
        raise ValueError("Each row must have the same number of columns; values are limited to 256 characters.")
    return headers, [[cell.strip() for cell in row] for row in data]


def train(headers, rows, class_target, regression_target):
    import numpy as np
    import pandas as pd
    import torch
    from sklearn.compose import ColumnTransformer
    from sklearn.ensemble import RandomForestClassifier
    from sklearn.impute import SimpleImputer
    from sklearn.linear_model import LinearRegression
    from sklearn.metrics import accuracy_score, mean_squared_error
    from sklearn.model_selection import train_test_split
    from sklearn.pipeline import make_pipeline
    from sklearn.preprocessing import LabelEncoder, OneHotEncoder, StandardScaler
    from torch import nn

    if class_target not in headers or regression_target not in headers or class_target == regression_target:
        raise ValueError("Choose two different columns for classification and regression.")
    frame = pd.DataFrame(rows, columns=headers)
    classes = frame[class_target]
    counts = classes.value_counts()
    if classes.eq("").any() or not 2 <= len(counts) <= 20 or counts.min() < 2:
        raise ValueError("The classification target needs 2 to 20 nonempty classes with at least two rows each.")
    numeric_target = pd.to_numeric(frame[regression_target], errors="coerce")
    if numeric_target.isna().any() or not np.isfinite(numeric_target).all() or (numeric_target.abs() > 1e12).any():
        raise ValueError("The regression target must contain finite numbers between -1e12 and 1e12.")
    test_count = max(math.ceil(len(frame) * 0.2), len(counts))
    if len(frame) - test_count < len(counts):
        raise ValueError("There are too few rows for a stratified train/test split.")

    features = frame.drop(columns=[class_target, regression_target]).replace("", np.nan)
    numeric_columns, categorical_columns = [], []
    for column in features.columns:
        converted = pd.to_numeric(features[column], errors="coerce")
        if converted.notna().sum() == features[column].notna().sum():
            if not np.isfinite(converted.dropna()).all() or (converted.dropna().abs() > 1e12).any():
                raise ValueError("Numeric feature values must be finite and between -1e12 and 1e12.")
            features[column] = converted
            numeric_columns.append(column)
        else:
            if features[column].nunique() > 32:
                raise ValueError("Each categorical feature may have at most 32 distinct values.")
            categorical_columns.append(column)
    if len(numeric_columns) + sum(features[column].nunique() + 1 for column in categorical_columns) > 128:
        raise ValueError("The dataset expands to too many features; reduce category counts.")

    train_idx, test_idx = train_test_split(
        np.arange(len(frame)), test_size=test_count, random_state=42, stratify=classes
    )
    preprocess = ColumnTransformer([
        ("numeric", make_pipeline(SimpleImputer(strategy="constant", fill_value=0), StandardScaler()), numeric_columns),
        ("categorical", make_pipeline(
            SimpleImputer(strategy="constant", fill_value="(missing)"),
            OneHotEncoder(handle_unknown="ignore", sparse_output=False),
        ), categorical_columns),
    ], sparse_threshold=0)
    x_train = np.asarray(preprocess.fit_transform(features.iloc[train_idx]), dtype=np.float32)
    x_test = np.asarray(preprocess.transform(features.iloc[test_idx]), dtype=np.float32)
    if x_train.shape[1] == 0:
        raise ValueError("At least one usable feature column is required.")

    labeler = LabelEncoder()
    labels = labeler.fit_transform(classes)
    y_class_train, y_class_test = labels[train_idx], labels[test_idx]
    targets = numeric_target.to_numpy(dtype=np.float64)
    y_reg_train, y_reg_test = targets[train_idx], targets[test_idx]

    forest = RandomForestClassifier(n_estimators=100, random_state=42, n_jobs=1)
    forest.fit(x_train, y_class_train)
    linear = LinearRegression()
    linear.fit(x_train, y_reg_train)

    torch.manual_seed(42)
    torch.set_num_threads(1)

    class TabularTransformer(nn.Module):
        def __init__(self, feature_count, class_count):
            super().__init__()
            self.projection = nn.Linear(1, 16)
            self.columns = nn.Parameter(torch.randn(1, feature_count, 16) * 0.02)
            layer = nn.TransformerEncoderLayer(16, 4, dim_feedforward=32, dropout=0.1, batch_first=True)
            self.encoder = nn.TransformerEncoder(layer, num_layers=1, enable_nested_tensor=False)
            self.classifier = nn.Linear(16, class_count)
            self.regressor = nn.Linear(16, 1)

        def forward(self, inputs):
            tokens = self.projection(inputs.unsqueeze(-1)) + self.columns
            pooled = self.encoder(tokens).mean(dim=1)
            return self.classifier(pooled), self.regressor(pooled).squeeze(-1)

    model = TabularTransformer(x_train.shape[1], len(labeler.classes_))
    optimizer = torch.optim.AdamW(model.parameters(), lr=0.001)
    class_loss = nn.CrossEntropyLoss()
    regression_loss = nn.MSELoss()
    mean, std = float(y_reg_train.mean()), float(y_reg_train.std())
    if std < 1e-12:
        std = 1.0
    normalized_targets = (y_reg_train - mean) / std
    x_tensor = torch.tensor(x_train)
    class_tensor = torch.tensor(y_class_train, dtype=torch.long)
    reg_tensor = torch.tensor(normalized_targets, dtype=torch.float32)
    for _ in range(15):
        model.train()
        for indices in torch.randperm(len(x_train)).split(32):
            optimizer.zero_grad()
            class_prediction, regression_prediction = model(x_tensor[indices])
            loss = class_loss(class_prediction, class_tensor[indices]) + regression_loss(regression_prediction, reg_tensor[indices])
            loss.backward()
            optimizer.step()

    model.eval()
    with torch.no_grad():
        class_prediction, regression_prediction = model(torch.tensor(x_test))
        transformer_classes = class_prediction.argmax(dim=1).numpy()
        transformer_numbers = regression_prediction.numpy().astype(np.float64) * std + mean
    results = {
        "train_rows": len(train_idx),
        "test_rows": len(test_idx),
        "forest_accuracy": float(accuracy_score(y_class_test, forest.predict(x_test))),
        "linear_rmse": float(np.sqrt(mean_squared_error(y_reg_test, linear.predict(x_test)))),
        "transformer_accuracy": float(accuracy_score(y_class_test, transformer_classes)),
        "transformer_rmse": float(np.sqrt(mean_squared_error(y_reg_test, transformer_numbers))),
        "transformer_optimizer": "AdamW",
        "transformer_epochs": 15,
    }
    if not all(math.isfinite(value) for value in results.values() if isinstance(value, float)):
        raise ValueError("Training produced non-finite metrics. Check the dataset values.")
    return results


def main(request):
    headers, rows = read_dataset(request["csv"])
    if request["operation"] == "inspect":
        return {"columns": headers, "rows": len(rows)}
    if request["operation"] == "train":
        return train(headers, rows, request["classificationTarget"], request["regressionTarget"])
    raise ValueError("Unsupported operation.")


if __name__ == "__main__":
    try:
        result = main(json.load(sys.stdin))
        json.dump(result, sys.stdout, allow_nan=False)
    except ValueError as error:
        json.dump({"error": str(error)}, sys.stdout)
        sys.exit(2)
