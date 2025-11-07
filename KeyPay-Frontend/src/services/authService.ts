const API_BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '');

export async function exchangeAuthCode(code: string): Promise<{ token: string; adyenClientKey?: string; expiresIn?: number }>
{
  const res = await fetch(`${API_BASE}/token/exchange`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ code })
  });
  if (!res.ok) {
    throw new Error(`token/exchange ${res.status}`);
  }
  return await res.json();
}


