# ValPay Test Cards Reference

This document provides a comprehensive list of test cards available for testing the ValPay payment integration with Adyen.

## Available Test Cards

| Card Number | Brand | Expected Result | Description | Expiry | CVC |
|-------------|-------|----------------|-------------|---------|-----|
| `4111 1111 1111 1111` | Visa | ✅ Authorised | Always successful - Use for positive testing | 03/2030 | 737 |
| `4000 0000 0000 0002` | Visa | ❌ Refused | Always declined - Use for error handling testing | 03/2030 | 737 |
| `4000 0000 0000 0119` | Visa | ⚠️ Error | Processing error - Use for exception testing | 03/2030 | 737 |
| `5555 5555 5554 4444` | Mastercard | ✅ Authorised | Always successful - Use for positive testing | 03/2030 | 737 |
| `5200 0000 0000 0007` | Mastercard | ❌ Refused | Always declined - Use for error handling testing | 03/2030 | 737 |
| `3782 8224 6310 005` | American Express | ✅ Authorised | Always successful - Use for positive testing | 03/2030 | 7373 |

## Usage Instructions

### Frontend Integration
These test cards are pre-configured in the frontend application and can be selected from the "Test Cards" section during payment. The cards will automatically populate the payment form with the correct details.

### Backend Processing
The backend receives these card numbers in encrypted format as `test_{cardNumber}` and processes them according to Adyen's test environment rules.

### Test Scenarios

#### ✅ Successful Payments
Use these cards to test successful payment flows:
- Visa: `4111 1111 1111 1111`
- Mastercard: `5555 5555 5554 4444`
- American Express: `3782 8224 6310 005`

#### ❌ Declined Payments
Use these cards to test payment decline scenarios:
- Visa: `4000 0000 0000 0002`
- Mastercard: `5200 0000 0000 0007`

#### ⚠️ Error Handling
Use this card to test error handling:
- Visa: `4000 0000 0000 0119`

## Important Notes

- **Test Environment Only**: These cards only work in Adyen's test environment
- **No Real Charges**: No actual money will be processed with these cards
- **Cardholder Name**: Use "Test User" or any name for testing
- **Encryption**: The frontend automatically encrypts card data with `test_` prefix for the backend
- **Expiry Dates**: All test cards use 03/2030 as expiry date
- **CVC Codes**: Visa/Mastercard use 737, American Express uses 7373

## Integration Details

### Frontend Constants
Test cards are defined in `src/utils/constants.ts` as `TEST_CARDS` object.

### Backend Processing
The backend expects encrypted card data in the format:
```json
{
  "type": "scheme",
  "encryptedCardNumber": "test_4111111111111111",
  "encryptedExpiryMonth": "test_03",
  "encryptedExpiryYear": "test_2030",
  "encryptedSecurityCode": "test_737",
  "holderName": "Test User"
}
```

### Expected Responses
- **Authorised**: Payment successful, transaction status = "Authorised"
- **Refused**: Payment declined, transaction status = "Refused"
- **Error**: Processing error, may result in various error states

## Testing Workflow

1. **Start Payment Flow**: Navigate to the demo page and configure payment
2. **Select Test Card**: Choose from the available test cards or enter manually
3. **Submit Payment**: Process the payment and observe the result
4. **Verify Status**: Check transaction status matches expected result
5. **Test Error Handling**: Ensure proper error messages and recovery options

---

*Last Updated: October 2024*
*Environment: Adyen Test Environment*
*Version: ValPay 1.0.0*
