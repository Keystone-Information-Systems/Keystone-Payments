@echo off
echo ========================================
echo ValPay API Debug Script
echo ========================================
echo.

set API_URL=http://localhost:5000
echo Testing API at: %API_URL%
echo.

echo 1. Testing basic connectivity...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/" || echo "FAILED: Cannot connect to API"
echo.

echo 2. Testing health endpoint...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/health" || echo "FAILED: Health check failed"
echo.

echo 3. Testing debug/simple endpoint...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/debug/simple" || echo "FAILED: Simple debug failed"
echo.

echo 4. Testing debug/config endpoint...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/debug/config" || echo "FAILED: Config debug failed"
echo.

echo 5. Testing debug/db endpoint...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/debug/db" || echo "FAILED: Database debug failed"
echo.

echo 6. Testing comprehensive health check...
curl -s -w "Status: %%{http_code}\n" "%API_URL%/debug/health" || echo "FAILED: Comprehensive health check failed"
echo.

echo 7. Testing payment methods endpoint with sample data...
curl -s -X POST -H "Content-Type: application/json" -w "Status: %%{http_code}\n" -d "{\"amountMinor\":1000,\"currency\":\"USD\",\"country\":\"US\",\"orderId\":\"test-order-123\",\"merchantAccount\":\"Keystone\"}" "%API_URL%/paymentMethods" || echo "FAILED: Payment methods test failed"
echo.

echo ========================================
echo Debug script completed
echo ========================================
pause
