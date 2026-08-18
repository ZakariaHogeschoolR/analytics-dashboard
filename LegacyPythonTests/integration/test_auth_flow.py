# tests/integration/test_auth_flow.py
import os
import json
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


BASE_URL = "http://0.0.0.0:8000"


# ---------- util ----------

def _wait_for_port(host: str, port: int, timeout: float = 8.0) -> None:
    """Wacht tot de TCP-poort luistert (robuste readiness i.p.v. sleep)."""
    deadline = time.time() + timeout
    while time.time() < deadline:
        with closing(socket.socket(socket.AF_INET, socket.SOCK_STREAM)) as sock:
            sock.settimeout(0.25)
            if sock.connect_ex((host, port)) == 0:
                return
        time.sleep(0.1)
    raise RuntimeError(f"Server niet bereikbaar op {host}:{port} binnen {timeout}s")


# ---------- API helpers (contract behouden) ----------

def api_register(client: requests.Session, username: str, password: str, name: str, base=BASE_URL):
    return client.post(f"{base}/register", json={
        "username": username, "password": password, "name": name
    })


def api_login(client: requests.Session, username: str, password: str, base=BASE_URL):
    return client.post(f"{base}/login", json={
        "username": username, "password": password
    })


def api_profile(client: requests.Session, token: str, base=BASE_URL):
    return client.get(f"{base}/profile", headers={"Authorization": token})


def api_logout(client: requests.Session, token: str | None, base=BASE_URL):
    headers = {"Authorization": token} if token else {}
    return client.get(f"{base}/logout", headers=headers)


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
    """
    Start server als subprocess; backup ./data, maak schone testdata, herstel na afloop.
    Poort-poll voor readiness, DEVNULL om PIPE-deadlocks te voorkomen.
    """
    # Backup en schone test-data
    if os.path.exists("data_backup"):
        shutil.rmtree("data_backup")
    if os.path.exists("data"):
        shutil.copytree("data", "data_backup")
        shutil.rmtree("data")
    os.makedirs("data/pdata", exist_ok=True)
    with open("data/users.json", "w", encoding="utf-8") as f:
        json.dump([], f)

    # Start server
    process = subprocess.Popen(
        ["python3", "server.py"],
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

    # Teardown
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
    """Reset users.json vóór elke test (als je server het bestand gebruikt)."""
    os.makedirs("data", exist_ok=True)
    with open("data/users.json", "w", encoding="utf-8") as f:
        json.dump([], f)
    yield


@pytest.fixture
def client():
    """Requests session per test (sneller, header reuse)."""
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

class TestAuthenticationFlow:
    """Integratietests voor registratie, login, profile en logout."""

    # -------- REGISTER --------

    def test_register_success(self, server_process, client, unique_username):
        username = unique_username("testuser")
        password = "testpass"

        # Register
        resp = api_register(client, username, password, "Test User")
        assert_created_response(resp)

        # Verifieer via API: login moet slagen
        login = api_login(client, username, password)
        assert login.status_code == 200
        data = login.json()
        assert data["message"] == "User logged in"
        assert "session_token" in data and len(data["session_token"]) == 36

        # Verifieer duplicate gedrag
        dup = api_register(client, username, "newpass", "Dup User")
        assert dup.status_code == 200
        assert dup.text == "Username already taken"

    def test_register_duplicate(self, server_process, client, unique_username):
        username = unique_username("duplicate")
        first = api_register(client, username, "pass", "User")
        assert_created_response(first)
        resp = api_register(client, username, "pass2", "User2")
        assert resp.status_code == 200
        assert resp.text == "Username already taken"

    # -------- LOGIN --------

    def test_login_success(self, server_process, client, unique_username):
        username = unique_username("user1")
        api_register(client, username, "pass1", "User 1")
        resp = api_login(client, username, "pass1")
        assert resp.status_code == 200
        data = resp.json()
        assert data["message"] == "User logged in"
        assert "session_token" in data and len(data["session_token"]) == 36  # UUID

    def test_login_wrong_password(self, server_process, client, unique_username):
        username = unique_username("user2")
        api_register(client, username, "correct", "User")
        resp = api_login(client, username, "wrong")
        assert resp.status_code == 401
        assert resp.text == "User not found"

    def test_login_nonexistent_user(self, server_process, client, unique_username):
        username = unique_username("notexist")
        resp = api_login(client, username, "pass")
        assert resp.status_code == 401
        assert resp.text == "User not found"

    def test_login_missing_credentials(self, server_process, client):
        resp = client.post(f"{BASE_URL}/login", json={"username": "test"})
        assert resp.status_code == 400
        assert resp.text == "Missing credentials"

    # -------- LOGOUT --------

    def test_logout_success(self, server_process, client, unique_username):
        username = unique_username("user3")
        api_register(client, username, "pass", "User")
        login = api_login(client, username, "pass")
        token = login.json()["session_token"]

        resp = api_logout(client, token)
        assert resp.status_code == 200
        assert resp.text == "User logged out"

    def test_logout_without_token(self, server_process, client):
        resp = api_logout(client, None)
        assert resp.status_code == 400
        assert resp.text == "Invalid session token"

    def test_logout_invalid_token(self, server_process, client):
        resp = api_logout(client, "fake-token")
        assert resp.status_code == 400
        assert resp.text == "Invalid session token"

    # -------- COMPLETE FLOW --------

    def test_complete_auth_flow(self, server_process, client, unique_username):
        username = unique_username("flowuser")

        reg = api_register(client, username, "pass123", "Flow User")
        assert_created_response(reg)

        login = api_login(client, username, "pass123")
        assert login.status_code == 200
        token = login.json()["session_token"]

        profile = api_profile(client, token)
        assert profile.status_code == 200
        data = profile.json()
        assert data["username"] == username

        out = api_logout(client, token)
        assert out.status_code == 200
        assert out.text == "User logged out"

        after = api_profile(client, token)
        assert after.status_code == 401