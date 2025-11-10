import { useEffect, useRef } from 'react';
import AdyenCheckout from '@adyen/adyen-web';
import '@adyen/adyen-web/dist/adyen.css';
import { createPayment, submitAdditionalDetails } from '@/services/paymentService';

type Props = {
  // Server-locked data from /paymentMethods (DO NOT accept shopper inputs here)
  paymentMethodsResponse: any;
  reference: string;
  amount: { value: number; currency: string };
  // Provide latest total at submit time (base + surcharge)
  getSubmitAmount?: () => { value: number; currency: string };
  countryCode: string;
  transactionId?: string;
  clientKey?: string;
  cardHolderName?: string;
  onRequireHolderName?: () => void;
  onFinalResult: (r: { resultCode?: string; pspReference?: string; txId?: string; provisional?: boolean; statusCheckUrl?: string; paymentMethodType?: string; paymentMethodBrand?: string; cardHolderName?: string }) => void;
  onError: (e: any) => void;
};

export default function AdyenDropin({
  paymentMethodsResponse,
  reference,
  amount,
  getSubmitAmount,
  countryCode,
  transactionId,
  clientKey,
  cardHolderName,
  onRequireHolderName,
  onFinalResult,
  onError
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const dropinRef = useRef<any>(null);
  const holderNameRef = useRef<string | undefined>(cardHolderName);
  const initialPropsRef = useRef({ paymentMethodsResponse, reference, amount, countryCode, transactionId });
  const selectedPaymentMethodTypeRef = useRef<string | undefined>(undefined);
  const selectedPaymentMethodBrandRef = useRef<string | undefined>(undefined);

  // Keep latest holder name without remounting Drop-in on each keystroke
  useEffect(() => {
    holderNameRef.current = cardHolderName;
  }, [cardHolderName]);

  useEffect(() => {
    let dropin: any;

    (async () => {
      try {
        const { paymentMethodsResponse: pm, reference: ref, amount: amt, countryCode: cc } = initialPropsRef.current;
        const checkout = await AdyenCheckout({
          environment: import.meta.env.VITE_ADYEN_ENVIRONMENT || 'test',
          clientKey: (typeof clientKey === 'string' && clientKey) ? clientKey : import.meta.env.VITE_ADYEN_CLIENT_KEY,
          paymentMethodsResponse: pm,
          // Provide amount so the Pay button shows the total
          amount: (getSubmitAmount?.() ?? amt),
          onChange: (state) => {
            try {
              const pmSel = (state as any)?.data?.paymentMethod;
              if (pmSel?.type) selectedPaymentMethodTypeRef.current = pmSel.type; // e.g., "scheme", "ideal"
              if (pmSel?.brand) selectedPaymentMethodBrandRef.current = pmSel.brand; // e.g., "visa", "mc"
            } catch {}
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
              let submitAmount = getSubmitAmount?.() ?? amt;
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
                  statusCheckUrl: (res as any).statusCheckUrl,
                  paymentMethodType: selectedPaymentMethodTypeRef.current,
                  paymentMethodBrand: selectedPaymentMethodBrandRef.current,
                  cardHolderName: holderNameRef.current
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
  }, [clientKey]);

  return <div ref={containerRef} id="dropin" />;
}
