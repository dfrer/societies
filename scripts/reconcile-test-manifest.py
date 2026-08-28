from __future__ import annotations

import json
from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file_path = Path(path)
    text = file_path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"expected one match in {path}, found {count}: {old!r}")
    file_path.write_text(text.replace(old, new), encoding="utf-8", newline="\n")


def reconcile_manifest() -> None:
    manifest_path = "tests/test-manifest.json"
    replace_once(
        manifest_path,
        '"updatedUtc": "2026-08-25T00:00:00Z"',
        '"updatedUtc": "2026-08-28T00:00:00Z"',
    )
    replace_once(manifest_path, '"expectedTestCount": 494', '"expectedTestCount": 507')
    replace_once(manifest_path, '"expectedTestCount": 374', '"expectedTestCount": 387')
    replace_once(manifest_path, '"expectedTestCount": 27', '"expectedTestCount": 28')
    replace_once(
        manifest_path,
        '          "Test_SnowGlobeVoxelFoundationSmoke",\n'
        '          "Test_SnowGlobeVoxelPlayerGroundingRegression",',
        '          "Test_SnowGlobeVoxelFoundationSmoke",\n'
        '          "Test_LegacyVoxelVerticalRunCollision",\n'
        '          "Test_SnowGlobeVoxelPlayerGroundingRegression",',
    )
    replace_once(
        manifest_path,
        "The manifest declares 494 .NET tests: 374 fast, 11 integration, and 109 soak; "
        "and 27 Godot tests: six fast, nineteen integration, and two soak.",
        "The manifest declares 507 .NET tests: 387 fast, 11 integration, and 109 soak; "
        "and 28 Godot tests: six fast, twenty integration, and two soak.",
    )


def reconcile_pull_request_workflow() -> None:
    replace_once(
        ".github/workflows/tests.yml",
        """          if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
          }

      - name: Build Godot C# solutions
""",
        """          if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
          }

          $trxPath = "tests/Societies.Core.Tests/TestResults/fast-unit-tests.trx"
          [xml]$trx = Get-Content $trxPath -Raw
          $counters = $trx.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
          if ($null -eq $counters) {
            Write-Error "TRX counters were not found at $trxPath"
            exit 1
          }

          $actualFastCount = [int]$counters.GetAttribute("total")
          $expectedFastCount = [int]$manifest.required.dotnet.tiers.fast.expectedTestCount
          if ($actualFastCount -ne $expectedFastCount) {
            Write-Error "Fast-test manifest drift: expected $expectedFastCount, discovered $actualFastCount."
            exit 1
          }

          Write-Host "Fast-test manifest count verified: $actualFastCount"

      - name: Build Godot C# solutions
""",
    )

    replace_once(
        ".github/workflows/tests.yml",
        """      - name: Run headless smoke suite
        if: steps.change_scope.outputs.docs_only != 'true'
        run: godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
""",
        r"""      - name: Run headless smoke suite
        if: steps.change_scope.outputs.docs_only != 'true'
        shell: bash
        run: |
          set -euo pipefail
          log_file="$(mktemp)"
          godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn 2>&1 | tee "$log_file"

          expected_count="$(python3 -c 'import json; print(json.load(open("tests/test-manifest.json", encoding="utf-8"))["required"]["godot"]["expectedTestCount"])')"
          result_line="$(grep -E 'Headless results: [0-9]+ passed, [0-9]+ failed' "$log_file" | tail -n 1 || true)"
          if [[ -z "$result_line" ]]; then
            echo "::error::Godot headless result summary was not found."
            exit 1
          fi

          actual_passed="$(sed -E 's/.*Headless results: ([0-9]+) passed, ([0-9]+) failed.*/\1/' <<< "$result_line")"
          actual_failed="$(sed -E 's/.*Headless results: ([0-9]+) passed, ([0-9]+) failed.*/\2/' <<< "$result_line")"
          if [[ "$actual_failed" -ne 0 || "$actual_passed" -ne "$expected_count" ]]; then
            echo "::error::Godot manifest drift or failure: expected $expected_count passed and 0 failed; observed $actual_passed passed and $actual_failed failed."
            exit 1
          fi

          echo "Godot manifest count verified: $actual_passed"
""",
    )


