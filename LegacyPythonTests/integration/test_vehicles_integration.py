import requests
import pytest
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

BASE_URL = "http://localhost:8000"

@pytest.fixture(scope="session")
def auth_token():
    # Eerst een gebruiker aanmaken
    register = requests.post(f"{BASE_URL}/register", json={
        "username": "testuser",
        "password": "testpass",
        "name": "Test User"
    })
    # Login voor token
    login = requests.post(f"{BASE_URL}/login", json={
        "username": "testuser",
        "password": "testpass"
    })
    token = login.json().get("session_token")
    return {"Authorization": token}

def test_create_vehicle(auth_token):
    response = requests.post(f"{BASE_URL}/vehicles", headers=auth_token, json={
        "name": "MyCar",
        "license_plate": "AB-123-CD"
    })
    assert response.status_code == 201
    data = response.json()
    assert data["status"] == "Success"
    assert data["vehicle"]["license_plate"] == "AB-123-CD"

def test_get_vehicles(auth_token):
    response = requests.get(f"{BASE_URL}/vehicles", headers=auth_token)
    assert response.status_code == 200
    vehicles = response.json()
    assert "AB123CD" in vehicles  # want je code verwijdert streepjes bij opslaan

def test_update_vehicle(auth_token):
    lid = "AB123CD"
    response = requests.put(f"{BASE_URL}/vehicles/{lid}", headers=auth_token, json={
        "name": "UpdatedCar"
    })
    assert response.status_code == 200
    data = response.json()
    assert data["vehicle"]["name"] == "UpdatedCar"

def test_delete_vehicle(auth_token):
    lid = "AB123CD"
    response = requests.delete(f"{BASE_URL}/vehicles/{lid}", headers=auth_token)
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "Deleted"
