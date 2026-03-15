<script lang="ts">
	import { Canvas } from 'svelte-canvas';
	import { innerWidth, innerHeight } from 'svelte/reactivity/window';
	import { randomColor } from '$lib/color';
	import { PixelGridData } from '$lib/pixelGridData';
	import { DEFAULT_PALETTE } from '$lib/palette';
	import CursorLayer from './CursorLayer.svelte';
	import PixelLayer from './PixelLayer.svelte';

	type PixelGridProps = {
		width: number;
		height: number;
	};

	const { width, height }: PixelGridProps = $props();

	const MAX_ZOOM = 2.5;
	const MIN_ZOOM = 0.1;
	const INITIAL_PIXEL_SIZE = 20;

	let scale = $state(1);
	let offset = $state({ x: 0, y: 0 });
	let isDragging = $state(false);
	let dragStart = $state({ x: 0, y: 0 });
	let mouseGridPos = $state<{ x: number; y: number } | 'unset'>('unset');
	let pixelRatioValue: number | 'auto' | undefined = $state('auto');

	let pixelSize = $derived(INITIAL_PIXEL_SIZE * scale);

	function createPixelGridData(width: number, height: number): PixelGridData {
		const grid = new PixelGridData(width, height);
		const paletteKeys = Object.keys(DEFAULT_PALETTE).map(Number);
		for (let i = 0; i < width * height; i++) {
			const colorId = paletteKeys[Math.floor(Math.random() * paletteKeys.length)];
			const color = DEFAULT_PALETTE[colorId];
			const x = i % width;
			const y = Math.floor(i / width);
			grid.setPixel(x, y, color.r, color.g, color.b);
		}
		return grid;
	}

	let gridData = $state(createPixelGridData(width, height));

	function setPixelColor(x: number, y: number, r: number, g: number, b: number): void {
		gridData.setPixel(x, y, r, g, b);
		gridData = gridData;
	}

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

		const canvasX = (e.clientX - offset.x) / scale;
		const canvasY = (e.clientY - offset.y) / scale;
		const gridX = Math.floor(canvasX / pixelSize);
		const gridY = Math.floor(canvasY / pixelSize);

		if (gridX >= 0 && gridX < width && gridY >= 0 && gridY < height) {
			mouseGridPos = { x: gridX, y: gridY };
		} else {
			mouseGridPos = 'unset';
		}
	}

	function handleMouseUp() {
		isDragging = false;
	}

	function handleMouseLeave() {
		isDragging = false;
		mouseGridPos = 'unset';
	}
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
	onmouseleave={handleMouseLeave}
>
	<PixelLayer {gridData} {offset} {scale} {pixelSize} />
	<CursorLayer {mouseGridPos} {offset} {scale} {pixelSize} />
</Canvas>
