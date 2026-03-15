export type Color = `#${string}`;

export class Pixel {
	constructor(
		public x: number,
		public y: number,
		public color: Color
	) {}
}
