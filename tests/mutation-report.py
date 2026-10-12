#!/usr/bin/env python3
"""Собирает из JSON-отчётов Stryker короткую сводку: оценка по файлам, виды порчи и выжившие мутанты.

Выживший мутант это испорченная версия кода, которую тесты НЕ заметили: каждый такой случай
показывает место, где проверки слабее, чем кажется. Отчётов может быть несколько (каждый файл сайта
проверяется отдельным заданием), скрипт находит все mutation-report.json под указанной папкой и объединяет их.

Запуск: python3 tests/mutation-report.py <папка с отчётами> [сколько выживших показать]
"""
import glob
import json
import os
import sys
from collections import Counter

COUNTED = ("Killed", "Timeout", "Survived", "NoCoverage")


def reports(root):
    return sorted(glob.glob(os.path.join(root, "**", "mutation-report.json"), recursive=True))


def short(text, limit=90):
    text = " ".join(str(text).split())
    return text if len(text) <= limit else text[: limit - 1] + "…"


def original_text(source_lines, location):
    try:
        start, end = location["start"], location["end"]
        if start["line"] == end["line"]:
            return source_lines[start["line"] - 1][start["column"] - 1 : end["column"] - 1]
        return source_lines[start["line"] - 1].strip()
    except (KeyError, IndexError, TypeError):
        return ""


def score(counts):
    caught = counts["Killed"] + counts["Timeout"]
    total = caught + counts["Survived"] + counts["NoCoverage"]
    return (100.0 * caught / total) if total else 0.0


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "StrykerOutput"
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 60

    paths = reports(root)
    if not paths:
        print("Отчёты Stryker не найдены в папке " + root)
        return

    per_file = {}
    survivors = []
    by_mutator = {}
    for path in paths:
        with open(path, encoding="utf-8") as handle:
            report = json.load(handle)
        for file_name, info in report.get("files", {}).items():
            name = os.path.basename(file_name)
            source_lines = str(info.get("source", "")).splitlines()
            for mutant in info.get("mutants", []):
                status = mutant.get("status")
                if status not in COUNTED:
                    continue
                counts = per_file.setdefault(name, {key: 0 for key in COUNTED})
                counts[status] += 1
                mutator = mutant.get("mutatorName", "?")
                stats = by_mutator.setdefault(mutator, Counter())
                stats[status] += 1
                if status in ("Survived", "NoCoverage"):
                    location = mutant.get("location", {})
                    survivors.append(
                        (
                            name,
                            location.get("start", {}).get("line", 0),
                            status,
                            mutator,
                            short(original_text(source_lines, location), 60),
                            short(mutant.get("replacement", ""), 60),
                        )
                    )

    totals = {key: 0 for key in COUNTED}
    for counts in per_file.values():
        for key in COUNTED:
            totals[key] += counts[key]

    print("## Проверка тестов мутациями (Stryker)")
    print()
    print("Оценка по файлам. Убито: тесты заметили порчу. Выжило: не заметили. Не покрыто: ни один тест не заходит в это место.")
    print()
    print("| Файл | Убито | Выжило | Не покрыто | Время вышло | Оценка |")
    print("|---|---|---|---|---|---|")
    for name in sorted(per_file):
        counts = per_file[name]
        print(
            "| %s | %d | %d | %d | %d | %.1f%% |"
            % (name, counts["Killed"], counts["Survived"], counts["NoCoverage"], counts["Timeout"], score(counts))
        )
    print(
        "| **Всего** | %d | %d | %d | %d | **%.1f%%** |"
        % (totals["Killed"], totals["Survived"], totals["NoCoverage"], totals["Timeout"], score(totals))
    )

    print()
    print("Какие виды порчи выживают чаще всего.")
    print()
    print("| Вид порчи | Убито | Выжило | Не покрыто |")
    print("|---|---|---|---|")
    for mutator, stats in sorted(by_mutator.items(), key=lambda item: -(item[1]["Survived"] + item[1]["NoCoverage"])):
        print("| %s | %d | %d | %d |" % (mutator, stats["Killed"] + stats["Timeout"], stats["Survived"], stats["NoCoverage"]))

    survivors.sort(key=lambda item: (item[0], item[1]))
    if survivors:
        print()
        print("Выжившие мутанты (первые %d из %d): файл:строка, что сделали с кодом." % (min(limit, len(survivors)), len(survivors)))
        print()
        for name, line, status, mutator, before, after in survivors[:limit]:
            tag = "не покрыто" if status == "NoCoverage" else "выжил"
            print("- %s:%s [%s] %s: «%s» заменено на «%s»" % (name, line, tag, mutator, before, after))


if __name__ == "__main__":
    main()
