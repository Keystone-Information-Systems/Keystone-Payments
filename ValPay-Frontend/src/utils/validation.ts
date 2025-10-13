import { z } from 'zod';

export const paymentMethodsRequestSchema = z.object({
  amountMinor: z.number().positive('Amount must be positive'),
  currency: z.string().min(3, 'Currency must be at least 3 characters'),
  country: z.string().min(2, 'Country must be at least 2 characters'),
  merchantAccount: z.string().min(1, 'Merchant account is required'),
});

export const paymentRequestSchema = z.object({
  reference: z.string().min(1, 'Reference is required'),
  amountMinor: z.number().positive('Amount must be positive'),
  currency: z.string().min(3, 'Currency must be at least 3 characters'),
  returnUrl: z.string().url('Return URL must be valid'),
  paymentMethod: z.object({
    type: z.string().min(1, 'Payment method type is required'),
    name: z.string().min(1, 'Payment method name is required'),
  }),
  merchantAccount: z.string().min(1, 'Merchant account is required'),
  country: z.string().optional(),
});

export const cardDetailsSchema = z.object({
  cardNumber: z.string()
    .min(13, 'Card number must be at least 13 digits')
    .max(19, 'Card number must be at most 19 digits')
    .regex(/^\d+$/, 'Card number must contain only digits'),
  expiryMonth: z.string()
    .min(1, 'Expiry month is required')
    .max(2, 'Expiry month must be 1-2 digits')
    .regex(/^(0?[1-9]|1[0-2])$/, 'Expiry month must be 01-12'),
  expiryYear: z.string()
    .min(2, 'Expiry year is required')
    .max(4, 'Expiry year must be 2-4 digits')
    .regex(/^\d{2,4}$/, 'Expiry year must be digits'),
  cvc: z.string()
    .min(3, 'CVC must be at least 3 digits')
    .max(4, 'CVC must be at most 4 digits')
    .regex(/^\d+$/, 'CVC must contain only digits'),
  holderName: z.string()
    .min(1, 'Cardholder name is required')
    .max(50, 'Cardholder name must be at most 50 characters'),
});

export const sepaDetailsSchema = z.object({
  iban: z.string()
    .min(15, 'IBAN must be at least 15 characters')
    .max(34, 'IBAN must be at most 34 characters')
    .regex(/^[A-Z]{2}[0-9]{2}[A-Z0-9]+$/, 'Invalid IBAN format'),
  holderName: z.string()
    .min(1, 'Account holder name is required')
    .max(50, 'Account holder name must be at most 50 characters'),
});

export const idealDetailsSchema = z.object({
  issuer: z.string().min(1, 'Bank selection is required'),
});

export type PaymentMethodsRequestInput = z.infer<typeof paymentMethodsRequestSchema>;
export type PaymentRequestInput = z.infer<typeof paymentRequestSchema>;
export type CardDetailsInput = z.infer<typeof cardDetailsSchema>;
export type SepaDetailsInput = z.infer<typeof sepaDetailsSchema>;
export type IdealDetailsInput = z.infer<typeof idealDetailsSchema>;
