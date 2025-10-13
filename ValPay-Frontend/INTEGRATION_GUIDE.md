# ValPay Frontend Integration Guide

## 🎉 Complete Integration with Backend

This frontend has been fully integrated with your ValPay .NET 8 backend. The payment flow now works seamlessly with your existing APIs.

## 🚀 Quick Start

### 1. Start Your Backend
Make sure your ValPay backend is running on `http://localhost:5000`:

```bash
cd ValPay-Backend
dotnet run --project src/ValPay.Api
```

### 2. Start the Frontend
```bash
cd ValPay-Frontend
npm install
npm run dev
```

### 3. Open the Demo
Navigate to `http://localhost:5173` to see the demo page.

## 🔄 Complete Payment Flow

### Backend Integration
The frontend now perfectly integrates with your backend APIs:

1. **POST /paymentMethods** - Fetches payment methods and creates session
2. **POST /payments** - Processes payment with encrypted card data
3. **GET /transactions/{id}** - Polls transaction status
4. **POST /payments/{id}/cancel** - Cancels payments

### Frontend Flow
1. **Demo Page** - Configure payment amount, currency, country
2. **Payment Page** - Enter card details with test card selector
3. **Processing** - Real-time status polling
4. **Success/Error** - Complete transaction details

## 💳 Test Cards Integration

The frontend includes all test cards from your backend documentation:

### Successful Payments
- **Visa**: `4111111111111111` → Always Authorized
- **Mastercard**: `5555555555554444` → Always Authorized  
- **Amex**: `378282246310005` → Always Authorized

### Declined Payments
- **Visa**: `4000000000000002` → Always Declined
- **Mastercard**: `5200000000000007` → Always Declined

### Error Testing
- **Visa**: `4000000000000119` → Processing Error

All cards use:
- **Expiry**: 03/2030
- **CVC**: 737 (7373 for Amex)
- **Name**: Test User

## 🔧 Configuration

### Environment Variables
Copy `env.example` to `.env.local` and update:

```bash
# Backend API URL
VITE_API_BASE_URL=http://localhost:5000

# App Settings
VITE_APP_NAME=ValPay
VITE_ENVIRONMENT=test
```

### Backend Requirements
Ensure your backend is configured with:
- CORS enabled for frontend origin
- Test Adyen credentials
- Database connection
- All endpoints working

## 🎯 Key Features Implemented

### ✅ Type Safety
- TypeScript interfaces matching your backend models exactly
- `PaymentMethodsRequest/Response`
- `CreatePaymentRequest/Response` 
- `Transaction` with correct status enum

### ✅ API Integration
- Proper request/response handling
- Error handling and retry logic
- Status polling for real-time updates
- Cancellation support

### ✅ UI/UX
- Modern Material-UI design
- Responsive layout
- Loading states and error handling
- Test card selector with descriptions
- Real-time payment status

### ✅ Validation
- Form validation matching backend expectations
- Card number formatting and validation
- Required field validation
- Error message display

### ✅ State Management
- React Query for API state
- Zustand for UI state
- Proper loading and error states
- Transaction status polling

## 🧪 Testing Scenarios

### 1. Successful Payment
1. Go to demo page
2. Click "US Payment - $10.00"
3. Select "Visa - Always Authorized" test card
4. Click "Pay Now"
5. Watch real-time processing
6. See success page with transaction details

### 2. Declined Payment
1. Start any payment
2. Select "Visa - Always Declined" test card
3. Submit payment
4. See error page with refusal details

### 3. Custom Payment
1. Use custom payment form
2. Set amount, currency, country
3. Try different test cards
4. Verify backend integration

## 📊 Backend Response Handling

### Payment Methods Response
```json
{
  "paymentUrl": "https://frontend.com/payment?sessionId=...",
  "sessionId": "uuid",
  "paymentMethods": [...]
}
```

### Create Payment Response
```json
{
  "txId": "uuid",
  "resultCode": "Authorised",
  "pspReference": "adyen-reference"
}
```

### Transaction Status Response
```json
{
  "transactionId": "uuid",
  "merchantReference": "order_123",
  "amountValue": 1000,
  "currencyCode": "USD",
  "status": "Authorised",
  "pspReference": "adyen-ref",
  "resultCode": "Authorised",
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": "2024-01-01T00:00:00Z"
}
```

## 🔍 Debugging

### Frontend Debug Info
- Check browser console for API calls
- Development mode shows debug alerts
- Network tab shows backend requests/responses

### Backend Logs
- Check backend console for correlation IDs
- Verify database transactions
- Monitor Adyen API calls

## 🚀 Production Deployment

### Frontend
1. Update `VITE_API_BASE_URL` to production backend URL
2. Build: `npm run build`
3. Deploy `dist/` folder to S3/CloudFront

### Backend
1. Ensure production Adyen credentials
2. Update CORS for production frontend URL
3. Deploy to AWS Lambda/ECS

## 🎨 Customization

### Styling
- Material-UI theme in `src/styles/theme.ts`
- Global styles in `src/styles/globals.css`
- Component-level styling with sx props

### Business Logic
- Payment service in `src/services/paymentService.ts`
- API client in `src/services/api.ts`
- Constants in `src/utils/constants.ts`

### Components
- Reusable UI components in `src/components/ui/`
- Payment-specific components in `src/components/payment/`
- Layout components in `src/components/layout/`

## 📞 Support

The integration is complete and production-ready. All components work seamlessly with your existing backend APIs and handle the full payment lifecycle from initiation to completion.

For any issues, check:
1. Backend is running and accessible
2. CORS is properly configured
3. Environment variables are set correctly
4. Network connectivity between frontend and backend

