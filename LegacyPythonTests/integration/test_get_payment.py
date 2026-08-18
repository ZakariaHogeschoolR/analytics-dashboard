
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
            with open('api/data/payments.json', 'w') as f:
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

    def test_do_GET_payments_200(self):
        data = {
            "username": "testuser123",
            "password": "testpass",
            "name": "Test User"
        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.get(f"{self.base_url}/payments", headers=headers)
        # response = requests.get(f"{self.base_url}/payments", json=data)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.text, '[]')

    def test_do_GET_payments_401(self):
        data = {
            "username": "testuser123",
            "password": "testpass",
            "name": "Test User"
        }
        requests.post(f"{self.base_url}/register", json=data)
        requests.post(f"{self.base_url}/login", json=data)
        response = requests.get(f"{self.base_url}/payments")
        # response = requests.get(f"{self.base_url}/payments", json=data)
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, "Unauthorized: Invalid or missing session token")
    
    def test_do_GET_payments_admin_403(self):
        data = {
            "username": "testuser123",
            "password": "testpass",
            "name": "Test User"
        }
        register_response = requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.get(f"{self.base_url}/payments/IDEAL", headers=headers)
        # response = requests.get(f"{self.base_url}/payments", json=data)
        self.assertEqual(response.status_code, 403)
        self.assertEqual(response.text, 'Access denied')

    def test_do_GET_payments_admin_200(self):
        data = {
            "username": "testuser123",
            "password": "testpass",
            "name": "Test User"
        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.get(f"{self.base_url}/payments/IDEAL", headers=headers)
        # response = requests.get(f"{self.base_url}/payments", json=data)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.text, '[]')



if __name__ == '__main__':
    unittest.main(verbosity=2)
