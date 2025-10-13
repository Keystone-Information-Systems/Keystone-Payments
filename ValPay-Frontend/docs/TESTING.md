# ValPay Frontend Testing Guide

This document provides comprehensive information about testing the ValPay frontend application.

## Test Structure

The ValPay frontend uses a multi-layered testing approach:

- **Unit Tests**: Test individual components and functions in isolation
- **Integration Tests**: Test component interactions and data flow
- **End-to-End (E2E) Tests**: Test complete user journeys across the application

## Test Technologies

- **Vitest**: Fast unit testing framework
- **React Testing Library**: Component testing utilities
- **Playwright**: End-to-end testing framework
- **Jest DOM**: Custom matchers for DOM testing

## Running Tests

### Quick Start

```bash
# Run all tests
npm run test:all

# Or run individual test suites
npm run test          # Unit tests
npm run test:e2e      # End-to-end tests
```

### Available Test Commands

| Command | Description |
|---------|-------------|
| `npm run test` | Run unit tests once |
| `npm run test:watch` | Run unit tests in watch mode |
| `npm run test:coverage` | Run unit tests with coverage report |
| `npm run test:ui` | Run unit tests with UI interface |
| `npm run test:e2e` | Run end-to-end tests |
| `npm run test:e2e:ui` | Run e2e tests with UI interface |
| `npm run test:e2e:headed` | Run e2e tests in headed mode (visible browser) |
| `npm run test:all` | Run all test suites |

### Using Test Scripts

#### Windows (PowerShell)
```powershell
.\scripts\test-all.ps1
```

#### Unix/Linux/macOS
```bash
./scripts/test-all.sh
```

## Test Coverage

### Unit Tests

Located in `src/__tests__/`:

- **Components**: `src/__tests__/components/`
  - `PaymentForm.test.tsx` - Payment form component tests
  - `PaymentMethodsList.test.tsx` - Payment methods selection tests
  - `Button.test.tsx` - UI component tests
  - `Card.test.tsx` - UI component tests

- **Hooks**: `src/__tests__/hooks/`
  - `useAdyen.test.ts` - Adyen integration hook tests
  - `usePaymentMethods.test.ts` - Payment methods hook tests
  - `useCreatePayment.test.ts` - Payment creation hook tests

- **Services**: `src/__tests__/services/`
  - `adyenService.test.ts` - Adyen service tests
  - `paymentService.test.ts` - Payment service tests
  - `api.test.ts` - API service tests

- **Utils**: `src/__tests__/utils/`
  - `formatters.test.ts` - Utility function tests
  - `validation.test.ts` - Validation function tests

### Integration Tests

Located in `src/__tests__/integration/`:

- `PaymentFlow.test.tsx` - Complete payment flow integration tests
- `AdyenIntegration.test.tsx` - Adyen payment integration tests

### End-to-End Tests

Located in `src/__tests__/e2e/`:

- `payment-flow.spec.ts` - Complete user journey tests
- `payment-methods.spec.ts` - Payment method selection tests
- `error-handling.spec.ts` - Error scenario tests

## Test Configuration

### Vitest Configuration (`vitest.config.ts`)

```typescript
export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
```

### Playwright Configuration (`playwright.config.ts`)

```typescript
export default defineConfig({
  testDir: './src/__tests__/e2e',
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
    { name: 'Mobile Chrome', use: { ...devices['Pixel 5'] } },
  ],
});
```

## Writing Tests

### Unit Test Example

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PaymentForm } from '@/components/payment/PaymentForm';

describe('PaymentForm', () => {
  it('renders payment form with correct information', () => {
    const mockProps = {
      paymentMethod: { type: 'scheme', name: 'Credit Card' },
      amountMinor: 1000,
      currency: 'USD',
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };

    render(<PaymentForm {...mockProps} />);
    
    expect(screen.getByText('Credit Card')).toBeInTheDocument();
    expect(screen.getByText('$10.00')).toBeInTheDocument();
  });
});
```

### E2E Test Example

```typescript
import { test, expect } from '@playwright/test';

test('should complete payment flow', async ({ page }) => {
  await page.goto('/payment?amount=1000&currency=USD');
  
  await page.getByRole('button', { name: /Credit Card/i }).click();
  await expect(page.getByText('Payment Details')).toBeVisible();
  
  await page.getByRole('button', { name: 'Pay $10.00' }).click();
  await expect(page.getByText('Payment successful')).toBeVisible();
});
```

## Mocking

### API Mocking

```typescript
// Mock API responses
vi.mock('@/services/api', () => ({
  createPayment: vi.fn().mockResolvedValue({
    success: true,
    transactionId: 'test-123',
  }),
}));
```

### Adyen Mocking

```typescript
// Mock Adyen service
vi.mock('@/services/adyenService', () => ({
  AdyenService: {
    getInstance: vi.fn(() => ({
      createComponent: vi.fn(),
      mountComponent: vi.fn(),
      isAvailable: vi.fn(() => true),
    })),
  },
}));
```

## Test Data

### Mock Payment Methods

```typescript
const mockPaymentMethods = [
  {
    type: 'scheme',
    name: 'Credit Card',
    icon: 'credit_card',
    description: 'Pay with your credit or debit card',
    configuration: {},
  },
  {
    type: 'ideal',
    name: 'iDEAL',
    icon: 'account_balance',
    description: 'Pay with iDEAL',
    configuration: {},
  },
];
```

### Mock Payment Data

```typescript
const mockPaymentRequest = {
  reference: 'test-ref-123',
  amountMinor: 1000,
  currency: 'USD',
  paymentMethod: mockPaymentMethods[0],
  merchantAccount: 'test-merchant',
  country: 'US',
};
```

## Continuous Integration

### GitHub Actions Example

```yaml
name: Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '18'
      - run: npm ci
      - run: npm run test:all
```

## Debugging Tests

### Unit Tests

```bash
# Run specific test file
npm run test PaymentForm.test.tsx

# Run tests in watch mode
npm run test:watch

# Run with UI for debugging
npm run test:ui
```

### E2E Tests

```bash
# Run in headed mode to see browser
npm run test:e2e:headed

# Run specific test
npx playwright test payment-flow.spec.ts

# Debug mode
npx playwright test --debug
```

## Best Practices

### 1. Test Structure
- Use descriptive test names
- Group related tests with `describe` blocks
- Follow AAA pattern: Arrange, Act, Assert

### 2. Mocking
- Mock external dependencies
- Use realistic test data
- Keep mocks simple and focused

### 3. Assertions
- Test behavior, not implementation
- Use semantic queries (getByRole, getByLabelText)
- Test error states and edge cases

### 4. E2E Tests
- Test complete user journeys
- Use realistic data and scenarios
- Test across different browsers and devices

## Troubleshooting

### Common Issues

1. **Tests failing due to missing mocks**
   - Ensure all external dependencies are mocked
   - Check test setup files

2. **E2E tests timing out**
   - Increase timeout in test configuration
   - Add proper wait conditions

3. **Coverage not showing**
   - Run `npm run test:coverage`
   - Check coverage configuration

### Getting Help

- Check test output for specific error messages
- Review test setup files
- Consult testing library documentation
- Check Playwright documentation for e2e issues

## Performance Testing

### Load Testing

```bash
# Run performance tests
npm run test:performance
```

### Bundle Analysis

```bash
# Analyze bundle size
npm run build
npm run analyze
```

## Security Testing

### Dependency Scanning

```bash
# Check for vulnerabilities
npm audit

# Fix vulnerabilities
npm audit fix
```

This testing guide ensures comprehensive coverage of the ValPay frontend application, from individual components to complete user journeys.
