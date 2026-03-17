<script lang="ts">
	import { Canvas } from 'svelte-canvas';
	import { onMount } from 'svelte';
	import { innerWidth, innerHeight } from 'svelte/reactivity/window';
	import { PixelGridData } from '$lib/pixelGridData';
	import type { Color } from '$lib/pixel';
	import CursorLayer from './CursorLayer.svelte';
	import PixelLayer from './PixelLayer.svelte';
	import ColorPalette from './ColorPalette.svelte';
	import * as signalR from '@microsoft/signalr';
	import { apiUrl } from '$lib/api';
	import { DEFAULT_PALETTE } from '$lib/palette';

	const MAX_ZOOM = 2.5;
	const DEFAULT_BRUSH_COLOR: Color = (() => {
		const rgb = DEFAULT_PALETTE[0];
		const hex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
		return `#${hex(rgb.r)}${hex(rgb.g)}${hex(rgb.b)}` as Color;
	})();
	const MIN_ZOOM = 0.1;
	const INITIAL_PIXEL_SIZE = 20;

	let connection: signalR.HubConnection | null = $state(null);

	onMount(async () => {
		const conn = new signalR.HubConnectionBuilder()
			.withUrl(apiUrl('/hubs/canvas'))
			.withAutomaticReconnect()
			.build();

		conn.on(
			'PixelPlaced',
			({ x, y, r, g, b }: { x: number; y: number; r: number; g: number; b: number }) => {
				setPixelColor(x, y, r, g, b);
			}
		);

		await conn.start();
		connection = conn;
	});
	let scale = $state(1);
	let offset = $state({ x: 0, y: 0 });
	let isDragging = $state(false);
	let hasDragged = $state(false);
	let dragStart = $state({ x: 0, y: 0 });
	let mouseGridPos = $state<{ x: number; y: number } | 'unset'>('unset');
	let rightButtonHeld = $state(false);
	let lastPaintedRight = $state<{ x: number; y: number } | null>(null);
	let brushColor = $state<Color>(DEFAULT_BRUSH_COLOR);
	let canvasWidth = $state(0);
	let canvasHeight = $state(0);
	let pixelRatioValue: number | 'auto' | undefined = $state('auto');

	let pixelSize = $derived(INITIAL_PIXEL_SIZE * scale);

	let gridData = $state<PixelGridData | null>(null);

	onMount(async () => {
		const [configRes, snapshotRes] = await Promise.all([
			fetch(apiUrl('/api/canvas/config')),
			fetch(apiUrl('/api/canvas/snapshot'))
		]);
		const { width, height } = await configRes.json();
		const buffer = await snapshotRes.arrayBuffer();
		canvasWidth = width;
		canvasHeight = height;
		const grid = new PixelGridData(width, height);
		grid.loadBuffer(new Uint8Array(buffer));
		gridData = grid;
	});

	function setPixelColor(x: number, y: number, r: number, g: number, b: number): void {
		if (gridData === null) return;
		const next = gridData.clone();
		next.setPixel(x, y, r, g, b);
		gridData = next;
	}

	function colorToRgb(color: Color): { r: number; g: number; b: number } {
		const hex = color.slice(1);
		return {
			r: parseInt(hex.slice(0, 2), 16),
			g: parseInt(hex.slice(2, 4), 16),
			b: parseInt(hex.slice(4, 6), 16)
		};
	}

	function applyBrushToPixel(x: number, y: number): void {
		if (gridData === null) return;
		const { r, g, b } = colorToRgb(brushColor);
		setPixelColor(x, y, r, g, b);
		connection
			?.invoke('PlacePixel', { x, y, r, g, b })
			.catch((err) => console.error('PlacePixel failed:', err));
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
			hasDragged = false;
			dragStart = { x: e.clientX - offset.x, y: e.clientY - offset.y };
		}
		if (e.button === 2) {
			e.preventDefault();
			rightButtonHeld = true;
			if (mouseGridPos !== 'unset') {
				applyBrushToPixel(mouseGridPos.x, mouseGridPos.y);
				lastPaintedRight = { x: mouseGridPos.x, y: mouseGridPos.y };
			}
		}
	}

	function handleContextMenu(event: Event) {
		event.preventDefault();
	}

	function handleMouseMove(event: Event) {
		const e = event as MouseEvent;

		if (isDragging) {
			hasDragged = true;
			offset = { x: e.clientX - dragStart.x, y: e.clientY - dragStart.y };
		}

		const canvasX = (e.clientX - offset.x) / scale;
		const canvasY = (e.clientY - offset.y) / scale;
		const gridX = Math.floor(canvasX / pixelSize);
		const gridY = Math.floor(canvasY / pixelSize);

		if (gridX >= 0 && gridX < canvasWidth && gridY >= 0 && gridY < canvasHeight) {
			mouseGridPos = { x: gridX, y: gridY };
			if (
				rightButtonHeld &&
				(lastPaintedRight === null ||
					lastPaintedRight.x !== gridX ||
					lastPaintedRight.y !== gridY)
			) {
				applyBrushToPixel(gridX, gridY);
				lastPaintedRight = { x: gridX, y: gridY };
			}
		} else {
			mouseGridPos = 'unset';
		}
	}

	function handleMouseUp(event: Event) {
		const e = event as MouseEvent;
		if (e.button === 2) {
			rightButtonHeld = false;
			lastPaintedRight = null;
		}
		if (e.button === 0) {
			isDragging = false;
			hasDragged = false;
		}
	}

	function handleMouseLeave() {
		isDragging = false;
		hasDragged = false;
		rightButtonHeld = false;
		lastPaintedRight = null;
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
	oncontextmenu={handleContextMenu}
>
	{#if gridData !== null}
		<PixelLayer {gridData} {offset} {scale} {pixelSize} />
	{/if}
	<CursorLayer {mouseGridPos} {offset} {scale} {pixelSize} />
</Canvas>

<div class="pointer-events-none fixed inset-0 flex items-end justify-center pb-4">
	<ColorPalette selectedColor={brushColor} oncolorchange={(c) => (brushColor = c)} />
</div>
