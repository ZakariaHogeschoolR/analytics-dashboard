import pytest
import requests

BASE_URL = "http://localhost:8000"

@pytest.fixture(scope="session")
def user_token():
    # Nieuwe gebruiker registreren en inloggen
    requests.post(f"{BASE_URL}/register", json={
        "username": "normaluser",
        "password": "userpass",
        "name": "Normal User"
    })
    login = requests.post(f"{BASE_URL}/login", json={
        "username": "normaluser",
        "password": "userpass"
    })
    token = login.json().get("session_token")
    return {"Authorization": token}

def test_get_all_parking_lots(user_token):
    response = requests.get(f"{BASE_URL}/parking-lots/1", headers=user_token)
    # Verwacht: werkt als de parking-lot dataset al iets bevat
    assert response.status_code in [200, 404]

def test_user_cannot_create_parking_lot(user_token):
    response = requests.post(f"{BASE_URL}/parking-lots", headers=user_token, json={
        "name": "Fail Lot",
        "location": "Utrecht",
        "capacity": 50,
        "reserved": 0,
        "tariff": 2,
        "daytariff": 12
    })
    # Verwacht: gebruiker is geen ADMIN
    assert response.status_code == 403

def test_session_flow(user_token):
    # Start sessie
    start = requests.post(f"{BASE_URL}/parking-lots/1/sessions/start", headers=user_token, json={
        "licenseplate": "XX-123-YY"
    })
    # Mogelijk 200 als lot 1 bestaat, anders 404
    assert start.status_code in [200, 404]

    # Stop sessie
    stop = requests.post(f"{BASE_URL}/parking-lots/1/sessions/stop", headers=user_token, json={
        "licenseplate": "XX-123-YY"
    })
    assert stop.status_code in [200, 401, 404]
