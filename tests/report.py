#!/usr/bin/env python3
"""Собирает из файлов TRX (результатов dotnet test) короткий отчёт в формате Markdown.

Падения идут первыми, с причиной; пройденные тесты спрятаны под спойлер. Отчёт попадает на страницу запуска
в GitHub Actions. Запуск: python3 tests/report.py <папка с .trx>
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def read(path):
    results = []
    for node in ET.parse(path).getroot().iter("{%s}UnitTestResult" % NS["t"]):
        message = ""
        info = node.find("t:Output/t:ErrorInfo/t:Message", NS)
        if info is not None and info.text:
            message = " ".join(info.text.split())
        results.append((node.get("testName", "?"), node.get("outcome", "?"), message))
    return results


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else "test-results"
    suites = {}
    for path in sorted(glob.glob(os.path.join(folder, "*.trx"))):
        suites[os.path.splitext(os.path.basename(path))[0]] = read(path)

    if not suites:
        print("## Отчёт о тестах\n\nФайлов с результатами нет: тесты, скорее всего, не запустились.")
        return

    names = {"api": "API-тесты", "e2e": "Браузерные тесты"}
    total = failed = skipped = 0
    for results in suites.values():
        total += len(results)
        failed += sum(1 for _, outcome, _ in results if outcome == "Failed")
        skipped += sum(1 for _, outcome, _ in results if outcome == "NotExecuted")

    print("## Отчёт о тестах\n")
    print("Всего: **%d**, упало: **%d**, пропущено: **%d**, прошло: **%d**\n" % (total, failed, skipped, total - failed - skipped))

    for key, results in suites.items():
        bad = [r for r in results if r[1] == "Failed"]
        ok = [r for r in results if r[1] == "Passed"]
        print("### %s: %d из %d прошли\n" % (names.get(key, key), len(ok), len(results)))
        for name, _, message in bad:
            print("- ПАДЕНИЕ: %s\n  - %s" % (name, message[:400] or "без сообщения"))
        if bad:
            print()
        if ok:
            print("<details><summary>Прошедшие тесты (%d)</summary>\n" % len(ok))
            for name, _, _ in ok:
                print("- %s" % name)
            print("\n</details>\n")


if __name__ == "__main__":
    main()
