export const API_ENDPOINTS = {
  PAYMENT_METHODS: '/paymentMethods',
  PAYMENTS: '/payments',
  TRANSACTIONS: '/transactions',
  CANCEL_PAYMENT: (id: string) => `/payments/${id}/cancel`,
} as const;

// Match backend KeyPay.Domain.TransactionStatus exactly
export const TRANSACTION_STATUS = {
  PENDING: 'Pending',
  AUTHORISED: 'Authorised',
  REFUSED: 'Refused',
  CAPTURED: 'Captured',
  CANCELLED: 'Cancelled',
} as const;

export const PAYMENT_METHOD_TYPES = {
  SCHEME: 'scheme',
  IDEAL: 'ideal',
  PAYPAL: 'paypal',
  APPLE_PAY: 'applepay',
  GOOGLE_PAY: 'googlepay',
  SEPA: 'sepadirectdebit',
  KLARNA: 'klarna',
} as const;

export const CURRENCIES = {
  USD: 'USD',
  EUR: 'EUR',
  GBP: 'GBP',
  CAD: 'CAD',
  AUD: 'AUD',
} as const;

export const COUNTRIES = {
  US: 'US',
  GB: 'GB',
  DE: 'DE',
  FR: 'FR',
  NL: 'NL',
  CA: 'CA',
  AU: 'AU',
} as const;

export const APP_CONFIG = {
  NAME: import.meta.env.VITE_APP_NAME || 'KeyPay',
  VERSION: import.meta.env.VITE_APP_VERSION || '1.0.0',
  API_BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000', // Match backend actual port
  ADYEN_CLIENT_KEY: import.meta.env.VITE_ADYEN_CLIENT_KEY || '',
  ENVIRONMENT: import.meta.env.VITE_ENVIRONMENT || 'test',
} as const;

export const POLLING_INTERVALS = {
  TRANSACTION_STATUS: 2000, // 2 seconds
  PAYMENT_METHODS: 30000, // 30 seconds
} as const;

export const TIMEOUTS = {
  API_REQUEST: 30000, // 30 seconds
  PAYMENT_PROCESSING: 120000, // 2 minutes
} as const;

// Test Cards from backend documentation
export const TEST_CARDS = {
  VISA_SUCCESS: {
    number: '4111111111111111',
    encryptedNumber: 'test_4111111111111111',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '737',
    holderName: 'Test User',
    brand: 'visa',
    description: 'Visa - Always Authorized',
    expectedResult: 'Authorised' as const,
  },
  VISA_DECLINED: {
    number: '4000000000000002',
    encryptedNumber: 'test_4000000000000002',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '737',
    holderName: 'Test User',
    brand: 'visa',
    description: 'Visa - Always Declined',
    expectedResult: 'Refused' as const,
  },
  VISA_ERROR: {
    number: '4000000000000119',
    encryptedNumber: 'test_4000000000000119',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '737',
    holderName: 'Test User',
    brand: 'visa',
    description: 'Visa - Processing Error',
    expectedResult: 'Error' as const,
  },
  MASTERCARD_SUCCESS: {
    number: '5555555555554444',
    encryptedNumber: 'test_5555555555554444',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '737',
    holderName: 'Test User',
    brand: 'mc',
    description: 'Mastercard - Always Authorized',
    expectedResult: 'Authorised' as const,
  },
  MASTERCARD_DECLINED: {
    number: '5200000000000007',
    encryptedNumber: 'test_5200000000000007',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '737',
    holderName: 'Test User',
    brand: 'mc',
    description: 'Mastercard - Always Declined',
    expectedResult: 'Refused' as const,
  },
  AMEX_SUCCESS: {
    number: '378282246310005',
    encryptedNumber: 'test_378282246310005',
    expiryMonth: '03',
    expiryYear: '2030',
    cvc: '7373',
    holderName: 'Test User',
    brand: 'amex',
    description: 'American Express - Always Authorized',
    expectedResult: 'Authorised' as const,
  },
} as const;

export const TEST_CARD_LIST = Object.values(TEST_CARDS);
