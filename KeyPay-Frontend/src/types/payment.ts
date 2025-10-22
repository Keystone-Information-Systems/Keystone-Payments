// Backend API Types - matching ValPay.Application.PaymentsContracts exactly

export interface PaymentMethodsRequest {
  amountMinor: number;
  currency: string;
  country: string;
  merchantAccount: string;
  orderId: string;
}

export interface PaymentMethodsResponse {
  paymentUrl: string;
  sessionId: string;
  paymentMethods: any; // Adyen's raw response
  amount?: number; // For Advanced Flow
  currency?: string; // For Advanced Flow
}

export interface CreatePaymentRequest {
  reference: string;
  amountMinor: number;
  currency: string;
  returnUrl: string;
  paymentMethod: any; // Adyen encrypted blob as object
  country?: string;
}

export interface CreatePaymentResponse {
  txId: string;
  resultCode: string;
  pspReference?: string;
}

// Adyen Types
export interface AdyenPaymentMethod {
  type: string;
  name: string;
  brands?: string[];
  configuration?: Record<string, any>;
  details?: AdyenPaymentMethodDetail[];
  group?: {
    name: string;
    paymentMethodData: string;
  };
}

export interface AdyenPaymentMethodDetail {
  key: string;
  type: string;
  optional?: boolean;
}

export interface AdyenPaymentMethodData {
  type: string;
  encryptedCardNumber?: string;
  encryptedExpiryMonth?: string;
  encryptedExpiryYear?: string;
  encryptedSecurityCode?: string;
  holderName?: string;
  // For other payment methods
  [key: string]: any;
}

// Transaction Types - matching backend database schema
export interface Transaction {
  transactionId: string;
  tenantId: string;
  merchantReference: string;
  amountValue: number;
  currencyCode: string;
  status: TransactionStatus;
  pspReference?: string;
  resultCode?: string;
  refusalReason?: string;
  idempotencyKey: string;
  createdAt: string;
  updatedAt: string;
}

// Exact match with backend ValPay.Domain.TransactionStatus enum
export type TransactionStatus = 
  | 'Pending'
  | 'Authorised' 
  | 'Refused'
  | 'Captured'
  | 'Cancelled';

// Test Card Types
export interface TestCard {
  number: string;
  encryptedNumber: string;
  expiryMonth: string;
  expiryYear: string;
  cvc: string;
  holderName: string;
  brand: string;
  description: string;
  expectedResult: 'Authorised' | 'Refused' | 'Error';
}

// Cancel Payment Response
export interface CancelPaymentResponse {
  message: string;
  transactionId: string;
}

// UI Types
export interface PaymentFormData {
  cardNumber?: string;
  expiryMonth?: string;
  expiryYear?: string;
  cvc?: string;
  holderName?: string;
}

export interface PaymentState {
  sessionId?: string;
  transactionId?: string;
  selectedPaymentMethod?: AdyenPaymentMethod;
  paymentMethods: AdyenPaymentMethod[];
  isLoading: boolean;
  error?: string;
  currentStep: number;
}
