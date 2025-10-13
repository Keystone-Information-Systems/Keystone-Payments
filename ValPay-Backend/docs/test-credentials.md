# ValPay Test Credentials

## Adyen Test Card Numbers

When testing payments with Adyen's test environment, use these official test card numbers:

### Visa Test Cards
- **Original**: `4111111111111111` → **Encrypted**: `test_4111111111111111` - Always authorized
- **Original**: `4000000000000002` → **Encrypted**: `test_4000000000000002` - Always declined
- **Original**: `4000000000000119` → **Encrypted**: `test_4000000000000119` - Processing error

### Mastercard Test Cards
- **Original**: `5555555555554444` → **Encrypted**: `test_5555555555554444` - Always authorized
- **Original**: `5200000000000007` → **Encrypted**: `test_5200000000000007` - Always declined
- **Original**: `5200000000000127` → **Encrypted**: `test_5200000000000127` - Processing error

### American Express Test Cards
- **Original**: `378282246310005` → **Encrypted**: `test_378282246310005` - Always authorized
- **Original**: `370000000000002` → **Encrypted**: `test_370000000000002` - Always declined

### Discover Test Cards
- **Original**: `6011111111111117` → **Encrypted**: `test_6011111111111117` - Always authorized
- **Original**: `6011000000000004` → **Encrypted**: `test_6011000000000004` - Always declined

## Frontend vs Backend Usage

### Frontend (User Input)
Users would enter the **original** card numbers in your payment form:
- **Card Number**: `4111111111111111`
- **Expiry Month**: `03`
- **Expiry Year**: `2030`
- **CVV**: `737`

### Backend (API Testing)
For direct API testing, use the **encrypted** versions:
```json
{
    "type": "scheme",
    "encryptedCardNumber": "test_4111111111111111",
    "encryptedExpiryMonth": "test_03",
    "encryptedExpiryYear": "test_2030",
    "encryptedSecurityCode": "test_737"
}
```

## Test Card Details

### Required Fields for Testing
```json
{
    "type": "scheme",
    "encryptedCardNumber": "test_4111111111111111",
    "encryptedExpiryMonth": "test_03",
    "encryptedExpiryYear": "test_2030",
    "encryptedSecurityCode": "test_737"
}
```

### Test Security Codes
- **`test_737`** - Valid security code
- **`test_000`** - Invalid security code

### Test Expiry Dates
- **Month**: `test_03` (any month works with test_ prefix)
- **Year**: `test_2030` (any future year works with test_ prefix)

## Important Notes

1. **Test Prefix Required**: All test values must start with `test_`
2. **Test Environment Only**: These only work with Adyen's test API (`checkout-test.adyen.com`)
3. **No Real Money**: These cards never charge real money
4. **Consistent Results**: Same card number always returns the same result

## API Endpoints for Testing

### Payment Methods
```
POST http://localhost:5000/paymentMethods
```

### Create Payment
```
POST http://localhost:5000/payments
```

### Get Transaction
```
GET http://localhost:5000/transactions/{transaction-id}
```

## Test Scenarios

### Successful Payment
Use `test_4111111111111111` with any valid test details

### Declined Payment
Use `test_4000000000000002` to test declined scenarios

### Processing Error
Use `test_4000000000000119` to test error handling
