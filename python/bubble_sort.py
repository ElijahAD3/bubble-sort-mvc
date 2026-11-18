import json
import sys
from datetime import date
from decimal import Decimal


def bubble_sort(values, key=lambda value: value):
    result = list(values)
    for end in range(len(result) - 1, 0, -1):
        swapped = False
        for index in range(end):
            if key(result[index]) > key(result[index + 1]):
                result[index], result[index + 1] = result[index + 1], result[index]
                swapped = True
        if not swapped:
            break
    return result


def sort_request(request):
    values = request["values"]
    if not isinstance(values, list) or not 1 <= len(values) <= 100 or any(
        not isinstance(value, str) or not value or len(value) > 200 for value in values
    ):
        raise ValueError("Expected 1 to 100 nonempty strings of at most 200 characters.")
    keys = {"number": Decimal, "date": date.fromisoformat, "text": str.casefold}
    if request["type"] not in keys:
        raise ValueError("Unsupported data type.")
    return bubble_sort(values, keys[request["type"]])


if __name__ == "__main__":
    json.dump(sort_request(json.load(sys.stdin)), sys.stdout)
