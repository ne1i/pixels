// In dev: uses Vite proxy (relative URLs work)
// In prod: uses VITE_API_URL from build-time env var
export const API_URL = import.meta.env.VITE_API_URL ?? '';

export function apiUrl(path: string): string {
	return `${API_URL}${path}`;
}
