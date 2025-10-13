# ValPay - AWS Serverless Payment Integration

A .NET 8 AWS Lambda solution for integrating with Adyen payment service provider, designed to bridge legacy ASP/Universe systems with modern payment processing.

## Architecture

```
Legacy ASP/Universe → .NET 8 APIs (Lambda) → Adyen PSP
                           ↓
                    Aurora PostgreSQL
                           ↓
                    S3 + CloudFront (Frontend)
```

## Project Structure

```
ValPay/
├── ValPay.sln
├── Directory.Build.props
├── .gitignore
├── README.md
├── src/
│   ├── ValPay.Domain/          # Domain models and business logic
│   ├── ValPay.Application/     # Use cases and contracts
│   ├── ValPay.Infrastructure/  # External services (Adyen, Database)
│   ├── ValPay.Api/            # API Lambda (Minimal API)
│   └── ValPay.Webhook/        # Webhook Lambda
└── tests/
    └── ValPay.Tests/          # Unit tests
```

## Features

- **API Lambda**: 3 routes for payment processing
  - `POST /paymentMethods` - Get available payment methods from Adyen
  - `POST /payments` - Create payment with Adyen
  - `GET /transactions/{id}` - Retrieve transaction status
- **Webhook Lambda**: Handles Adyen notifications
- **Database**: PostgreSQL with Dapper for data persistence
- **Configuration**: User secrets for sensitive data

## Quick Start

### Prerequisites

- .NET 8 SDK
- AWS CLI configured
- PostgreSQL database
- Adyen test account

### Installation

1. Install AWS Lambda tools globally:
```bash
dotnet tool install -g Amazon.Lambda.Tools
```

2. Restore packages:
```bash
dotnet restore
```

3. Set up user secrets for API project:
```bash
cd src/ValPay.Api
dotnet user-secrets init
dotnet user-secrets set "Adyen:ApiKey" "YOUR_ADYEN_API_KEY"
dotnet user-secrets set "Adyen:MerchantAccount" "YOUR_MERCHANT_ACCOUNT"
```

### Local Development

Run the API locally:
```bash
cd src/ValPay.Api
dotnet run
```

The API will be available at `https://localhost:5001` (or the port shown in console).

### Testing

Run unit tests:
```bash
dotnet test
```

### Deployment

Deploy API Lambda:
```bash
cd src/ValPay.Api
dotnet lambda deploy-function
```

Deploy Webhook Lambda:
```bash
cd src/ValPay.Webhook
dotnet lambda deploy-function
```

## Configuration

### Environment Variables

- `ConnectionStrings__Postgres` - PostgreSQL connection string
- `Adyen__BaseUrl` - Adyen API base URL (default: test environment)
- `Adyen__ApiKey` - Adyen API key
- `Adyen__MerchantAccount` - Adyen merchant account
- `Adyen__HmacKey` - HMAC key for webhook verification
- `Tenant__Id` - Default tenant ID

### Database Schema

The solution creates the following tables:
- `tenants` - Multi-tenant configuration
- `transactions` - Payment transactions
- `operations` - Webhook events and operations

## API Endpoints

### Payment Methods
```http
POST /paymentMethods
Content-Type: application/json

{
  "amountMinor": 1000,
  "currency": "USD",
  "country": "US",
  "merchantAccount": "YourMerchantAccount"
}
```

### Create Payment
```http
POST /payments
Content-Type: application/json

{
  "reference": "ORDER-123",
  "amountMinor": 1000,
  "currency": "USD",
  "returnUrl": "https://yoursite.com/return",
  "paymentMethod": { /* Adyen encrypted payment method */ }
}
```

### Get Transaction
```http
GET /transactions/{transactionId}
```

## Webhook

The webhook endpoint accepts Adyen notifications and:
1. Verifies HMAC signature (TODO: implement)
2. Stores the event in the operations table
3. Updates transaction status based on the event

## Development Notes

- Uses C# 12 with nullable reference types enabled
- Warnings are treated as errors
- Implicit usings are enabled
- Invariant globalization for Lambda compatibility
- Polly for resilience patterns (ready for implementation)

## Security Considerations

- Store sensitive data in AWS Secrets Manager or Parameter Store
- Implement proper HMAC verification for webhooks
- Use HTTPS for all communications
- Follow PCI DSS guidelines for payment data

## Contributing

1. Follow the existing code style
2. Add tests for new functionality
3. Update documentation as needed
4. Ensure all tests pass before submitting

## License

Internal Valsoft Corporation project.

