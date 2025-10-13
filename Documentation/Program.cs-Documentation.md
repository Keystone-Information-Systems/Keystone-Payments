# ValPay API - Program.cs Documentation

## Overview
This is the main entry point for your ValPay payment processing API. It's built using ASP.NET Core and designed to run on AWS Lambda. The application handles payment method retrieval, payment creation, and transaction management through the Adyen payment gateway.

## Application Structure

### 1. Service Configuration (Lines 11-33)
The application sets up all necessary services:

- **Logging**: Console and AWS Lambda logging
- **Database**: PostgreSQL connection with Dapper ORM
- **Idempotency**: Prevents duplicate requests
- **HTTP Client**: Configured for Adyen API calls
- **AWS Lambda**: Hosting configuration

### 2. Application Startup (Lines 34-66)
- Runs startup diagnostics
- Initializes the database
- Sets up middleware pipeline:
  - Request logging middleware
  - Developer exception page (development only)
  - Custom error handling middleware

### 3. API Endpoints

#### Health & Debug Endpoints
- `GET /` - Simple "API is running" message
- `GET /health` - Health check endpoint
- `GET /debug/config` - Shows configuration status
- `GET /debug/db` - Tests database connectivity
- `GET /debug/simple` - Basic endpoint test
- `POST /debug/test-request` - Tests request handling

#### Main Payment Endpoints

##### `POST /paymentMethods` (Lines 107-180)
**Purpose**: Retrieves available payment methods from Adyen

**Flow**:
1. Validates request (amount > 0, currency required)
2. Checks for duplicate requests (idempotency)
3. Creates pending transaction in database
4. Calls Adyen API for payment methods
5. Generates payment URL for frontend
6. Caches response for idempotency
7. Returns payment URL and session ID

**Key Features**:
- Comprehensive logging with correlation IDs
- Performance monitoring with stopwatch
- Error handling and validation
- Idempotency support

##### `POST /payments` (Lines 182-232)
**Purpose**: Creates a payment transaction

**Flow**:
1. Checks idempotency
2. Creates pending transaction record
3. Calls Adyen to process payment
4. Updates transaction status based on result
5. Caches response
6. Returns transaction details

##### `GET /transactions/{id}` (Lines 234-259)
**Purpose**: Retrieves transaction details by ID

**Flow**:
1. Queries database for transaction
2. Returns transaction data or 404 if not found

## Key Technologies Used

- **ASP.NET Core**: Web API framework
- **AWS Lambda**: Serverless hosting
- **PostgreSQL**: Database with Dapper ORM
- **Adyen**: Payment processing gateway
- **Idempotency**: Prevents duplicate operations

## Configuration Requirements

The application expects these configuration values:
- `Adyen:BaseUrl` - Adyen API endpoint
- `Adyen:ApiKey` - Adyen API key
- `Adyen:MerchantAccount` - Merchant account ID
- `Postgres` - Database connection string
- `Tenant:Id` - Tenant identifier
- `Frontend:BaseUrl` - Frontend application URL

## Debugging Tips

1. **Use Debug Mode**: As you discovered, debug mode allows breakpoints to work properly
2. **Check Logs**: The application has extensive logging with correlation IDs
3. **Test Endpoints**: Use the `/debug/*` endpoints to verify configuration and connectivity
4. **Database**: The `/debug/db` endpoint tests your database connection

## Error Handling

- Custom `ErrorHandlingMiddleware` processes all exceptions
- Comprehensive logging with correlation IDs for tracing
- Graceful error responses with proper HTTP status codes
- Database connection error handling during startup

This API is designed to be robust, traceable, and production-ready with proper error handling and monitoring capabilities.
