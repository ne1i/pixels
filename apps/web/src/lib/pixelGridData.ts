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

	setPixel(x: number, y: number, r: number, g: number, b: number): void {
		if (x < 0 || x >= this.width || y < 0 || y >= this.height) return;
		const idx = (y * this.width + x) * 4;
		this.data[idx] = r;
		this.data[idx + 1] = g;
		this.data[idx + 2] = b;
		this.data[idx + 3] = 255;
	}

	getPixel(x: number, y: number): { r: number; g: number; b: number } | null {
		if (x < 0 || x >= this.width || y < 0 || y >= this.height) return null;
		const idx = (y * this.width + x) * 4;
		return {
			r: this.data[idx],
			g: this.data[idx + 1],
			b: this.data[idx + 2]
		};
	}

	fill(r: number, g: number, b: number): void {
		for (let i = 0; i < this.width * this.height; i++) {
			const idx = i * 4;
			this.data[idx] = r;
			this.data[idx + 1] = g;
			this.data[idx + 2] = b;
			this.data[idx + 3] = 255;
		}
	}
}
