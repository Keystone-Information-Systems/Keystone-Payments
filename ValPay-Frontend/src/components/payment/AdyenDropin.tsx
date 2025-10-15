import { useEffect, useRef } from 'react';
import AdyenCheckout from '@adyen/adyen-web';
import '@adyen/adyen-web/dist/adyen.css';
import { createPayment, submitAdditionalDetails, estimatePaymentCost } from '@/services/paymentService';

type Props = {
  // Server-locked data from /paymentMethods (DO NOT accept shopper inputs here)
  paymentMethodsResponse: any;
  reference: string;
  amount: { value: number; currency: string };
  // Provide latest total at submit time (base + surcharge)
  getSubmitAmount?: () => { value: number; currency: string };
  countryCode: string;
  transactionId?: string;
  cardHolderName?: string;
  onRequireHolderName?: () => void;
  onFinalResult: (r: { resultCode?: string; pspReference?: string; txId?: string; provisional?: boolean; statusCheckUrl?: string }) => void;
  onError: (e: any) => void;
  onEncryptedCardNumber?: (enc: string) => void;
};

export default function AdyenDropin({
  paymentMethodsResponse,
  reference,
  amount,
  getSubmitAmount,
  countryCode,
  transactionId,
  cardHolderName,
  onRequireHolderName,
  onFinalResult,
  onError,
  onEncryptedCardNumber
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const dropinRef = useRef<any>(null);
  const holderNameRef = useRef<string | undefined>(cardHolderName);
  const lastEncRef = useRef<string | null>(null);
  const initialPropsRef = useRef({ paymentMethodsResponse, reference, amount, countryCode, transactionId });

  // Keep latest holder name without remounting Drop-in on each keystroke
  useEffect(() => {
    holderNameRef.current = cardHolderName;
  }, [cardHolderName]);

  useEffect(() => {
    let dropin: any;

    (async () => {
      try {
        const { paymentMethodsResponse: pm, reference: ref, amount: amt, countryCode: cc, transactionId: txId } = initialPropsRef.current;
        const checkout = await AdyenCheckout({
          environment: import.meta.env.VITE_ADYEN_ENVIRONMENT || 'test',
          clientKey: import.meta.env.VITE_ADYEN_CLIENT_KEY,
          paymentMethodsResponse: pm,
          // Omit amount to avoid label and dependency-driven remounts
          onChange: (state) => {
            const enc = (state as any)?.data?.paymentMethod?.encryptedCardNumber;
            if (enc && enc !== lastEncRef.current) {
              lastEncRef.current = enc;
              onEncryptedCardNumber?.(enc);
            }
          },
          onError: (error) => {
            onError(error);
          },
          onSubmit: async (state, dropinInstance) => {
            try {
              // Prevent submitting when card holder name is missing
              if (!holderNameRef.current || !holderNameRef.current.trim()) {
                onRequireHolderName?.();
                // Reset Drop-in status so the Pay button is re-enabled
                try { dropinInstance?.setStatus?.('ready'); } catch {}
                return;
              }
              const encryptedCardNumber = (state as any)?.data?.paymentMethod?.encryptedCardNumber;
              let submitAmount = getSubmitAmount?.() ?? amt;
              try {
                if (encryptedCardNumber) {
                  const est = await estimatePaymentCost({
                    amount: amt,
                    encryptedCardNumber,
                    reference: ref,
                    shopperCountry: cc,
                    transactionId: txId as any
                  });
                  submitAmount = { value: est.totalWithSurcharge, currency: amt.currency };
                }
              } catch {}
              const res = await createPayment({
                reference: ref,
                amount: submitAmount,
                countryCode: cc,
                returnUrl: `${window.location.origin}/payment/result`,
                paymentMethod: state.data.paymentMethod,
                cardHolderName: holderNameRef.current
              });

              if (res.action) {
                dropinInstance?.handleAction(res.action); // 3DS/redirect
              } else {
                onFinalResult({
                  resultCode: res.resultCode,
                  pspReference: res.pspReference,
                  txId: (res as any).txId,
                  provisional: (res as any).provisional,
                  statusCheckUrl: (res as any).statusCheckUrl
                });
                // Also surface the amount used via a custom event on the window for the parent to read if needed
                try {
                  const ev = new CustomEvent('valpay:submitAmountUsed', { detail: { value: submitAmount.value, currency: submitAmount.currency } });
                  window.dispatchEvent(ev);
                } catch {}
              }
            } catch (e) {
              onError(e);
            }
          },
          onAdditionalDetails: async (state, dropinInstance) => {
            try {
              const res = await submitAdditionalDetails(state.data);
              if (res.action) {
                dropinInstance?.handleAction(res.action);
              } else {
                onFinalResult({ resultCode: res.resultCode, pspReference: res.pspReference, txId: (res as any).txId });
              }
            } catch (e) {
              onError(e);
            }
          }
        });

        dropin = checkout.create('dropin').mount(containerRef.current!);
        dropinRef.current = dropin;
      } catch (e) {
        onError(e);
      }
    })();

    return () => { try { dropin?.unmount?.(); } finally { dropinRef.current = null; } };
  }, []);

  return <div ref={containerRef} id="dropin" />;
}
