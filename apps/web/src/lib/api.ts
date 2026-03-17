// In dev: uses direct URL (default localhost:5080).
// In prod: uses VITE_API_URL from build-time env var.
export const API_URL =
	(import.meta.env.VITE_API_URL && import.meta.env.VITE_API_URL.trim() !== ''
		? import.meta.env.VITE_API_URL.trim()
		: 'http://localhost:5080') as string;

export function apiUrl(path: string): string {
	return `${API_URL}${path}`;
}
