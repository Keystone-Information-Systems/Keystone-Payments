import { useEffect, useRef } from 'react';
import AdyenCheckout from '@adyen/adyen-web';
import '@adyen/adyen-web/dist/adyen.css';
import { createPayment, submitAdditionalDetails } from '@/services/paymentService';

type Props = {
  // Server-locked data from /paymentMethods (DO NOT accept shopper inputs here)
  paymentMethodsResponse: any;
  reference: string;
  amount: { value: number; currency: string };
  countryCode: string;
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
  countryCode,
  cardHolderName,
  onRequireHolderName,
  onFinalResult,
  onError,
  onEncryptedCardNumber
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const holderNameRef = useRef<string | undefined>(cardHolderName);
  const lastEncRef = useRef<string | null>(null);

  // Keep latest holder name without remounting Drop-in on each keystroke
  useEffect(() => {
    holderNameRef.current = cardHolderName;
  }, [cardHolderName]);

  useEffect(() => {
    let dropin: any;

    (async () => {
      try {
        const checkout = await AdyenCheckout({
          environment: import.meta.env.VITE_ADYEN_ENVIRONMENT || 'test',
          clientKey: import.meta.env.VITE_ADYEN_CLIENT_KEY,
          paymentMethodsResponse,
          amount: { value: amount.value, currency: amount.currency },
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
              const res = await createPayment({
                reference,
                amount,
                countryCode,
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
      } catch (e) {
        onError(e);
      }
    })();

    return () => dropin?.unmount?.();
  }, [paymentMethodsResponse, reference, amount.value, amount.currency, countryCode]);

  return <div ref={containerRef} id="dropin" />;
}
