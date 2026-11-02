"""Read effective JSON/environment settings and run a read-only PostgreSQL coverage report.

Requires psql on PATH, or --docker-container for an already running local PostgreSQL.
Connection credentials use PG* environment variables, never command-line arguments.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request


def settings(directory, environment, environ):
    flat = {}
    def flatten(value, prefix=""):
        if isinstance(value, dict):
            for key, child in value.items(): flatten(child, prefix + key.lower() + ":")
        elif isinstance(value, list):
            if not value: flat[prefix.rstrip(":")] = "[]"
            for index, child in enumerate(value): flatten(child, prefix + str(index) + ":")
        elif value is not None:
            flat[prefix.rstrip(":")] = str(value).lower() if isinstance(value, bool) else str(value)
    for name in ["appsettings.json", f"appsettings.{environment}.json"]:
        file = directory / name
        if file.exists(): flatten(json.loads(file.read_text(encoding="utf-8-sig")))
    for key, value in environ.items():
        flat[key.replace("__", ":").lower()] = value
    return flat


def effective(flat):
    def get(key, default=""): return flat.get(key.lower(), default)
    def flag(key, default=False): return get(key, str(default)).lower() == "true"
    def array(key):
        prefix = key.lower() + ":"
        return [v for k, v in sorted(((k,v) for k,v in flat.items() if k.startswith(prefix) and k[len(prefix):].isdigit()), key=lambda x: int(x[0][len(prefix):]))]
    registered = ["hotels"]
    if all(flag(key) for key in ["Entertainments:Enabled", "Entertainments:CatalogueEnabled", "Entertainments:Providers:TouristPanel:Enabled"]):
        registered.append("entertainments")
    sources = array("Catalogue:EnabledSources") if "catalogue:enabledsources" in flat or any(k.startswith("catalogue:enabledsources:") for k in flat) else registered
    local = array("Hotels:StaticContent:ReadScopes") if flag("Hotels:StaticContent:ReadEnabled") else []
    local = [{"snapptrip":"SnappTrip", "lamasoo":"Lamasoo"}.get(scope.partition(":")[0].lower(), scope.partition(":")[0])+":"+scope.partition(":")[2] for scope in local]
    providers = [p for p, default in [("SnappTrip", True), ("Lamasoo", False)] if flag(f"Hotels:Providers:{p}:SearchEnabled", default)]
    providers = sorted(set(providers + [scope.split(":")[0] for scope in local]))
    prefix = "Entertainments:Providers:TouristPanel:"
    account = "|".join(get(prefix+k, default) for k, default in [
        ("BaseUrl", "https://marketplace-api.touristpanel.ir"), ("Tenant", ""), ("ClientId", ""), ("Username", ""), ("Scope", "B2CApi")])
    config = dict(sources=sources, providers=providers, local_scopes=local,
                  hotel_max_age_days=int(get("Hotels:Discovery:MaxAgeDays", "7")),
                  entertainment_max_age_days=int(get("Entertainments:MaxDiscoveryAgeDays", "7")),
                  account_key=hashlib.sha256(account.encode()).hexdigest())
    errors = []
    if not flag("Catalogue:Enabled"): errors.append("catalogue_disabled")
    if not sources: errors.append("no_sources")
    if set(sources) - {"hotels", "entertainments"}: errors.append("unsupported_source")
    if config["hotel_max_age_days"] < 1 or config["entertainment_max_age_days"] < 1: errors.append("invalid_freshness")
    if "hotels" in sources and not (flag("Hotels:Discovery:WorkerEnabled") or flag("Hotels:StaticContent:WorkerEnabled")):
        errors.append("hotel_worker_disabled")
    if "entertainments" in sources and not flag(prefix+"Enabled"): errors.append("entertainment_provider_disabled")
    if "entertainments" in sources and not flag("Entertainments:Enabled"): errors.append("entertainment_module_disabled")
    if "entertainments" in sources and not flag("Entertainments:CatalogueEnabled"): errors.append("entertainment_catalogue_disabled")
    if "entertainments" in sources and not flag("Entertainments:DiscoveryWorkerEnabled"): errors.append("entertainment_worker_disabled")
    return config, errors


def assess(report, config, errors):
    for row in report["scopes"]: row["ready"] = not row["reasons"]
    for source in config["sources"]:
        rows = [x for x in report["scopes"] if x["source"] == source]
        if not rows: errors.append("no_coverage:" + source)
    report["ready"] = not errors and all(not row["reasons"] for row in report["scopes"])
    report["errors"] = errors
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config-dir", type=Path, required=True)
    parser.add_argument("--environment", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--docker-container")
    parser.add_argument("--api-url")
    parser.add_argument("--admin-token-env", default="REFAHI_READINESS_ADMIN_TOKEN")
    args = parser.parse_args()
    report = {"ready": False, "scopes": [], "errors": []}
    try:
        config, errors = effective(settings(args.config_dir, args.environment, os.environ))
        sql = Path(__file__).resolve().parents[1] / "deployment/catalogue-readiness.sql"
        command = ["psql", "-X", "-qAt", "-v", "ON_ERROR_STOP=1", "-v", "config="+json.dumps(config)]
        if args.docker_container:
            command = ["docker", "exec", "-i"] + [part for name in ["PGHOST", "PGPORT", "PGDATABASE", "PGUSER", "PGPASSWORD"]
                if name in os.environ for part in ["-e", name]] + [args.docker_container] + command
        result = subprocess.run(command, input=sql.read_text(encoding="utf-8"), capture_output=True, text=True, encoding="utf-8", timeout=45)
        if result.returncode: raise RuntimeError("Coverage SQL failed")
        report = json.loads(result.stdout)
        if config["local_scopes"]:
            token = os.environ.get(args.admin_token_env)
            if not args.api_url or not token:
                errors.append("missing_local_content_readiness")
            else:
                request = urllib.request.Request(args.api_url.rstrip("/")+"/api/hotels/v2/content/readiness", headers={"Authorization": "Bearer "+token})
                with urllib.request.urlopen(request, timeout=10) as response: local = json.load(response)["data"]
                for scope in config["local_scopes"]:
                    provider, city = scope.split(":")
                    matches = [x for x in local if x["provider"].lower()==provider.lower() and x["referenceCityId"]==int(city)]
                    if not matches or not all(x["ready"] for x in matches): errors.append("local_content_not_ready:"+scope)
        report = assess(report, config, errors)
        report["environment"] = args.environment
        report["commit"] = os.environ.get("CI_COMMIT_SHA")
        report["database"] = os.environ.get("PGDATABASE")
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.TimeoutExpired) as error:
        # Never print provider settings, database connection errors or auth payloads.
        report["errors"].append("coverage_check_failed:"+type(error).__name__)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("source          provider     scope       published expired pending reasons")
    for row in report["scopes"]:
        print(row["source"], row["provider"], row["scope"], row["published"], row["expired"], row["pendingDetails"], ",".join(row["reasons"]))
    print(json.dumps({"ready": report["ready"], "errors": report["errors"]}))
    return int(not report["ready"])


if __name__ == "__main__":
    raise SystemExit(main())
