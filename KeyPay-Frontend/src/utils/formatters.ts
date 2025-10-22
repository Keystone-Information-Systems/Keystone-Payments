import { CURRENCIES } from './constants';

export const formatCurrency = (
  amountMinor: number,
  currency: string = CURRENCIES.USD
): string => {
  const amount = amountMinor / 100;
  
  const formatter = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
  
  return formatter.format(amount);
};

export const formatAmount = (amountMinor: number): number => {
  return amountMinor / 100;
};

export const parseAmount = (amount: number): number => {
  return Math.round(amount * 100);
};

export const formatDate = (dateString: string): string => {
  const date = new Date(dateString);
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
};

export const formatReference = (reference: string): string => {
  return reference.toUpperCase().replace(/[^A-Z0-9]/g, '');
};

export const generateReference = (): string => {
  const timestamp = Date.now().toString(36);
  const random = Math.random().toString(36).substring(2, 8);
  return `VALPAY_${timestamp}_${random}`.toUpperCase();
};

export const truncateText = (text: string, maxLength: number): string => {
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};

export const formatCardNumber = (cardNumber: string): string => {
  const cleaned = cardNumber.replace(/\s/g, '');
  const groups = cleaned.match(/.{1,4}/g) || [];
  return groups.join(' ');
};

export const maskCardNumber = (cardNumber: string): string => {
  const cleaned = cardNumber.replace(/\s/g, '');
  if (cleaned.length < 8) return cardNumber;
  
  const firstFour = cleaned.substring(0, 4);
  const lastFour = cleaned.substring(cleaned.length - 4);
  const middle = '*'.repeat(cleaned.length - 8);
  
  return `${firstFour} ${middle} ${lastFour}`;
};
