<script lang="ts">
	import { Layer } from 'svelte-canvas';
	import { onDestroy } from 'svelte';
	import type { PixelGridData } from '$lib/pixelGridData';
	import type { RGB } from '$lib/palette';

	type PixelPlacement = { x: number; y: number; rgb: RGB };

	type PixelLayerProps = {
		gridData: PixelGridData;
		renderVersion: number;
		incrementalPlacements: PixelPlacement[];
		incrementalVersion: number;
		offset: { x: number; y: number };
		scale: number;
		pixelSize: number;
	};

	const {
		gridData,
		renderVersion,
		incrementalPlacements,
		incrementalVersion,
		offset,
		scale,
		pixelSize
	}: PixelLayerProps = $props();

	const width = $derived(gridData.width);
	const height = $derived(gridData.height);
	let offscreenCanvas: HTMLCanvasElement | null = null;
	let offscreenCtx: CanvasRenderingContext2D | null = null;
	let imageData: ImageData | null = null;

	function ensureSurface(): void {
		if (offscreenCanvas === null) {
			offscreenCanvas = document.createElement('canvas');
		}

		if (offscreenCanvas.width !== width || offscreenCanvas.height !== height) {
			offscreenCanvas.width = width;
			offscreenCanvas.height = height;
			offscreenCtx = offscreenCanvas.getContext('2d');
			imageData = null;
		}

		if (offscreenCtx === null) {
			offscreenCtx = offscreenCanvas.getContext('2d');
		}

		if (imageData === null) {
			imageData = new ImageData(width, height);
		}
	}

	function applyIncrementalPlacements(placements: PixelPlacement[]): void {
		if (placements.length === 0) {
			return;
		}

		ensureSurface();

		if (imageData === null || offscreenCtx === null) {
			return;
		}

		const data = imageData.data;
		let dirtyMinX = width;
		let dirtyMinY = height;
		let dirtyMaxX = -1;
		let dirtyMaxY = -1;

		for (const placement of placements) {
			if (placement.x < 0 || placement.x >= width || placement.y < 0 || placement.y >= height) {
				continue;
			}

			const idx = (placement.y * width + placement.x) * 4;
			data[idx] = placement.rgb.r;
			data[idx + 1] = placement.rgb.g;
			data[idx + 2] = placement.rgb.b;
			data[idx + 3] = 255;

			if (placement.x < dirtyMinX) dirtyMinX = placement.x;
			if (placement.y < dirtyMinY) dirtyMinY = placement.y;
			if (placement.x > dirtyMaxX) dirtyMaxX = placement.x;
			if (placement.y > dirtyMaxY) dirtyMaxY = placement.y;
		}

		if (dirtyMaxX < dirtyMinX || dirtyMaxY < dirtyMinY) {
			return;
		}

		offscreenCtx.putImageData(
			imageData,
			0,
			0,
			dirtyMinX,
			dirtyMinY,
			dirtyMaxX - dirtyMinX + 1,
			dirtyMaxY - dirtyMinY + 1
		);
	}

	$effect(() => {
		renderVersion;
		ensureSurface();

		if (imageData === null || offscreenCtx === null) {
			return;
		}

		imageData.data.set(gridData.getData());
		offscreenCtx.putImageData(imageData, 0, 0);
	});

	$effect(() => {
		incrementalVersion;
		applyIncrementalPlacements(incrementalPlacements);
	});

	onDestroy(() => {
		offscreenCanvas = null;
		offscreenCtx = null;
		imageData = null;
	});
</script>

<Layer
	render={function (opts) {
		const ctx = opts.context;
		ensureSurface();

		if (offscreenCanvas === null) {
			return;
		}

		ctx.save();
		ctx.translate(offset.x, offset.y);
		ctx.scale(scale, scale);

		ctx.imageSmoothingEnabled = false;
		ctx.drawImage(offscreenCanvas, 0, 0, width * pixelSize, height * pixelSize);

		ctx.restore();
	}}
/>
