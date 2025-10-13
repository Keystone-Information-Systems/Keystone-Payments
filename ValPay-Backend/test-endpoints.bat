@echo off
echo Testing ValPay API Endpoints...
echo.

echo 1. Testing basic health endpoint...
curl -X GET http://localhost:5000/health
echo.
echo.

echo 2. Testing simple debug endpoint...
curl -X GET http://localhost:5000/debug/simple
echo.
echo.

echo 3. Testing configuration endpoint...
curl -X GET http://localhost:5000/debug/config
echo.
echo.

echo 4. Testing database endpoint...
curl -X GET http://localhost:5000/debug/db
echo.
echo.

echo 5. Testing request parsing...
curl -X POST http://localhost:5000/debug/test-request ^
  -H "Content-Type: application/json" ^
  -d "{\"amountMinor\": 1000, \"currency\": \"USD\", \"country\": \"US\", \"merchantAccount\": \"test\"}"
echo.
echo.

echo 6. Testing payment methods endpoint...
curl -X POST http://localhost:5000/paymentMethods ^
  -H "Content-Type: application/json" ^
  -d "{\"amountMinor\": 1000, \"currency\": \"USD\", \"country\": \"US\", \"merchantAccount\": \"test\"}"
echo.
echo.

echo Testing complete!
pause
