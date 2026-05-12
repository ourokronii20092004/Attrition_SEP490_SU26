const isServer = typeof window === 'undefined';
const BASE_URL = isServer 
  ? (process.env.API_URL || 'http://localhost:5000') // Internal Docker/Localhost URL for SSR
  : (process.env.NEXT_PUBLIC_API_URL || ''); // Client-side URL

async function request(method: string, path: string, body?: any) {
  const token = typeof window !== 'undefined' ? localStorage.getItem('attrition-token') : null;

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    cache: 'no-store', // Disable Next.js build-time caching
    body: body ? JSON.stringify(body) : undefined,
  });

  // Handle 401 — try refresh
  if (res.status === 401 && typeof window !== 'undefined') {
    const refreshToken = localStorage.getItem('attrition-refresh');
    if (refreshToken) {
      const refreshRes = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (refreshRes.ok) {
        const data = await refreshRes.json();
        localStorage.setItem('attrition-token', data.data.accessToken);
        localStorage.setItem('attrition-refresh', data.data.refreshToken);
        // Retry original request
        headers['Authorization'] = `Bearer ${data.data.accessToken}`;
        const retry = await fetch(`${BASE_URL}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined });
        return retry.json();
      }
    }
  }

  return res.json();
}

async function uploadFile(path: string, file: File, fieldName = 'file') {
  const token = typeof window !== 'undefined' ? localStorage.getItem('attrition-token') : null;
  const formData = new FormData();
  formData.append(fieldName, file);

  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${BASE_URL}${path}`, { method: 'POST', headers, body: formData });
  return res.json();
}

export const api = {
  get: (path: string) => request('GET', path),
  post: (path: string, body?: any) => request('POST', path, body),
  put: (path: string, body?: any) => request('PUT', path, body),
  delete: (path: string) => request('DELETE', path),
  upload: uploadFile,
};