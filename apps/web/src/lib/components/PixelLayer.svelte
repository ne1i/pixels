<script lang="ts">
	import { Layer } from 'svelte-canvas';
	import { onDestroy } from 'svelte';
	import type { PixelGridData } from '$lib/pixelGridData';

	type PixelLayerProps = {
		gridData: PixelGridData;
		renderVersion: number;
		offset: { x: number; y: number };
		scale: number;
		pixelSize: number;
	};

	const { gridData, renderVersion, offset, scale, pixelSize }: PixelLayerProps = $props();

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

	$effect(() => {
		renderVersion;
		ensureSurface();

		if (imageData === null || offscreenCtx === null) {
			return;
		}

		imageData.data.set(gridData.getData());
		offscreenCtx.putImageData(imageData, 0, 0);
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
