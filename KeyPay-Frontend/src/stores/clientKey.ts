let inMemoryClientKey: string | null = null;

export function getClientKey(): string | null {
  if (inMemoryClientKey) return inMemoryClientKey;
  try {
    const k = sessionStorage.getItem('valpay.adyenClientKey');
    if (k) inMemoryClientKey = k;
  } catch {}
  return inMemoryClientKey;
}

export function setClientKey(key: string | null): void {
  inMemoryClientKey = key;
  try {
    if (key) sessionStorage.setItem('valpay.adyenClientKey', key);
    else sessionStorage.removeItem('valpay.adyenClientKey');
  } catch {}
}


