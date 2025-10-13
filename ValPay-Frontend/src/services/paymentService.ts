import { z } from 'zod';

const API = import.meta.env.VITE_API_BASE;

export const schemas = {
  paymentMethodsRes: z.object({
    paymentMethods: z.any(),
    sessionId: z.string(),
    reference: z.string(),
    amount: z.object({ value: z.number().int().positive(), currency: z.string().length(3) }),
    countryCode: z.string().length(2),
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
    cardHolderName: z.string().nullable().optional()
  }),
  paymentsRes: z.object({
    resultCode: z.string().optional(),
    pspReference: z.string().optional(),
    txId: z.string().optional(),
    action: z.any().optional(),
    provisional: z.boolean().optional(),
    statusCheckUrl: z.union([z.string().url(), z.string().regex(/^\/.*/)]).optional()
  }),
  detailsRes: z.object({
    resultCode: z.string().optional(),
    pspReference: z.string().optional(),
    action: z.any().optional()
  }),
  cancelRes: z.object({
    message: z.string().optional(),
    transactionId: z.string().optional()
  })
};

export async function getPaymentMethods(payload: { orderId: string }) {
  const res = await fetch(`${API}/getpaymentMethods`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
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
}) {
  const res = await fetch(`${API}/payments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      reference: args.reference,
      amountMinor: args.amount.value,
      currency: args.amount.currency,
      country: args.countryCode,
      returnUrl: args.returnUrl,
      paymentMethod: args.paymentMethod,
      cardHolderName: args.cardHolderName
    })
  });
  if (!res.ok) throw new Error(`payments ${res.status}`);
  return schemas.paymentsRes.parse(await res.json());
}

export async function submitAdditionalDetails(detailsPayload: any) {
  const res = await fetch(`${API}/payments/details`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(detailsPayload)
  });
  if (!res.ok) throw new Error(`payments/details ${res.status}`);
  return schemas.detailsRes.parse(await res.json());
}

export async function getStatusByUrl(statusCheckUrl: string) {
  const url = /^https?:\/\//.test(statusCheckUrl)
    ? statusCheckUrl
    : new URL(statusCheckUrl, API).toString();
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
  const res = await fetch(`${API}/transactions/${transactionId}`, {
    headers: {
      'Accept': 'application/json',
      'ngrok-skip-browser-warning': 'true'
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
  const res = await fetch(`${API}/payments/${transactionId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' }
  });
  if (!res.ok) throw new Error(`cancel ${res.status}`);
  return schemas.cancelRes.parse(await res.json());
}
