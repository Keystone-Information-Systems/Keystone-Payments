# ValPay Backend API Contract - Adyen Advanced Flow

## Overview
This document defines the API contract between the ValPay frontend and backend for implementing Adyen's Advanced flow with Web Drop-in.

## Environment Setup
- **Frontend URL**: `http://localhost:3000` (development)
- **Backend URL**: `http://localhost:5000` (as defined in VITE_API_BASE)
- **Adyen Environment**: `test` (change to `live` for production)

## API Endpoints

### 1. POST /paymentMethods
**Purpose**: Get available payment methods for an order

**Request Body**:
```json
{
  "orderId": "string"  // Required: Order identifier from frontend
}
```

**Response**:
```json
{
  "paymentMethods": {},  // Adyen's paymentMethods JSON response
  "sessionId": "string", // Your bridge's session marker
  "reference": "string", // Server-locked order ID
  "amount": {
    "value": 1000,      // Amount in minor units (e.g., cents)
    "currency": "USD"   // 3-letter currency code
  },
  "countryCode": "US"   // 2-letter country code
}
```

**Backend Implementation Notes**:
- Resolve `merchantAccount` based on tenant/auth
- Call Adyen `/v71/paymentMethods` with server-locked values:
  ```json
  {
    "amount": { "value": 1000, "currency": "USD" },
    "countryCode": "US",
    "merchantAccount": "YourMerchantAccount",
    "channel": "Web"
  }
  ```
- Return the complete Adyen response in `paymentMethods` field
- Generate and return your own `sessionId` and `reference`

### 2. POST /payments
**Purpose**: Create a payment with selected payment method

**Request Body**:
```json
{
  "reference": "string",           // From /paymentMethods response
  "amountMinor": 1000,             // From /paymentMethods response
  "currency": "USD",               // From /paymentMethods response
  "country": "US",                 // From /paymentMethods response
  "returnUrl": "http://localhost:3000/payment/return", // Frontend origin + "/payment/return"
  "paymentMethod": {}              // From Drop-in state.data.paymentMethod
}
```

**Response**:
```json
{
  "resultCode": "string",    // Optional: "Authorised", "Refused", etc.
  "pspReference": "string",  // Optional: Adyen's payment reference
  "action": {}               // Optional: 3DS/redirect action object
}
```

**Backend Implementation Notes**:
- Add `origin: "http://localhost:3000"` (from returnUrl)
- Add `channel: "Web"`
- Add `Idempotency-Key` header (generate UUID)
- Call Adyen `/v71/payments` with:
  ```json
  {
    "paymentMethod": {}, // From request
    "amount": { "value": 1000, "currency": "USD" },
    "reference": "string",
    "countryCode": "US",
    "returnUrl": "http://localhost:3000/payment/return",
    "origin": "http://localhost:3000",
    "channel": "Web",
    "merchantAccount": "YourMerchantAccount"
  }
  ```
- Return either `action` (for 3DS/redirect) or final `resultCode`

### 3. POST /payments/details
**Purpose**: Submit additional details for 3DS/redirect flows

**Request Body**:
```json
{
  "details": {},           // From Drop-in onAdditionalDetails
  "paymentData": "string"  // Optional: From Drop-in onAdditionalDetails
}
```

**Response**:
```json
{
  "resultCode": "string",    // Optional: Final result
  "pspReference": "string",  // Optional: Adyen's payment reference
  "action": {}               // Optional: Additional action if needed
}
```

**Backend Implementation Notes**:
- Forward `details` and `paymentData` directly to Adyen `/v71/payments/details`
- Return the complete Adyen response

## Security Requirements

### Server-Side Validation
- **NEVER** trust client-provided monetary values
- Always use server-locked `amount`, `currency`, `reference`, `countryCode`
- Validate `orderId` exists and belongs to authenticated user
- Implement proper authentication/authorization

### CORS Configuration
- Allow `http://localhost:3000` for development
- Allow your production domain for live environment
- Include proper headers: `Content-Type: application/json`

### Headers
- Include `Idempotency-Key` header for `/payments` endpoint
- Use `Content-Type: application/json` for all endpoints
- Implement proper error handling and logging

## Error Handling

### HTTP Status Codes
- `200`: Success
- `400`: Bad Request (invalid data)
- `401`: Unauthorized (missing/invalid auth)
- `404`: Not Found (orderId not found)
- `500`: Internal Server Error

### Error Response Format
```json
{
  "error": "string",
  "message": "string",
  "code": "string"
}
```

## Testing

### Test URLs
- Frontend: `http://localhost:3000/payment?orderId=ORD-123`
- Backend: `http://localhost:5000`

### Test Flow
1. Frontend calls `/paymentMethods` with `orderId`
2. Backend returns payment methods + locked values
3. User selects payment method in Drop-in
4. Frontend calls `/payments` with payment method data
5. Backend processes with Adyen and returns action or result
6. If action, Drop-in handles 3DS/redirect
7. Frontend calls `/payments/details` for additional details
8. Backend forwards to Adyen and returns final result

## Production Considerations

### Environment Variables
- `ADYEN_API_KEY`: Your Adyen API key
- `ADYEN_MERCHANT_ACCOUNT`: Your merchant account
- `ADYEN_ENVIRONMENT`: `live` for production
- `FRONTEND_ORIGIN`: Your production frontend URL

### Security
- Use HTTPS in production
- Implement rate limiting
- Add request logging and monitoring
- Validate all inputs server-side

## Adyen Documentation References
- [Advanced flow overview](https://docs.adyen.com/online-payments/advanced-flow)
- [Web Drop-in + 3DS redirect](https://docs.adyen.com/online-payments/web-drop-in)
- [API Explorer](https://docs.adyen.com/api-explorer)
- [Client key & allowed origins](https://docs.adyen.com/user-management/client-side-authentication)
