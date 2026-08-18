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
from api.storage_utils import load_payment_data 


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

    def test_do_PUT_payments_200(self):
        payment_type = "IDEAL"
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "t_data": "",
            "validation": "8c6a54db-b03a-4f56-b514-4b56e465ebdd"

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.put(f"{self.base_url}/payments/{payment_type}", json=data, headers=headers)
        payments = load_payment_data()
        payments = [payment for payment in payments if payment['transaction'] == payment_type and payment['completed'] != False]
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.text, json.dumps({"status": "Success", "payment": payments[0]}))

    def test_do_PUT_payments_t_data_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "validation": "8c6a54db-b03a-4f56-b514-4b56e465ebdd"

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.put(f"{self.base_url}/payments/IDEAL", json=data, headers=headers)
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, '{"error": "Require field missing", "field": "t_data"}')
    
    def test_do_PUT_payments_validation_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "t_data": "",
        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        headers = {"Authorization": f"{token}"}
        response = requests.put(f"{self.base_url}/payments/IDEAL", json=data, headers=headers)
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, '{"error": "Require field missing", "field": "validation"}')



if __name__ == '__main__':
    unittest.main(verbosity=2)