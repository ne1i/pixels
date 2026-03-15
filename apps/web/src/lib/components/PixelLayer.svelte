<script lang="ts">
	import { Layer } from 'svelte-canvas';
	import type { PixelGridData } from '$lib/pixelGridData';

	type PixelLayerProps = {
		gridData: PixelGridData;
		offset: { x: number; y: number };
		scale: number;
		pixelSize: number;
	};

	const { gridData, offset, scale, pixelSize }: PixelLayerProps = $props();

	const width = $derived(gridData.width);
	const height = $derived(gridData.height);

	const imageData = $derived(
		new ImageData(new Uint8ClampedArray(gridData.getData()), width, height)
	);
</script>

<Layer
	render={function (opts) {
		const ctx = opts.context;
		ctx.save();
		ctx.translate(offset.x, offset.y);
		ctx.scale(scale, scale);

		const canvas = document.createElement('canvas');
		canvas.width = width;
		canvas.height = height;
		const offscreenCtx = canvas.getContext('2d')!;
		offscreenCtx.putImageData(imageData, 0, 0);

		ctx.imageSmoothingEnabled = false;
		ctx.drawImage(canvas, 0, 0, width * pixelSize, height * pixelSize);

		ctx.restore();
	}}
/>
