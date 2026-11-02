import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

release = load("verify-catalogue-release")
readiness = load("verify-catalogue-readiness")
nginx = load("render-catalogue-nginx")


class ReleaseTests(unittest.TestCase):
    def test_audit_fails_closed_and_checks_transitive_packages(self):
        self.assertTrue(release.audit_errors({}))
        audit = {"version": 1, "parameters": "--vulnerable --include-transitive", "sources": ["nuget.org"], "projects": [{"path": "test.csproj", "frameworks": [{"framework": "net10.0", "transitivePackages": [
            {"id": "bad", "vulnerabilities": [{"severity": "High"}]}]}]}]}
        self.assertTrue(release.audit_errors(audit))
        audit["projects"][0]["frameworks"][0]["transitivePackages"] = []
        self.assertFalse(release.audit_errors(audit))
        audit["projects"] = [{"path": "clean.csproj"}]
        self.assertFalse(release.audit_errors(audit))

    def test_required_fixture_cannot_be_missing_or_skipped(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/"results.trx"
            for outcome in ["NotExecuted", "Passed"]:
                path.write_text('<TestRun xmlns="urn:trx"><Results>'+''.join(
                    f'<UnitTestResult testName="{name}" outcome="{outcome}"/>' for name in release.FIXTURES)+
                    '</Results></TestRun>', encoding="utf-8")
                self.assertEqual(outcome != "Passed", bool(release.trx_errors([path], True)))
            self.assertTrue(release.trx_errors([], True))

    def test_published_crypto_and_openapi_versions(self):
        self.assertTrue(release.deps_errors({"libraries": {"System.Security.Cryptography.Xml/10.0.11": {}}}))
        self.assertTrue(release.deps_errors({"libraries": {"Microsoft.AspNetCore.Components.WebAssembly.Server/10.0.12": {}}}))
        self.assertFalse(release.deps_errors({"libraries": {"System.Security.Cryptography.Xml/10.0.13": {}, "Microsoft.OpenApi/2.7.5": {}}}))

    def test_nginx_only_accepts_exact_ips(self):
        self.assertIn("192.0.2.4 1;", nginx.render("192.0.2.4"))
        for value in ["0.0.0.0/0", "192.0.2.0/24", "0.0.0.0", "some-header", "127.0.0.1; include /tmp/evil;", "fe80::1%eth0", "fe80::1%x; include /tmp/evil;"]:
            with self.assertRaises(ValueError): nginx.render(value)

    def test_effective_environment_overrides_and_account_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/"appsettings.json").write_text(json.dumps({"Catalogue": {"Enabled": False, "EnabledSources": ["hotels"]}}))
            (root/"appsettings.Stage.json").write_text('{"Catalogue":{"Enabled":true}}')
            flat = readiness.settings(root, "Stage", {"Catalogue__EnabledSources__0": "entertainments"})
            config, errors = readiness.effective(flat)
            self.assertEqual(["entertainments"], config["sources"])
            self.assertNotIn("catalogue_disabled", errors)
            self.assertEqual(64, len(config["account_key"]))
            self.assertNotIn("password", config)

    def test_zero_inventory_is_valid_but_missing_scope_or_sync_is_not(self):
        config = {"sources": ["hotels"]}
        self.assertFalse(readiness.assess({"scopes": []}, config, [])["ready"])
        self.assertTrue(readiness.assess({"scopes": [{"source": "hotels", "published": 0, "reasons": []}]}, config, [])["ready"])
        self.assertFalse(readiness.assess({"scopes": [{"source": "hotels", "reasons": ["missing_sync"]}]}, config, [])["ready"])

    def test_source_registration_flags_are_part_of_readiness(self):
        config, errors = readiness.effective({})
        self.assertEqual(["hotels"], config["sources"])
        config, errors = readiness.effective({"catalogue:enabledsources:0": "entertainments"})
        self.assertIn("entertainment_module_disabled", errors)
        self.assertIn("entertainment_catalogue_disabled", errors)
        self.assertIn("entertainment_provider_disabled", errors)


if __name__ == "__main__": unittest.main()
