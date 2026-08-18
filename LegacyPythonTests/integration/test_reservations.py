import json
import os
import sys
import threading
import time
from http.server import HTTPServer
from pathlib import Path

import pytest
import requests


@pytest.fixture(scope="module")
def test_server(tmp_path_factory):
    # Arrange: temp data directory structure
    tmp = tmp_path_factory.mktemp("api_data")
    data_dir = tmp / "data"
    pdata_dir = data_dir / "pdata"
    data_dir.mkdir()
    pdata_dir.mkdir()

    # Seed users (plain md5 like server expects)
    import hashlib
    users = [
        {
            "username": "user1",
            "password": hashlib.md5("password".encode()).hexdigest(),
            "name": "User One",
            "role": "User",
        }
    ]
    (data_dir / "users.json").write_text(json.dumps(users))

    # Seed parking lots as dict with reserved counter
    parking_lots = {
        "1": {
            "name": "Lot A",
            "location": "Center",
            "tariff": 2.0,
            "daytariff": 10.0,
            "reserved": 0,
        }
    }
    (data_dir / "parking-lots.json").write_text(json.dumps(parking_lots))

    # Reservations stored as dict in this API
    (data_dir / "reservations.json").write_text(json.dumps({}))
    # Optional files referenced elsewhere
    (data_dir / "payments.json").write_text(json.dumps([]))
    (data_dir / "vehicles.json").write_text(json.dumps({}))

    # Ensure Python can import the server module
    pythonapi_dir = Path(__file__).resolve().parents[1]
    if str(pythonapi_dir) not in sys.path:
        sys.path.insert(0, str(pythonapi_dir))

    # Change working directory so storage_utils resolves data/* correctly
    cwd_before = os.getcwd()
    os.chdir(str(tmp))

    # Import server and reset sessions
    import importlib
    server_module = importlib.import_module("server")
    # Reset in-memory sessions
    import session_manager
    session_manager.sessions.clear()

    # Start HTTP server on an ephemeral port
    httpd = HTTPServer(("127.0.0.1", 0), server_module.RequestHandler)
    host, port = httpd.server_address
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()

    base_url = f"http://{host}:{port}"

    yield {"base_url": base_url}

    # Teardown
    httpd.shutdown()
    thread.join(timeout=2)
    os.chdir(cwd_before)


def login_and_get_token(base_url: str) -> str:
    res = requests.post(
        f"{base_url}/login",
        json={"username": "user1", "password": "password"},
        timeout=5,
    )
    res.raise_for_status()
    return res.json()["session_token"]


def test_create_reservation_requires_auth(test_server):
    base = test_server["base_url"]
    res = requests.post(
        f"{base}/reservations",
        json={"licenseplate": "AA-11-BB"},
        timeout=5,
    )
    assert res.status_code == 401


def test_create_reservation_missing_field(test_server):
    base = test_server["base_url"]
    token = login_and_get_token(base)
    res = requests.post(
        f"{base}/reservations",
        headers={"Authorization": token},
        json={
            # Missing enddate
            "licenseplate": "AA-11-BB",
            "startdate": "2025-01-01 08:00:00",
            "parkinglot": "1",
        },
        timeout=5,
    )
    assert res.status_code == 401
    body = res.json()
    assert body.get("error") == "Require field missing"
    assert body.get("field") == "enddate"


def test_create_reservation_success_and_get_by_id(test_server):
    base = test_server["base_url"]
    token = login_and_get_token(base)
    payload = {
        "licenseplate": "AA-11-BB",
        "startdate": "2025-01-01 08:00:00",
        "enddate": "2025-01-01 12:00:00",
        "parkinglot": "1",
    }
    res = requests.post(
        f"{base}/reservations",
        headers={"Authorization": token},
        json=payload,
        timeout=5,
    )
    assert res.status_code == 201
    data = res.json()
    assert data["status"] == "Success"
    rid = data["reservation"]["id"]

    # Fetch by id
    res2 = requests.get(
        f"{base}/reservations/{rid}", headers={"Authorization": token}, timeout=5
    )
    assert res2.status_code == 200
    r = res2.json()
    assert r["licenseplate"] == "AA-11-BB"
    assert r["user"] == "user1"


def test_update_reservation_success(test_server):
    base = test_server["base_url"]
    token = login_and_get_token(base)

    # create first
    payload = {
        "licenseplate": "CC-22-DD",
        "startdate": "2025-01-02 09:00:00",
        "enddate": "2025-01-02 10:00:00",
        "parkinglot": "1",
    }
    res = requests.post(
        f"{base}/reservations",
        headers={"Authorization": token},
        json=payload,
        timeout=5,
    )
    rid = res.json()["reservation"]["id"]

    # update
    upd = {
        "licenseplate": "CC-22-DD",
        "startdate": "2025-01-02 10:00:00",
        "enddate": "2025-01-02 12:00:00",
        "parkinglot": "1",
    }
    res2 = requests.put(
        f"{base}/reservations/{rid}",
        headers={"Authorization": token},
        json=upd,
        timeout=5,
    )
    assert res2.status_code == 200
    assert res2.json()["status"] == "Updated"


def test_delete_reservation_success(test_server):
    base = test_server["base_url"]
    token = login_and_get_token(base)

    # create
    payload = {
        "licenseplate": "EE-33-FF",
        "startdate": "2025-01-03 09:00:00",
        "enddate": "2025-01-03 11:00:00",
        "parkinglot": "1",
    }
    res = requests.post(
        f"{base}/reservations",
        headers={"Authorization": token},
        json=payload,
        timeout=5,
    )
    rid = res.json()["reservation"]["id"]

    # delete
    res2 = requests.delete(
        f"{base}/reservations/{rid}", headers={"Authorization": token}, timeout=5
    )
    assert res2.status_code == 200
    assert res2.json()["status"] == "Deleted"


