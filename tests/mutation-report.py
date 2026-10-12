#!/usr/bin/env python3
"""Читает JSON-отчёт Stryker и печатает короткую сводку: оценка по файлам и выжившие мутанты.

Выживший мутант это испорченная версия кода, которую тесты НЕ заметили: каждый такой случай
показывает место, где проверки слабее, чем кажется. Запуск: python3 tests/mutation-report.py <папка StrykerOutput> [сколько выживших показать]
"""
import glob
import json
import os
import sys

COUNTED = ("Killed", "Timeout", "Survived", "NoCoverage")


def newest_report(root):
    paths = glob.glob(os.path.join(root, "**", "mutation-report.json"), recursive=True)
    return max(paths, key=os.path.getmtime) if paths else None


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


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "StrykerOutput"
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else 60

    path = newest_report(root)
    if not path:
        print("Отчёт Stryker не найден в папке " + root)
        return

    with open(path, encoding="utf-8") as handle:
        report = json.load(handle)

    totals = {name: 0 for name in COUNTED}
    rows = []
    survivors = []
    for file_name, info in sorted(report.get("files", {}).items()):
        counts = {name: 0 for name in COUNTED}
        source_lines = str(info.get("source", "")).splitlines()
        for mutant in info.get("mutants", []):
            status = mutant.get("status")
            if status in counts:
                counts[status] += 1
            if status in ("Survived", "NoCoverage"):
                location = mutant.get("location", {})
                line = location.get("start", {}).get("line", 0)
                survivors.append(
                    (
                        status,
                        os.path.basename(file_name),
                        line,
                        mutant.get("mutatorName", "?"),
                        short(original_text(source_lines, location), 60),
                        short(mutant.get("replacement", ""), 60),
                    )
                )
        rows.append((os.path.basename(file_name), counts))
        for name in COUNTED:
            totals[name] += counts[name]

    def score(counts):
        caught = counts["Killed"] + counts["Timeout"]
        total = caught + counts["Survived"] + counts["NoCoverage"]
        return (100.0 * caught / total) if total else 0.0

    print("## Проверка тестов мутациями (Stryker)")
    print()
    print("Оценка по файлам. Убито: тесты заметили порчу. Выжило: не заметили. Не покрыто: ни один тест не заходит в это место.")
    print()
    print("| Файл | Убито | Выжило | Не покрыто | Время вышло | Оценка |")
    print("|---|---|---|---|---|---|")
    for name, counts in rows:
        print(
            "| %s | %d | %d | %d | %d | %.1f%% |"
            % (name, counts["Killed"], counts["Survived"], counts["NoCoverage"], counts["Timeout"], score(counts))
        )
    print(
        "| **Всего** | %d | %d | %d | %d | **%.1f%%** |"
        % (totals["Killed"], totals["Survived"], totals["NoCoverage"], totals["Timeout"], score(totals))
    )

    if survivors:
        print()
        print("Выжившие мутанты (первые %d из %d): файл:строка, что сделали с кодом." % (min(limit, len(survivors)), len(survivors)))
        print()
        for status, file_name, line, mutator, before, after in survivors[:limit]:
            tag = "не покрыто" if status == "NoCoverage" else "выжил"
            print("- %s:%s [%s] %s: «%s» заменено на «%s»" % (file_name, line, tag, mutator, before, after))


if __name__ == "__main__":
    main()
