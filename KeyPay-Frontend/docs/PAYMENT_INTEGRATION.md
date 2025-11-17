# ValPay Frontend Payment Integration Guide

This guide explains how to integrate and use the ValPay frontend payment system.

## Quick Start

### 1. Environment Setup

Create a `.env` file with the following variables:

```env
VITE_API_BASE_URL=https://your-api-gateway-url
VITE_ENVIRONMENT=test
VITE_APP_NAME=ValPay
VITE_APP_VERSION=1.0.0
```

### 2. Payment URL Structure

The payment page accepts the following URL parameters:

```
/payment?amount=1000&currency=USD&country=US&merchant=YourMerchantAccount
```

**Required Parameters:**
- `amount`: Amount in minor units (e.g., 1000 = $10.00)
- `currency`: Currency code (USD, EUR, GBP, etc.)
- `country`: Country code (US, GB, DE, etc.)
- `merchant`: Your merchant account identifier

**Optional Parameters:**
- `reference`: Custom payment reference (auto-generated if not provided)
- `returnUrl`: Custom return URL (defaults to `/payment/result`)

### 3. Payment Flow

#### Step 1: Payment Methods
The system fetches available payment methods from your backend:

```typescript
// API Call: POST /paymentMethods
{
  "amountMinor": 1000,
  "currency": "USD",
  "country": "US",
  "merchantAccount": "YourMerchantAccount"
}
```

#### Step 2: Payment Method Selection
Users select from available payment methods:
- Credit/Debit Cards (scheme)
- iDEAL
- PayPal
- Apple Pay
- Google Pay
- SEPA Direct Debit
- Klarna

#### Step 3: Payment Processing
Payment data is submitted to your backend:

```typescript
// API Call: POST /payments
{
  "reference": "VALPAY_ABC123",
  "amountMinor": 1000,
  "currency": "USD",
  "returnUrl": "https://your-domain.com/payment/result",
  "paymentMethod": {
    "type": "scheme",
    "name": "Credit Card"
  },
  "merchantAccount": "YourMerchantAccount",
  "country": "US"
}
```

#### Step 4: Status Monitoring
The system polls for transaction status:

```typescript
// API Call: GET /transactions/{id}
// Polls every 2 seconds until final status
```

## Integration Examples

### Basic Payment Link

```html
<a href="/payment?amount=2500&currency=USD&country=US&merchant=YourMerchant">
  Pay $25.00
</a>
```

### JavaScript Integration

```javascript
// Redirect to payment page
function initiatePayment(amount, currency, country, merchant) {
  const params = new URLSearchParams({
    amount: amount.toString(),
    currency,
    country,
    merchant
  });
  
  window.location.href = `/payment?${params.toString()}`;
}

// Usage
initiatePayment(1000, 'USD', 'US', 'YourMerchantAccount');
```

### React Component Integration

```tsx
import { useNavigate } from 'react-router-dom';

function PaymentButton({ amount, currency, country, merchant }) {
  const navigate = useNavigate();
  
  const handlePayment = () => {
    const params = new URLSearchParams({
      amount: amount.toString(),
      currency,
      country,
      merchant
    });
    
    navigate(`/payment?${params.toString()}`);
  };
  
  return (
    <button onClick={handlePayment}>
      Pay ${(amount / 100).toFixed(2)}
    </button>
  );
}
```

## Backend API Requirements

Your backend must implement these endpoints:

### 1. Payment Methods Endpoint

**POST** `/paymentMethods`

**Request:**
```json
{
  "amountMinor": 1000,
  "currency": "USD",
  "country": "US",
  "merchantAccount": "YourMerchantAccount"
}
```

**Response:**
```json
{
  "paymentMethods": [
    {
      "type": "scheme",
      "name": "Credit Card",
      "icon": "credit_card",
      "description": "Pay with your credit or debit card"
    },
    {
      "type": "ideal",
      "name": "iDEAL",
      "icon": "account_balance",
      "description": "Pay with iDEAL"
    }
  ],
  "sessionId": "optional-session-id"
}
```

### 2. Create Payment Endpoint

**POST** `/payments`

**Request:**
```json
{
  "reference": "VALPAY_ABC123",
  "amountMinor": 1000,
  "currency": "USD",
  "returnUrl": "https://your-domain.com/payment/result",
  "paymentMethod": {
    "type": "scheme",
    "name": "Credit Card"
  },
  "merchantAccount": "YourMerchantAccount",
  "country": "US"
}
```

**Response:**
```json
{
  "resultCode": "Authorised",
  "pspReference": "ADYEN_PSP_REF",
  "action": null,
  "refusalReason": null
}
```

### 3. Transaction Status Endpoint

**GET** `/transactions/{id}`

**Response:**
```json
{
  "id": "transaction-id",
  "reference": "VALPAY_ABC123",
  "amountMinor": 1000,
  "currency": "USD",
  "status": "SUCCESS",
  "paymentMethod": "scheme",
  "createdAt": "2024-01-01T12:00:00Z",
  "updatedAt": "2024-01-01T12:01:00Z",
  "pspReference": "ADYEN_PSP_REF",
  "resultCode": "Authorised",
  "refusalReason": null
}
```

## Error Handling

The system handles various error scenarios:

### Network Errors
- Automatic retry with exponential backoff
- User-friendly error messages
- Fallback to cached data when possible

### Payment Errors
- Clear error messages for different failure types
- Retry options for temporary failures
- Support contact information

### Validation Errors
- Real-time form validation
- Clear field-level error messages
- Prevention of invalid submissions

## Security Considerations

### HTTPS Only
- All connections must use HTTPS
- Mixed content warnings for HTTP resources

### Content Security Policy
- Strict CSP headers implemented
- Inline script restrictions
- External resource whitelisting

### Data Protection
- No sensitive payment data stored locally
- Secure transmission to Adyen
- PCI DSS compliance through Adyen

## Customization

### Theming
The application uses Material-UI theming. Customize the theme in `src/styles/theme.ts`:

```typescript
export const lightTheme = createTheme({
  palette: {
    primary: {
      main: '#your-brand-color',
    },
    // ... other theme options
  },
});
```

### Styling
Global styles can be customized in `src/styles/globals.css`:

```css
/* Custom payment method icons */
.payment-method-icon {
  width: 32px;
  height: 32px;
}

/* Custom status indicators */
.status-success {
  background-color: #your-success-color;
}
```

### Components
All components are modular and can be customized or replaced:

```tsx
// Custom payment method component
import { PaymentMethodsList } from '@/components/payment/PaymentMethodsList';

function CustomPaymentMethods(props) {
  return (
    <div className="custom-payment-methods">
      <PaymentMethodsList {...props} />
    </div>
  );
}
```

## Testing

### Unit Tests
```bash
npm run test
```

### Integration Tests
```bash
npm run test:integration
```

### E2E Tests
```bash
npm run test:e2e
```

## Deployment

### Development
```bash
npm run dev
```

### Production Build
```bash
npm run build
```

### AWS Deployment
```bash
# Linux/Mac
./scripts/deploy.sh

# Windows
.\scripts\deploy.ps1
```

## Monitoring and Analytics

### Error Tracking
- Automatic error reporting
- User action tracking
- Performance monitoring

### Payment Analytics
- Conversion rate tracking
- Payment method usage statistics
- Error rate monitoring

## Support

For technical support or questions:
- Check the documentation
- Review error logs
- Contact the development team

## Changelog

### Version 1.0.0
- Initial release
- Core payment functionality
- Adyen integration
- Responsive design
- Error handling
- Testing framework