def reconcile_extended_workflow() -> None:
    replace_once(
        ".github/workflows/tests-extended.yml",
        """      - name: Run all unit tests with coverage
        run: >
          dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj
          --configuration Release
          --logger "trx;LogFileName=unit-tests.trx"
          --collect:"XPlat Code Coverage"
""",
        """      - name: Run all unit tests with coverage
        shell: pwsh
        run: |
          $manifest = Get-Content tests/test-manifest.json -Raw | ConvertFrom-Json
          dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj `
            --configuration Release `
            --logger "trx;LogFileName=unit-tests.trx" `
            --collect:"XPlat Code Coverage"

          if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
          }

          $trxPath = "tests/Societies.Core.Tests/TestResults/unit-tests.trx"
          [xml]$trx = Get-Content $trxPath -Raw
          $counters = $trx.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
          if ($null -eq $counters) {
            Write-Error "TRX counters were not found at $trxPath"
            exit 1
          }

          $actualCount = [int]$counters.GetAttribute("total")
          $expectedCount = [int]$manifest.required.dotnet.expectedTestCount
          if ($actualCount -ne $expectedCount) {
            Write-Error "Full-test manifest drift: expected $expectedCount, discovered $actualCount."
            exit 1
          }

          Write-Host "Full-test manifest count verified: $actualCount"
""",
    )

    replace_once(
        ".github/workflows/tests-extended.yml",
        """      - name: Run headless smoke suite
        run: godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
""",
        r"""      - name: Run headless smoke suite
        shell: bash
        run: |
          set -euo pipefail
          log_file="$(mktemp)"
          godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn 2>&1 | tee "$log_file"

          expected_count="$(python3 -c 'import json; print(json.load(open("tests/test-manifest.json", encoding="utf-8"))["required"]["godot"]["expectedTestCount"])')"
          result_line="$(grep -E 'Headless results: [0-9]+ passed, [0-9]+ failed' "$log_file" | tail -n 1 || true)"
          if [[ -z "$result_line" ]]; then
            echo "::error::Godot headless result summary was not found."
            exit 1
          fi

          actual_passed="$(sed -E 's/.*Headless results: ([0-9]+) passed, ([0-9]+) failed.*/\1/' <<< "$result_line")"
          actual_failed="$(sed -E 's/.*Headless results: ([0-9]+) passed, ([0-9]+) failed.*/\2/' <<< "$result_line")"
          if [[ "$actual_failed" -ne 0 || "$actual_passed" -ne "$expected_count" ]]; then
            echo "::error::Godot manifest drift or failure: expected $expected_count passed and 0 failed; observed $actual_passed passed and $actual_failed failed."
            exit 1
          fi

          echo "Godot manifest count verified: $actual_passed"
""",
    )


def validate_manifest_arithmetic() -> None:
    manifest = json.loads(Path("tests/test-manifest.json").read_text(encoding="utf-8"))

    dotnet = manifest["required"]["dotnet"]
    dotnet_tiers = dotnet["tiers"]
    declared_dotnet = sum(
        int(dotnet_tiers[name]["expectedTestCount"])
        for name in ("fast", "integration", "soak")
    )
    if declared_dotnet != int(dotnet["expectedTestCount"]):
        raise RuntimeError(
            f"dotnet manifest arithmetic mismatch: {declared_dotnet} != {dotnet['expectedTestCount']}"
        )

    godot = manifest["required"]["godot"]
    declared_godot = sum(
        len(godot["tiers"][name]) for name in ("fast", "integration", "soak")
    )
    if declared_godot != int(godot["expectedTestCount"]):
        raise RuntimeError(
            f"godot manifest arithmetic mismatch: {declared_godot} != {godot['expectedTestCount']}"
        )

    print(f"Manifest arithmetic verified: .NET={declared_dotnet}, Godot={declared_godot}")


def main() -> None:
    reconcile_manifest()
    reconcile_pull_request_workflow()
    reconcile_extended_workflow()
    validate_manifest_arithmetic()


if __name__ == "__main__":
    main()
