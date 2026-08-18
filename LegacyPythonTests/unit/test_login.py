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


class TestLoginEndpoint(unittest.TestCase):
    """Test login endpoint functionaliteit"""

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

    def test_login_valid_credentials(self):
        """Test login met geldige credentials"""
        # Zorg dat user bestaat
        self.create_test_user("logintest", "loginpass", "Login Test")

        # Test geldige login
        data = {
            "username": "logintest",
            "password": "loginpass"
        }
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 200)

        response_data = response.json()
        self.assertEqual(response_data["message"], "User logged in")
        self.assertIn("session_token", response_data)
        self.assertIsNotNone(response_data["session_token"])

    def test_login_invalid_username(self):
        """Test login met onbestaande username"""
        data = {
            "username": "nonexistentuser",
            "password": "anypassword"
        }
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, "User not found")

    def test_login_invalid_password(self):
        """Test login met fout password"""
        # Zorg dat user bestaat
        self.create_test_user("passtest", "correctpass", "Pass Test")

        data = {
            "username": "passtest",
            "password": "wrongpassword"
        }
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, "User not found")

    def test_login_missing_username(self):
        """Test login zonder username"""
        data = {"password": "testpass"}  # username ontbreekt
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Missing credentials")

    def test_login_missing_password(self):
        """Test login zonder password"""
        data = {"username": "test"}  # password ontbreekt
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Missing credentials")

    def test_login_empty_credentials(self):
        """Test login met lege credentials"""
        data = {"username": "", "password": ""}
        response = requests.post(f"{self.base_url}/login", json=data)
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Missing credentials")


if __name__ == '__main__':
    unittest.main(verbosity=2)