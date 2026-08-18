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

    def test_do_POST_payments_201(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 201)
        self.assertEqual(response.text, json.dumps({"status": "Success", "payment": payment}))

    def test_do_POST_payments_transaction_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, json.dumps({"error": "Require field missing", "field": "transaction"}))
    
    def test_do_POST_payments_amount_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL"

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, json.dumps({"error": "Require field missing", "field": "amount"}))

    def test_do_POST_payments_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        response = requests.post(f"{self.base_url}/payments", json=data)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, "Unauthorized: Invalid or missing session token")
    
    def test_do_POST_refund_admin_201(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments/refund", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 201)
        self.assertEqual(response.text, json.dumps({"status": "Success", "payment": payment}))

    def test_do_POST_refund_admin_transaction_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments/refund", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, json.dumps({"error": "Require field missing", "field": "transaction"}))

    def test_do_POST_refund_admin_amount_401(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL"

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments/refund", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.text, json.dumps({"error": "Require field missing", "field": "amount"}))

    def test_do_POST_refund_admin_403(self):
        data = {
            "username": "zakaria",
            "password": "secret1234",
            "name": "zakaria",
            "transaction": "IDEAL",
            "amount": 5.99

        }
        requests.post(f"{self.base_url}/register", json=data)
        login_response = requests.post(f"{self.base_url}/login", json=data)
        token = login_response.json().get("session_token")
        header = {"Authorization": f"{token}"}
        response = requests.post(f"{self.base_url}/payments/refund", json=data, headers=header)
        payments = load_payment_data()
        payment = payments[-1]
        self.assertEqual(response.status_code, 403)
        self.assertEqual(response.text, "Access denied")



if __name__ == '__main__':
    unittest.main(verbosity=2)