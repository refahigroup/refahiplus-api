"""Render only an exact trusted Web socket peer; no subnets or header exemptions."""
import argparse
import ipaddress
from pathlib import Path


def render(peer):
    address = ipaddress.ip_address(peer)
    if address.is_unspecified or address.is_multicast or getattr(address, "scope_id", None) is not None:
        raise ValueError("An actual Web host peer IP is required")
    template = Path(__file__).resolve().parents[1] / "deployment/nginx/catalogue-http.conf.template"
    return template.read_text(encoding="utf-8").replace("__WEB_PEER_IP__", str(address))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--web-peer-ip", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(render(args.web_peer_ip), encoding="utf-8")
