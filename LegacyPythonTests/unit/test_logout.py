import unittest
import requests
import threading
import time
import sys
import os
import json

# Voeg parent directory toe aan path voor imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.append(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'api'))

from api.server import HTTPServer, RequestHandler


class TestLogoutEndpoint(unittest.TestCase):
    """Test logout endpoint functionaliteit"""

    @classmethod
    def setUpClass(cls):
        """Start test server"""
        # Sla originele directory op
        cls.original_dir = os.getcwd()

        # Ga naar parent directory (MobyPark level)
        parent_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        os.chdir(parent_dir)

        # Controleer of api/data/users.json bestaat
        if not os.path.exists('api/data/users.json'):
            # Maak directories aan als ze niet bestaan
            os.makedirs('api/data', exist_ok=True)
            os.makedirs('api/data/pdata', exist_ok=True)
            # Maak lege users.json
            with open('api/data/users.json', 'w') as f:
                json.dump([], f)

        # Ga naar api directory zodat server de juiste paths kan vinden
        os.chdir('api')

        # Start server
        cls.server = HTTPServer(('localhost', 8888), RequestHandler)
        cls.server_thread = threading.Thread(target=cls.server.serve_forever)
        cls.server_thread.daemon = True
        cls.server_thread.start()
        time.sleep(1)
        cls.base_url = "http://localhost:8888"

    @classmethod
    def tearDownClass(cls):
        """Stop test server en ga terug naar originele directory"""
        cls.server.shutdown()
        # Ga terug naar originele directory
        os.chdir(cls.original_dir)

    def create_test_user(self, username="testuser123", password="testpass", name="Test User"):
        """Helper method om test user aan te maken"""
        try:
            data = {
                "username": username,
                "password": password,
                "name": name
            }
            return requests.post(f"{self.base_url}/register", json=data)
        except:
            return None

    def login_test_user(self, username="testuser123", password="testpass"):
        """Helper method om in te loggen en token te krijgen"""
        try:
            data = {"username": username, "password": password}
            response = requests.post(f"{self.base_url}/login", json=data)
            if response.status_code == 200:
                return response.json().get("session_token")
        except:
            pass
        return None

    def test_logout_valid_token(self):
        """Test logout met geldig session token"""
        # Maak user aan en log in voor token
        self.create_test_user("logouttest", "logoutpass", "Logout Test")
        token = self.login_test_user("logouttest", "logoutpass")

        if not token:
            self.fail("Kon geen session token krijgen voor logout test")

        # Test logout met geldig token
        headers = {"Authorization": token}
        response = requests.get(f"{self.base_url}/logout", headers=headers)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.text, "User logged out")

    def test_logout_no_token(self):
        """Test logout zonder Authorization header"""
        response = requests.get(f"{self.base_url}/logout")
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Invalid session token")

    def test_logout_invalid_token(self):
        """Test logout met ongeldig token"""
        headers = {"Authorization": "invalid_token_12345"}
        response = requests.get(f"{self.base_url}/logout", headers=headers)
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Invalid session token")

    def test_logout_empty_token(self):
        """Test logout met lege Authorization header"""
        headers = {"Authorization": ""}
        response = requests.get(f"{self.base_url}/logout", headers=headers)
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Invalid session token")

    def test_logout_twice_same_token(self):
        """Test logout twee keer met zelfde token (token should be invalid after first logout)"""
        # Maak user aan en log in voor token
        self.create_test_user("doublelogout", "doublepass", "Double Logout Test")
        token = self.login_test_user("doublelogout", "doublepass")

        if not token:
            self.fail("Kon geen session token krijgen voor logout test")

        headers = {"Authorization": token}

        # Eerste logout - zou moeten werken
        response1 = requests.get(f"{self.base_url}/logout", headers=headers)
        self.assertEqual(response1.status_code, 200)
        self.assertEqual(response1.text, "User logged out")

        # Tweede logout met zelfde token - zou moeten falen
        response2 = requests.get(f"{self.base_url}/logout", headers=headers)
        self.assertEqual(response2.status_code, 400)
        self.assertEqual(response2.text, "Invalid session token")


if __name__ == '__main__':
    unittest.main(verbosity=2)