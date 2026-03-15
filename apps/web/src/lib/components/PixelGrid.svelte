<script lang="ts">
	import { Canvas, Layer, type Render } from 'svelte-canvas';
	import { innerWidth, innerHeight } from 'svelte/reactivity/window';
	import { randomColor } from '$lib/color';
	import { Pixel } from '$lib/pixel';
	import type { EventHandler } from 'svelte/elements';
	type PixelGridProps = {
		width: number;
		height: number;
	};

	const { width, height }: PixelGridProps = $props();

	let pixelRatioValue: number | 'auto' | undefined = $state('auto');

	function buildPixels(width: number, height: number) {
		const pixels = Array(width * height);
		const stride = width;
		for (let y = 0; y < height; y += 1) {
			for (let x = 0; x < width; x += 1) {
				const pixel = new Pixel(x, y, randomColor());
				pixels[y * stride + x] = pixel;
			}
		}
		return pixels;
	}

	const pixels = $derived(buildPixels(width, height));

	let scale = $state(1);
	let offset = $state({ x: 0, y: 0 });
	let isDragging = $state(false);
	let dragStart = $state({ x: 0, y: 0 });
	const MIN_ZOOM = 0.1;
	const MAX_ZOOM = 10;

	function handleWheel(e: WheelEvent) {
		e.preventDefault();
		const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;

		const newScale = Math.min(Math.max(scale * zoomFactor, MIN_ZOOM), MAX_ZOOM);

		const mouseX = e.clientX;
		const mouseY = e.clientY;

		offset = {
			x: mouseX - (mouseX - offset.x) * (newScale / scale),
			y: mouseY - (mouseY - offset.y) * (newScale / scale)
		};

		scale = newScale;
	}

	function handleMouseDown(event: Event) {
		const e = event as MouseEvent;
		if (e.button === 0) {
			isDragging = true;
			dragStart = { x: e.clientX - offset.x, y: e.clientY - offset.y };
		}
	}

	function handleMouseMove(event: Event) {
		const e = event as MouseEvent;

		if (isDragging) {
			offset = { x: e.clientX - dragStart.x, y: e.clientY - dragStart.y };
		}
	}

	function handleMouseUp() {
		isDragging = false;
	}
	const initialPixelSize = 20;
</script>

<svelte:window onwheel={handleWheel} />

<Canvas
	width={innerWidth.current}
	height={innerHeight.current}
	class="bg-black {isDragging ? 'cursor-grabbing' : 'cursor-grab'}"
	pixelRatio={pixelRatioValue}
	onresize={(e) => (pixelRatioValue = e.pixelRatio)}
	onmousedown={handleMouseDown}
	onmousemove={handleMouseMove}
	onmouseup={handleMouseUp}
	onmouseleave={handleMouseUp}
>
	<Layer
		render={function (opts) {
			const ctx = opts.context;
			ctx.save();
			ctx.translate(offset.x, offset.y);
			ctx.scale(scale, scale);

			for (let y = 0; y < height; y += 1) {
				for (let x = 0; x < width; x += 1) {
					const pixel = pixels[y * width + x];
					ctx.fillStyle = pixel.color;
					ctx.fillRect(
						pixel.x * initialPixelSize,
						pixel.y * initialPixelSize,
						initialPixelSize,
						initialPixelSize
					);
				}
			}

			ctx.restore();
		}}
	/>
	<!-- {#each { length: width } as _, x}
		{#each { length: height } as _, y}
			<Pixel width={pixelSize} height={pixelSize} x={x * pixelSize} y={y * pixelSize} color={randomColor()} />
		{/each}
	{/each} -->
</Canvas>
