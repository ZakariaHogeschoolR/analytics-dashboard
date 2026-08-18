# tests/integration/test_profile.py
import os
import json
import sys
import time
import shutil
import signal
import socket
import subprocess
import random
import string
from contextlib import closing

import pytest
import requests


BASE_URL = "http://127.0.0.1:8000"


# ---------- util ----------

def _wait_for_port(host: str, port: int, timeout: float = 8.0) -> None:
    """Wacht tot de TCP-poort luistert."""
    import time
    deadline = time.time() + timeout
    while time.time() < deadline:
        with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as sock:
            sock.settimeout(0.25)
            if sock.connect_ex((host, port)) == 0:
                return
        time.sleep(0.1)
    raise RuntimeError(f"Server niet bereikbaar op {host}:{port} binnen {timeout}s")


# ---------- API helpers ----------

def api_profile_get(client: requests.Session, token: str = None, base=BASE_URL):
    """GET /profile met optionele Authorization token."""
    headers = {"Authorization": token} if token else {}
    return client.get(f"{base}/profile", headers=headers)


def api_profile_put(client: requests.Session, data: dict, token: str = None, base=BASE_URL):
    """PUT /profile met optionele Authorization token en update data."""
    headers = {"Authorization": token} if token else {}
    return client.put(f"{base}/profile", headers=headers, json=data)

def api_register(client: requests.Session, username: str, password: str, name: str, base=BASE_URL):
    return client.post(f"{base}/register", json={
        "username": username, "password": password, "name": name
    })


def api_login(client: requests.Session, username: str, password: str, base=BASE_URL):
    return client.post(f"{base}/login", json={
        "username": username, "password": password
    })

def assert_created_response(resp):
    """Accepteer 200 of 201, body strikt 'User created'."""
    assert resp.status_code in (200, 201), f"Unexpected status {resp.status_code}: {resp.text}"
    assert resp.text == "User created"



# ---------- fixtures ----------

@pytest.fixture(scope="class")
def server_url():
    return BASE_URL


@pytest.fixture(scope="class")
def server_process(server_url):
    """Start server als subprocess."""
    if os.path.exists("data_backup"):
        shutil.rmtree("data_backup")
    if os.path.exists("data"):
        shutil.copytree("data", "data_backup")
        shutil.rmtree("data")
    os.makedirs("data/pdata", exist_ok=True)
    with open("data/users.json", "w", encoding="utf-8") as f:
        json.dump([], f)

    process = subprocess.Popen(
        [sys.executable, "server.py"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        start_new_session=True,
    )

    try:
        _wait_for_port("127.0.0.1", 8000, timeout=8.0)
    except Exception:
        process.send_signal(signal.SIGTERM)
        process.wait(timeout=5)
        raise

    yield server_url

    try:
        process.send_signal(signal.SIGTERM)
        process.wait(timeout=5)
    except Exception:
        process.kill()

    if os.path.exists("data"):
        shutil.rmtree("data")
    if os.path.exists("data_backup"):
        shutil.move("data_backup", "data")


@pytest.fixture(autouse=True)
def reset_users():
    """Reset users.json vóór elke test."""
    os.makedirs("data", exist_ok=True)
    with open("data/users.json", "w", encoding="utf-8") as f:
        json.dump([], f)
    yield


@pytest.fixture
def client():
    """Requests session per test."""
    with requests.Session() as s:
        yield s

@pytest.fixture
def unique_username():
    """Genereer een unieke username per call: prefix + 6 cijfers."""
    def _make(prefix: str = "user") -> str:
        suffix = "".join(random.choices(string.digits, k=6))
        return f"{prefix}{suffix}"
    return _make



# ==================== TESTS ====================

class TestProfileEndpoint:
    """Integratietests voor GET en PUT /profile."""

    # -------- GET /profile --------

    def test_get_profile_without_token(self, server_process, client):
        """Test profile opvraag zonder Authorization token."""
        resp = api_profile_get(client)
        assert resp.status_code == 401

    # -------- PUT /profile --------

    def test_put_profile_without_token(self, server_process, client):
        """Test profile update zonder Authorization token."""
        update_data = {"name": "New Name"}
        resp = api_profile_put(client, update_data)
        assert resp.status_code == 401

    def test_get_profile_with_token(self, server_process, client, unique_username):
        """Test profile opvraag met Authorization token."""
        # Maak unieke username
        username = unique_username("user1")
        password = "testpass"

        # Register en login zoals in auth test
        reg = api_register(client, username, password, "Get User")
        assert_created_response(reg)

        login = api_login(client, username, password)
        assert login.status_code == 200
        token = login.json()["session_token"] # Pak het token

        # Nu het GET /prfile endpoint testen met token
        resp = api_profile_get(client, token)
        assert resp.status_code == 200
        data = resp.json()
        assert data["username"] == username
        assert "password" not in data

    def test_put_profile_with_token(self, server_process, client, unique_username):
        """Test profile update met Authorization token."""
        username = unique_username("user2")
        password = "testpass"

        # Register en login zoals hierboven
        reg = api_register(client, username, password, "Old Name")
        assert_created_response(reg)

        login = api_login(client, username, password)
        token = login.json()["session_token"]

        # PUT /profile met token
        update_data = {"name": "Updated Name", "role": "ADMIN"}
        resp = api_profile_put(client, update_data, token)
        assert resp.status_code == 200

        # Dubbel check om te kijken of het echt geupdate is
        get_resp = api_profile_get(client, token)
        data = get_resp.json()
        assert data["name"] == "Updated Name"
        assert data["role"] == "ADMIN"