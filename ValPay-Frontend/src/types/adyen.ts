export interface AdyenConfiguration {
  clientKey: string;
  environment: 'test' | 'live';
  locale?: string;
  showPayButton?: boolean;
}

// Adyen Web SDK payment data (encrypted by Adyen)
export interface AdyenPaymentData {
  paymentMethod: any; // Adyen handles encryption internally
  browserInfo?: any;
  origin?: string;
}

// Adyen Checkout configuration
export interface AdyenCheckoutConfig {
  clientKey: string;
  environment: 'test' | 'live';
  paymentMethodsResponse: any;
  onSubmit: (state: any, component: any) => void;
  onError: (error: any) => void;
  onAdditionalDetails?: (state: any, component: any) => void;
}

export interface AdyenPaymentMethod {
  type: string;
  name: string;
  brands?: string[];
  configuration?: {
    [key: string]: any;
  };
}

export interface AdyenPaymentRequest {
  amount: {
    currency: string;
    value: number;
  };
  reference: string;
  paymentMethod: {
    type: string;
    [key: string]: any;
  };
  returnUrl: string;
  merchantAccount: string;
  countryCode?: string;
}

export interface AdyenPaymentResponse {
  resultCode: string;
  pspReference?: string;
  action?: {
    type: string;
    [key: string]: any;
  };
  refusalReason?: string;
  refusalReasonCode?: string;
}

export interface AdyenSession {
  id: string;
  sessionData: string;
}
