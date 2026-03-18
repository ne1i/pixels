import type { Color } from './pixel';

const STORAGE_KEY = 'pixel-favorite-colors';
const MAX_FAVORITES = 8;

export class FavoriteColors {
	private colors = $state<Color[]>([]);

	constructor() {
		if (typeof window !== 'undefined') {
			this.loadFromStorage();
		}
	}

	get list(): Color[] {
		return this.colors;
	}

	private loadFromStorage(): void {
		try {
			const stored = localStorage.getItem(STORAGE_KEY);
			if (stored) {
				const parsed = JSON.parse(stored);
				if (Array.isArray(parsed)) {
					this.colors = parsed.filter((c) => typeof c === 'string' && c.startsWith('#'));
				}
			}
		} catch (error) {
			console.error('Failed to load favorite colors:', error);
		}
	}

	private saveToStorage(): void {
		try {
			localStorage.setItem(STORAGE_KEY, JSON.stringify(this.colors));
		} catch (error) {
			console.error('Failed to save favorite colors:', error);
		}
	}

	add(color: Color): void {
		if (this.colors.includes(color)) {
			return;
		}
		
		if (this.colors.length >= MAX_FAVORITES) {
			this.colors.shift();
		}
		
		this.colors = [...this.colors, color];
		this.saveToStorage();
	}

	remove(color: Color): void {
		this.colors = this.colors.filter((c) => c !== color);
		this.saveToStorage();
	}

	has(color: Color): boolean {
		return this.colors.includes(color);
	}
}
