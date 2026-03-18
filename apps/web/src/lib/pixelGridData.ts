import type { RGB } from './palette';

export class PixelGridData {
	private data: Uint8Array;
	readonly width: number;
	readonly height: number;

	constructor(width: number, height: number) {
		this.width = width;
		this.height = height;
		this.data = new Uint8Array(width * height * 4);
	}

	getData(): Uint8Array {
		return this.data;
	}

	setPixel(x: number, y: number, rgb: RGB): void {
		if (x < 0 || x >= this.width || y < 0 || y >= this.height) return;
		const idx = (y * this.width + x) * 4;
		this.data[idx] = rgb.r;
		this.data[idx + 1] = rgb.g;
		this.data[idx + 2] = rgb.b;
		this.data[idx + 3] = 255;
	}

	setPixels(placements: Array<{ x: number; y: number; rgb: RGB }>): void {
		for (const placement of placements) {
			this.setPixel(placement.x, placement.y, placement.rgb);
		}
	}

	getPixel(x: number, y: number): RGB | null {
		if (x < 0 || x >= this.width || y < 0 || y >= this.height) return null;
		const idx = (y * this.width + x) * 4;
		return {
			r: this.data[idx],
			g: this.data[idx + 1],
			b: this.data[idx + 2]
		};
	}

	loadBuffer(buffer: Uint8Array): void {
		const pixels = this.width * this.height;
		if (buffer.length === pixels * 3) {
			for (let i = 0; i < pixels; i++) {
				this.data[i * 4] = buffer[i * 3];
				this.data[i * 4 + 1] = buffer[i * 3 + 1];
				this.data[i * 4 + 2] = buffer[i * 3 + 2];
				this.data[i * 4 + 3] = 255;
			}
		} else {
			this.data.set(buffer.subarray(0, this.data.length));
		}
	}

	clone(): PixelGridData {
		const copy = new PixelGridData(this.width, this.height);
		copy.data.set(this.data);
		return copy;
	}

	fill(rgb: RGB): void {
		for (let i = 0; i < this.width * this.height; i++) {
			const idx = i * 4;
			this.data[idx] = rgb.r;
			this.data[idx + 1] = rgb.g;
			this.data[idx + 2] = rgb.b;
			this.data[idx + 3] = 255;
		}
	}
}
