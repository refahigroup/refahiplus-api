"""Fail closed on missing audit evidence, vulnerable packages or skipped DB fixtures."""
import argparse
import json
import os
import xml.etree.ElementTree as ET
from pathlib import Path

FIXTURES = ("V4MappingsAndListingUpgrade_PreserveNativeAndHistoricalIdentities",
            "Upgrade_sync_identity_search_freshness_and_provider_payload_ownership")
PATCHED = {"microsoft.openapi": (2, 7, 5), "system.security.cryptography.pkcs": (10, 0, 13),
           "system.security.cryptography.xml": (10, 0, 13),
           "microsoft.aspnetcore.components.webassembly.server": (10, 0, 13)}


def audit_errors(document):
    errors = []
    if (not isinstance(document, dict) or document.get("version") != 1 or not document.get("projects")
        or not document.get("sources") or "--vulnerable" not in document.get("parameters", "")
        or "--include-transitive" not in document.get("parameters", "")):
        return ["Missing NuGet project audit results"]
    if document.get("errors") or document.get("logs"):
        errors.append("NuGet audit failed")
    for project in document["projects"]:
        # NuGet omits frameworks entirely for projects with no vulnerable packages.
        if not project.get("path"):
            errors.append("Missing audited project path")
        for framework in project.get("frameworks", []):
            for package in framework.get("topLevelPackages", []) + framework.get("transitivePackages", []):
                for vulnerability in package.get("vulnerabilities", []):
                    if vulnerability.get("severity", "").lower() in {"moderate", "high", "critical"}:
                        errors.append("Vulnerable package: " + package["id"])
    return errors


def trx_errors(paths, require_integration=False):
    results = []
    for path in paths:
        root = ET.parse(path).getroot()
        results.extend(root.findall(".//{*}UnitTestResult"))
        summary = root.find(".//{*}ResultSummary")
        if summary is not None and summary.get("outcome") in {"Failed", "Error", "Aborted"}:
            return ["Failed test run: " + str(path)]
    if not results:
        return ["Missing TRX test results"]
    errors = ["Failed test: " + x.get("testName", "unknown") for x in results
              if x.get("outcome") not in {"Passed", "NotExecuted"}]
    if require_integration:
        for fixture in FIXTURES:
            matches = [x for x in results if fixture in x.get("testName", "")]
            if not matches or any(x.get("outcome") != "Passed" for x in matches):
                errors.append("Required PostgreSQL fixture missing, skipped or failed: " + fixture)
    return errors


def deps_errors(document):
    if not document.get("libraries"):
        return ["Missing published dependency evidence"]
    errors = []
    for package in document["libraries"]:
        name, _, version = package.rpartition("/")
        minimum = PATCHED.get(name.lower())
        if minimum:
            parsed = tuple(int(x) for x in version.split("-")[0].split("."))
            if "-" in version or parsed < minimum or parsed[0] != minimum[0]:
                errors.append("Unapproved published version: " + package)
    return errors


def write_junit(paths, destination):
    suites = ET.Element("testsuites")
    for path in paths:
        results = ET.parse(path).getroot().findall(".//{*}UnitTestResult")
        suite = ET.SubElement(suites, "testsuite", name=path.stem, tests=str(len(results)),
            failures=str(sum(x.get("outcome") not in {"Passed", "NotExecuted"} for x in results)),
            skipped=str(sum(x.get("outcome") == "NotExecuted" for x in results)))
        for result in results:
            name = result.get("testName", "unknown")
            case = ET.SubElement(suite, "testcase", name=name, classname=name.rpartition(".")[0])
            if result.get("outcome") == "NotExecuted": ET.SubElement(case, "skipped")
            elif result.get("outcome") != "Passed": ET.SubElement(case, "failure", message=result.get("outcome", "Unknown"))
    ET.ElementTree(suites).write(destination, encoding="utf-8", xml_declaration=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", type=Path, required=True)
    parser.add_argument("--trx", type=Path, required=True)
    parser.add_argument("--deps", type=Path, required=True)
    parser.add_argument("--require-integration", action="store_true")
    parser.add_argument("--output", type=Path, default=Path("release-evidence.json"))
    args = parser.parse_args()
    try:
        errors = audit_errors(json.loads(args.audit.read_text(encoding="utf-8-sig")))
        paths = list(args.trx.rglob("*.trx"))
        errors += trx_errors(paths, args.require_integration)
        write_junit(paths, args.output.with_suffix(".junit.xml"))
        errors += deps_errors(json.loads(args.deps.read_text(encoding="utf-8-sig")))
    except (OSError, ValueError, ET.ParseError, KeyError, TypeError) as error:
        errors = ["Invalid or missing release evidence: " + type(error).__name__]
    report = {"ready": not errors, "errors": errors,
              "checks": ["nuget-audit", "test-results", "published-dependencies"],
              "commit": os.environ.get("CI_COMMIT_SHA"), "pipeline": os.environ.get("CI_PIPELINE_ID")}
    args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))
    raise SystemExit(bool(errors))
