import { z } from 'zod';
import { getAuthHeaders } from '@/stores/auth';

const API_BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '');

export const schemas = {
  paymentMethodsRes: z.object({
    paymentMethods: z.any(),
    sessionId: z.string(),
    reference: z.string(),
    transactionId: z.string().optional(),
    amount: z.object({ value: z.number().int().positive(), currency: z.string().length(3) }),
    countryCode: z.string().length(2),
    adyenClientKey: z.string().optional(),
    lineItems: z.array(
      z.object({
        accountNumber: z.string(),
        billNumber: z.string(),
        description: z.string(),
        amountValue: z.number().int().nonnegative()
      })
    ).optional().default([]),
    username: z.string().nullable().optional(),
    email: z.string().nullable().optional(),
    cardHolderName: z.string().nullable().optional(),
    surcharge: z.object({ amount: z.number().int().nonnegative(), percent: z.number().int().min(0).max(100).nullable().optional() }).optional(),
    legacyPostUrl: z.string().url()
  }),
  paymentsRes: z.object({
    resultCode: z.string().optional(),
    pspReference: z.string().optional(),
    txId: z.string().optional(),
    action: z.any().optional(),
    provisional: z.boolean().optional(),
    statusCheckUrl: z.union([z.string().url(), z.string().regex(/^\/.*/)]).optional(),
    cardSummary: z.string().optional()
  }),
  detailsRes: z.object({
    resultCode: z.string().optional(),
    pspReference: z.string().optional(),
    action: z.any().optional()
  }),
  cancelRes: z.object({
    message: z.string().optional(),
    transactionId: z.string().optional()
  }),
  costEstimateRes: z.object({
    surchargeAmount: z.number().int().nonnegative().default(0),
    totalWithSurcharge: z.number().int().positive(),
    breakdown: z.any().optional()
  })
};

export async function getPaymentMethods(payload: { orderId: string }) {
  const auth = getAuthHeaders();
  const res = await fetch(`${API_BASE}/getpaymentMethods`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...auth },
    body: JSON.stringify({ orderId: payload.orderId })
  });
  if (!res.ok) throw new Error(`paymentMethods ${res.status}`);
  const data = await res.json();
  return schemas.paymentMethodsRes.parse(data);
}

export async function createPayment(args: {
  reference: string;
  amount: { value: number; currency: string };
  countryCode: string;
  returnUrl: string;
  paymentMethod: any;
  cardHolderName?: string;
  billingAddress?: {
    street?: string;
    houseNumberOrName?: string;
    city?: string;
    stateOrProvince?: string;
    postalCode?: string;
    country?: string;
  };
  phoneNumber?: string;
  email?: string;
}) {
  const res = await fetch(`${API_BASE}/payments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      reference: args.reference,
      amountMinor: args.amount.value,
      currency: args.amount.currency,
      country: args.countryCode,
      returnUrl: args.returnUrl,
      paymentMethod: args.paymentMethod,
      cardHolderName: args.cardHolderName,
      billingAddress: args.billingAddress,
      phoneNumber: args.phoneNumber,
      email: args.email
    })
  });
  if (!res.ok) throw new Error(`payments ${res.status}`);
  return schemas.paymentsRes.parse(await res.json());
}

export async function submitAdditionalDetails(detailsPayload: any) {
  const res = await fetch(`${API_BASE}/payments/details`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify(detailsPayload)
  });
  if (!res.ok) throw new Error(`payments/details ${res.status}`);
  return schemas.detailsRes.parse(await res.json());
}

export async function getStatusByUrl(statusCheckUrl: string) {
  const url = /^https?:\/\//.test(statusCheckUrl)
    ? statusCheckUrl
    : new URL(statusCheckUrl, API_BASE).toString();
  const res = await fetch(url, {
    headers: {
      'Accept': 'application/json',
      'ngrok-skip-browser-warning': 'true'
    }
  });
  if (!res.ok) throw new Error(`statusCheck ${res.status}`);
  const contentType = res.headers.get('content-type') || '';
  if (!contentType.includes('application/json')) {
    const text = await res.text();
    throw new Error(`statusCheck ${res.status} non-JSON: ${text.slice(0, 120)}`);
  }
  return await res.json();
}

export async function getTransactionStatus(transactionId: string) {
  const res = await fetch(`${API_BASE}/transactions/${transactionId}`, {
    headers: {
      'Accept': 'application/json',
      'ngrok-skip-browser-warning': 'true',
      ...getAuthHeaders()
    }
  });
  if (!res.ok) throw new Error(`transactions ${res.status}`);
  const contentType = res.headers.get('content-type') || '';
  if (!contentType.includes('application/json')) {
    const text = await res.text();
    throw new Error(`transactions ${res.status} non-JSON: ${text.slice(0, 120)}`);
  }
  return await res.json();
}

export async function cancelPayment(transactionId: string) {
  const res = await fetch(`${API_BASE}/payments/${transactionId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() }
  });
  if (!res.ok) throw new Error(`cancel ${res.status}`);
  return schemas.cancelRes.parse(await res.json());
}

export async function estimatePaymentCost(args: {
  amount: { value: number; currency: string };
  encryptedCardNumber: string;
  reference: string;
  country: string;
  transactionId: string;
}) {
  const res = await fetch(`${API_BASE}/payments/cost-estimate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
    body: JSON.stringify({
      amount: args.amount,
      encryptedCardNumber: args.encryptedCardNumber,
      reference: args.reference,
      country: args.country,
      transactionId: args.transactionId
    })
  });
  if (!res.ok) throw new Error(`cost-estimate ${res.status}`);
  return schemas.costEstimateRes.parse(await res.json());
}
