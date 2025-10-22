# Adyen Web SDK Integration

This frontend now uses the official Adyen Web SDK for secure payment processing.

## What Changed

### ✅ **Removed Custom Components**
- `CardForm.tsx` - Custom card input form
- `TestCardSelector.tsx` - Test card selection component

### ✅ **Added Adyen Web SDK**
- `AdyenDropin.tsx` - Official Adyen Drop-in component
- `@adyen/adyen-web` package installed

### ✅ **Updated Payment Flow**
- Payment form now uses Adyen's secure encryption
- Card details are encrypted by Adyen before sending to backend
- No sensitive card data touches our frontend code

## How It Works

1. **Payment Methods**: Backend returns Adyen payment methods
2. **Adyen Drop-in**: Renders secure payment form with encryption
3. **Card Encryption**: Adyen encrypts card details automatically
4. **Payment Submission**: Encrypted data sent to backend
5. **Processing**: Backend processes with Adyen API

## Environment Variables

```bash
# Required for Adyen Web SDK
VITE_ADYEN_CLIENT_KEY=test_QCJ4F4V6K2VXZJ2G
VITE_ADYEN_ENVIRONMENT=test
```

## Test Cards

Use any of these test card numbers (no need to select them):

| Card Number | Brand | Result |
|-------------|-------|--------|
| `4111 1111 1111 1111` | Visa | ✅ Authorised |
| `4000 0000 0000 0002` | Visa | ❌ Refused |
| `5555 5555 5554 4444` | Mastercard | ✅ Authorised |
| `5200 0000 0000 0007` | Mastercard | ❌ Refused |
| `3782 8224 6310 005` | Amex | ✅ Authorised |

**Expiry**: Any future date (e.g., 12/25)  
**CVC**: Any 3-4 digits (e.g., 123)

## Security Benefits

- ✅ **PCI DSS Compliant** - No card data in our code
- ✅ **Automatic Encryption** - Adyen handles all encryption
- ✅ **3D Secure Support** - Built-in authentication
- ✅ **Fraud Protection** - Adyen's risk management
- ✅ **Tokenization** - Secure card storage options

## Production Setup

1. Get production Adyen client key
2. Change `VITE_ADYEN_ENVIRONMENT=live`
3. Update `VITE_ADYEN_CLIENT_KEY` to production key
4. Remove test card references from UI

The integration is now production-ready and follows Adyen's best practices!
