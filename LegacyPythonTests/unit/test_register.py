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


class TestRegisterEndpoint(unittest.TestCase):
    """Test register endpoint functionaliteit"""

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

    def test_register_new_user(self):
        """Test succesvolle registratie van nieuwe user"""
        data = {
            "username": "newuser456",
            "password": "newpass",
            "name": "New User"
        }
        response = requests.post(f"{self.base_url}/register", json=data)
        self.assertEqual(response.status_code, 201)
        self.assertEqual(response.text, "User created")

    def test_register_duplicate_username(self):
        """Test registratie met username die al bestaat"""
        # Maak eerst een user aan
        data = {
            "username": "duplicate_test",
            "password": "testpass",
            "name": "Test User"
        }
        requests.post(f"{self.base_url}/register", json=data)

        # Probeer dezelfde username opnieuw
        response = requests.post(f"{self.base_url}/register", json=data)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.text, "Username already taken")

    def test_register_missing_fields(self):
        """Test registratie met ontbrekende velden"""
        # Test zonder password
        data = {"username": "test", "name": "Test"}
        response = requests.post(f"{self.base_url}/register", json=data)
        # Na je server fix zou dit 400 moeten zijn
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response.text, "Missing required fields")


if __name__ == '__main__':
    unittest.main(verbosity=2)