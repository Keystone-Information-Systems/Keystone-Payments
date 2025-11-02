let inMemoryToken: string | null = null;

export function getAuthToken(): string | null {
  if (inMemoryToken) return inMemoryToken;
  try {
    const t = sessionStorage.getItem('valpay.jwt');
    if (t) inMemoryToken = t;
  } catch {}
  return inMemoryToken;
}

export function setAuthToken(token: string | null): void {
  inMemoryToken = token;
  try {
    if (token) sessionStorage.setItem('valpay.jwt', token);
    else sessionStorage.removeItem('valpay.jwt');
  } catch {}
}

export function getAuthHeaders(): Record<string, string> {
  const t = getAuthToken();
  return t ? { Authorization: `Bearer ${t}` } : {};
}


