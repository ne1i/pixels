<script lang="ts">
	import { Canvas } from 'svelte-canvas';
	import { onDestroy, onMount } from 'svelte';
	import { on } from 'svelte/events';
	import { innerHeight, innerWidth } from 'svelte/reactivity/window';
	import { apiUrl } from '$lib/api';
	import type { Color } from '$lib/pixel';
	import { PixelGridData } from '$lib/pixelGridData';
	import { DEFAULT_PALETTE, type RGB } from '$lib/palette';
	import * as signalR from '@microsoft/signalr';
	import ColorPalette from './ColorPalette.svelte';
	import CursorLayer from './CursorLayer.svelte';
	import PixelLayer from './PixelLayer.svelte';

	const MAX_ZOOM = 2.5;
	const MIN_ZOOM = 1 / 30;
	const INITIAL_PIXEL_SIZE = 20;
	const VIEWPORT_STORAGE_KEY = 'pixel-viewport';
	const LOCAL_CLIENT_ID =
		typeof crypto !== 'undefined' && 'randomUUID' in crypto
			? crypto.randomUUID()
			: `client-${Math.random().toString(36).slice(2)}`;

	type GridCell = { x: number; y: number };
	type GridPoint = { x: number; y: number };
	type PixelPlacement = { x: number; y: number; rgb: RGB };
	type StrokeSegment =
		| { kind: 'line'; from: GridPoint; to: GridPoint; rgb: RGB; clientId: string }
		| {
				kind: 'quadratic';
				from: GridPoint;
				control: GridPoint;
				to: GridPoint;
				rgb: RGB;
				clientId: string;
		  };

	const DEFAULT_BRUSH_COLOR: Color = (() => {
		const rgb = DEFAULT_PALETTE[0];
		const hex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
		return `#${hex(rgb.r)}${hex(rgb.g)}${hex(rgb.b)}` as Color;
	})();

	let connection: signalR.HubConnection | null = $state(null);
	let scale = $state(1);
	let offset = $state({ x: 0, y: 0 });
	let isDragging = $state(false);
	let hasDragged = $state(false);
	let dragStart = $state({ x: 0, y: 0 });
	let mouseGridPos = $state<{ x: number; y: number } | 'unset'>('unset');
	let rightButtonHeld = $state(false);
	let strokeSamples = $state<GridPoint[]>([]);
	let brushColor = $state<Color>(DEFAULT_BRUSH_COLOR);
	let canvasWidth = $state(0);
	let canvasHeight = $state(0);
	let pixelRatioValue: number | 'auto' | undefined = $state('auto');
	let viewportStateReady = $state(false);
	let viewportSaveTimeout: ReturnType<typeof setTimeout> | null = null;
	let pendingSegments: StrokeSegment[] = [];
	let pendingSegmentsFrame: number | null = null;
	let renderVersion = $state(0);
	let gridData = $state<PixelGridData | null>(null);
	let removeWheelListener: (() => void) | null = null;
	let activeTouchPointers = new Map<number, { x: number; y: number }>();
	let touchMode: 'none' | 'draw' | 'navigate' = 'none';
	let touchDrawPointerId: number | null = null;
	let touchGestureStartDistance = 0;
	let touchGestureStartScale = 1;
	let touchGestureWorldAnchor = { x: 0, y: 0 };
	let isPaletteVisible = $state(true);
	let isPickingColor = $state(false);

	let pixelSize = $derived(INITIAL_PIXEL_SIZE);

	function clampZoom(value: number): number {
		return Math.min(Math.max(value, MIN_ZOOM), MAX_ZOOM);
	}

	function loadViewportState(): void {
		if (typeof window === 'undefined') return;

		try {
			const stored = localStorage.getItem(VIEWPORT_STORAGE_KEY);
			if (!stored) return;

			const parsed = JSON.parse(stored) as {
				scale?: number;
				offset?: { x?: number; y?: number };
			};

			if (typeof parsed.scale === 'number' && Number.isFinite(parsed.scale)) {
				scale = clampZoom(parsed.scale);
			}

			if (
				typeof parsed.offset?.x === 'number' &&
				Number.isFinite(parsed.offset.x) &&
				typeof parsed.offset?.y === 'number' &&
				Number.isFinite(parsed.offset.y)
			) {
				offset = { x: parsed.offset.x, y: parsed.offset.y };
			}
		} catch (error) {
			console.error('Failed to load viewport state:', error);
		}
	}

	function scheduleViewportSave(): void {
		if (!viewportStateReady || typeof window === 'undefined') return;

		if (viewportSaveTimeout !== null) {
			clearTimeout(viewportSaveTimeout);
		}

		viewportSaveTimeout = setTimeout(() => {
			try {
				localStorage.setItem(VIEWPORT_STORAGE_KEY, JSON.stringify({ scale, offset }));
			} catch (error) {
				console.error('Failed to save viewport state:', error);
			}
		}, 120);
	}

	function drawInterpolatedLine(start: GridCell, end: GridCell): GridCell[] {
		let x = start.x;
		let y = start.y;
		const deltaX = Math.abs(end.x - start.x);
		const deltaY = Math.abs(end.y - start.y);
		const stepX = start.x < end.x ? 1 : -1;
		const stepY = start.y < end.y ? 1 : -1;
		let error = deltaX - deltaY;
		const cells: GridCell[] = [{ x, y }];

		while (x !== end.x || y !== end.y) {
			const doubledError = error * 2;

			if (doubledError > -deltaY) {
				error -= deltaY;
				x += stepX;
			}

			if (doubledError < deltaX) {
				error += deltaX;
				y += stepY;
			}

			cells.push({ x, y });
		}

		return cells;
	}

	function toGridCell(point: GridPoint): GridCell {
		return { x: Math.floor(point.x), y: Math.floor(point.y) };
	}

	function isGridCellInBounds(cell: GridCell): boolean {
		return cell.x >= 0 && cell.x < canvasWidth && cell.y >= 0 && cell.y < canvasHeight;
	}

	function getGridPointFromClient(clientX: number, clientY: number): GridPoint | null {
		const canvasX = (clientX - offset.x) / scale;
		const canvasY = (clientY - offset.y) / scale;
		const point = { x: canvasX / pixelSize, y: canvasY / pixelSize };
		return isGridCellInBounds(toGridCell(point)) ? point : null;
	}

	function midpoint(a: GridPoint, b: GridPoint): GridPoint {
		return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
	}

	function distanceBetweenPoints(a: GridPoint, b: GridPoint): number {
		return Math.hypot(b.x - a.x, b.y - a.y);
	}

	function getActiveTouchPoints(): GridPoint[] {
		return [...activeTouchPointers.values()];
	}

	function beginTouchNavigation(): void {
		const [firstPoint, secondPoint] = getActiveTouchPoints();
		if (firstPoint === undefined || secondPoint === undefined) {
			return;
		}

		finishStroke();
		touchMode = 'navigate';
		touchDrawPointerId = null;
		mouseGridPos = 'unset';

		const centerPoint = midpoint(firstPoint, secondPoint);
		touchGestureStartDistance = Math.max(distanceBetweenPoints(firstPoint, secondPoint), 1);
		touchGestureStartScale = scale;
		touchGestureWorldAnchor = {
			x: (centerPoint.x - offset.x) / scale,
			y: (centerPoint.y - offset.y) / scale
		};
	}

	function updateTouchNavigation(): void {
		const [firstPoint, secondPoint] = getActiveTouchPoints();
		if (firstPoint === undefined || secondPoint === undefined) {
			return;
		}

		const centerPoint = midpoint(firstPoint, secondPoint);
		const currentDistance = Math.max(distanceBetweenPoints(firstPoint, secondPoint), 1);
		const newScale = clampZoom(touchGestureStartScale * (currentDistance / touchGestureStartDistance));

		offset = {
			x: centerPoint.x - touchGestureWorldAnchor.x * newScale,
			y: centerPoint.y - touchGestureWorldAnchor.y * newScale
		};
		scale = newScale;
	}

	function startTouchDrawing(pointerId: number, point: GridPoint | null): void {
		finishStroke();
		touchDrawPointerId = pointerId;

		if (point === null) {
			touchMode = 'draw';
			mouseGridPos = 'unset';
			return;
		}

		const cell = toGridCell(point);

		// Handle color picking on touch
		if (isPickingColor) {
			pickColorAtCell(cell);
			touchMode = 'none';
			return;
		}

		touchMode = 'draw';
		strokeSamples = [point];
		mouseGridPos = cell;
		applyStrokeSegmentOptimistically(createLineSegment(point, point));
	}

	function flushPendingSegments(): void {
		const queuedSegments = pendingSegments;
		pendingSegments = [];

		if (queuedSegments.length === 0) {
			return;
		}

		connection
			?.invoke('PlaceStrokeSegments', queuedSegments)
			.catch((err) => console.error('PlaceStrokeSegments failed:', err));
	}

	function queueStrokeSegment(segment: StrokeSegment): void {
		if (typeof window === 'undefined') return;

		pendingSegments = [...pendingSegments, segment];

		if (pendingSegmentsFrame !== null) {
			return;
		}

		pendingSegmentsFrame = window.requestAnimationFrame(() => {
			pendingSegmentsFrame = null;
			flushPendingSegments();
		});
	}

	function setPixelColors(placements: PixelPlacement[]): void {
		if (gridData === null || placements.length === 0) return;

		gridData.setPixels(placements);
		renderVersion += 1;
	}

	function createPlacementsFromCells(cells: GridCell[], rgb: RGB): PixelPlacement[] {
		if (gridData === null || cells.length === 0) return [];

		const uniqueCells = new Set<string>();
		const placements: PixelPlacement[] = [];

		for (const cell of cells) {
			if (!isGridCellInBounds(cell)) {
				continue;
			}

			const key = `${cell.x},${cell.y}`;
			if (uniqueCells.has(key)) {
				continue;
			}

			uniqueCells.add(key);
			const currentPixel = gridData.getPixel(cell.x, cell.y);
			if (
				currentPixel !== null &&
				currentPixel.r === rgb.r &&
				currentPixel.g === rgb.g &&
				currentPixel.b === rgb.b
			) {
				continue;
			}

			placements.push({ x: cell.x, y: cell.y, rgb });
		}

		return placements;
	}

	function applyColorToCells(cells: GridCell[], rgb: RGB): PixelPlacement[] {
		const placements = createPlacementsFromCells(cells, rgb);

		if (placements.length === 0) {
			return placements;
		}

		setPixelColors(placements);
		return placements;
	}

	function renderStrokeSegment(segment: StrokeSegment): GridCell[] {
		if (segment.kind === 'line') {
			return drawLineSegment(segment.from, segment.to);
		}

		return drawQuadraticSegment(segment.from, segment.control, segment.to);
	}

	function applyStrokeSegmentOptimistically(segment: StrokeSegment): void {
		const placements = applyColorToCells(renderStrokeSegment(segment), segment.rgb);

		if (placements.length === 0) {
			return;
		}

		queueStrokeSegment(segment);
	}

	function createLineSegment(from: GridPoint, to: GridPoint): StrokeSegment {
		return {
			kind: 'line',
			from,
			to,
			rgb: colorToRgb(brushColor),
			clientId: LOCAL_CLIENT_ID
		};
	}

	function createQuadraticStrokeSegment(
		from: GridPoint,
		control: GridPoint,
		to: GridPoint
	): StrokeSegment {
		return {
			kind: 'quadratic',
			from,
			control,
			to,
			rgb: colorToRgb(brushColor),
			clientId: LOCAL_CLIENT_ID
		};
	}

	function paintPointSample(
		point: GridPoint,
		previousCell: GridCell | null,
		cells: GridCell[]
	): GridCell | null {
		const cell = toGridCell(point);

		if (!isGridCellInBounds(cell)) {
			return previousCell;
		}

		if (previousCell === null) {
			cells.push(cell);
			return cell;
		}

		if (previousCell.x !== cell.x || previousCell.y !== cell.y) {
			cells.push(...drawInterpolatedLine(previousCell, cell));
		}

		return cell;
	}

	function drawLineSegment(start: GridPoint, end: GridPoint): GridCell[] {
		let previousCell: GridCell | null = null;
		const distance = Math.max(Math.abs(end.x - start.x), Math.abs(end.y - start.y));
		const steps = Math.max(1, Math.ceil(distance * 2));
		const cells: GridCell[] = [];

		for (let i = 0; i <= steps; i += 1) {
			const t = i / steps;
			previousCell = paintPointSample(
				{
					x: start.x + (end.x - start.x) * t,
					y: start.y + (end.y - start.y) * t
				},
				previousCell,
				cells
			);
		}

		return cells;
	}

	function drawQuadraticSegment(start: GridPoint, control: GridPoint, end: GridPoint): GridCell[] {
		let previousCell: GridCell | null = null;
		const curveLength =
			Math.hypot(control.x - start.x, control.y - start.y) +
			Math.hypot(end.x - control.x, end.y - control.y);
		const steps = Math.max(4, Math.ceil(curveLength * 3));
		const cells: GridCell[] = [];

		for (let i = 0; i <= steps; i += 1) {
			const t = i / steps;
			const inverseT = 1 - t;
			previousCell = paintPointSample(
				{
					x:
						inverseT * inverseT * start.x +
						2 * inverseT * t * control.x +
						t * t * end.x,
					y:
						inverseT * inverseT * start.y +
						2 * inverseT * t * control.y +
						t * t * end.y
				},
				previousCell,
				cells
			);
		}

		return cells;
	}

	function extendStroke(point: GridPoint): void {
		strokeSamples = [...strokeSamples, point];

		if (strokeSamples.length === 2) {
			applyStrokeSegmentOptimistically(createLineSegment(strokeSamples[0], strokeSamples[1]));
			return;
		}

		if (strokeSamples.length < 3) {
			return;
		}

		const [previousPoint, controlPoint, currentPoint] = strokeSamples.slice(-3);
		applyStrokeSegmentOptimistically(
			createQuadraticStrokeSegment(
				midpoint(previousPoint, controlPoint),
				controlPoint,
				midpoint(controlPoint, currentPoint)
			)
		);
		strokeSamples = [controlPoint, currentPoint];
	}

	function finishStroke(): void {
		if (strokeSamples.length === 2) {
			applyStrokeSegmentOptimistically(
				createLineSegment(midpoint(strokeSamples[0], strokeSamples[1]), strokeSamples[1])
			);
		}

		strokeSamples = [];
	}

	function colorToRgb(color: Color): RGB {
		const hex = color.slice(1);
		return {
			r: parseInt(hex.slice(0, 2), 16),
			g: parseInt(hex.slice(2, 4), 16),
			b: parseInt(hex.slice(4, 6), 16)
		};
	}

	function rgbToColor(rgb: RGB): Color {
		const hex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
		return `#${hex(rgb.r)}${hex(rgb.g)}${hex(rgb.b)}` as Color;
	}

	function pickColorAtCell(cell: GridCell): void {
		if (gridData === null || !isGridCellInBounds(cell)) return;
		
		const rgb = gridData.getPixel(cell.x, cell.y);
		if (rgb !== null) {
			brushColor = rgbToColor(rgb);
		}
		isPickingColor = false;
	}

	function toggleColorPicker(): void {
		isPickingColor = !isPickingColor;
	}

	$effect(() => {
		viewportStateReady;
		scale;
		offset.x;
		offset.y;
		scheduleViewportSave();
	});

	onMount(async () => {
		loadViewportState();
		viewportStateReady = true;
		removeWheelListener = on(window, 'wheel', handleWheel, { passive: false });

		const conn = new signalR.HubConnectionBuilder()
			.withUrl(apiUrl('/hubs/canvas'))
			.withAutomaticReconnect()
			.build();

		conn.on('PixelPlaced', ({ x, y, rgb }: { x: number; y: number; rgb: RGB }) => {
			setPixelColors([{ x, y, rgb }]);
		});

		conn.on('PixelsPlaced', (placements: PixelPlacement[]) => {
			setPixelColors(placements);
		});

		conn.on('StrokeSegmentsPlaced', (segments: StrokeSegment[]) => {
			for (const segment of segments) {
				if (segment.clientId === LOCAL_CLIENT_ID) {
					continue;
				}

				applyColorToCells(renderStrokeSegment(segment), segment.rgb);
			}
		});

		await conn.start();
		connection = conn;

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
		renderVersion += 1;
	});

	function handleWheel(event: WheelEvent) {
		event.preventDefault();

		const zoomFactor = event.deltaY > 0 ? 0.9 : 1.1;
		const newScale = clampZoom(scale * zoomFactor);

		if (newScale === scale) {
			return;
		}

		const mouseX = event.clientX;
		const mouseY = event.clientY;
		const worldX = (mouseX - offset.x) / scale;
		const worldY = (mouseY - offset.y) / scale;

		offset = {
			x: mouseX - worldX * newScale,
			y: mouseY - worldY * newScale
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
			const point = getGridPointFromClient(e.clientX, e.clientY);
			if (point !== null) {
				strokeSamples = [point];
				applyStrokeSegmentOptimistically(createLineSegment(point, point));
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

		const point = getGridPointFromClient(e.clientX, e.clientY);

		if (point !== null) {
			const cell = toGridCell(point);
			mouseGridPos = cell;

			if (rightButtonHeld) {
				const lastPoint = strokeSamples.at(-1);
				if (
					lastPoint === undefined ||
					lastPoint.x !== point.x ||
					lastPoint.y !== point.y
				) {
					extendStroke(point);
				}
			}
		} else {
			mouseGridPos = 'unset';
		}
	}

	function handleMouseUp(event: Event) {
		const e = event as MouseEvent;

		if (e.button === 2) {
			rightButtonHeld = false;
			finishStroke();
		}

		if (e.button === 0) {
			// Handle color picker on click (not drag)
			if (!hasDragged && isPickingColor) {
				const point = getGridPointFromClient(e.clientX, e.clientY);
				if (point !== null) {
					const cell = toGridCell(point);
					pickColorAtCell(cell);
				}
			}
			isDragging = false;
			hasDragged = false;
		}
	}

	function handleMouseLeave() {
		isDragging = false;
		hasDragged = false;
		rightButtonHeld = false;
		finishStroke();
		mouseGridPos = 'unset';
	}

	function handlePointerDown(event: Event) {
		const e = event as PointerEvent;
		if (e.pointerType !== 'touch') {
			return;
		}

		e.preventDefault();
		(e.currentTarget as Element | null)?.setPointerCapture?.(e.pointerId);
		activeTouchPointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

		if (activeTouchPointers.size >= 2) {
			beginTouchNavigation();
			return;
		}

		startTouchDrawing(e.pointerId, getGridPointFromClient(e.clientX, e.clientY));
	}

	function handlePointerMove(event: Event) {
		const e = event as PointerEvent;
		if (e.pointerType !== 'touch' || !activeTouchPointers.has(e.pointerId)) {
			return;
		}

		e.preventDefault();
		activeTouchPointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

		if (touchMode === 'navigate' && activeTouchPointers.size >= 2) {
			updateTouchNavigation();
			return;
		}

		if (touchMode !== 'draw' || touchDrawPointerId !== e.pointerId) {
			return;
		}

		const point = getGridPointFromClient(e.clientX, e.clientY);
		if (point === null) {
			mouseGridPos = 'unset';
			return;
		}

		const cell = toGridCell(point);
		mouseGridPos = cell;

		const lastPoint = strokeSamples.at(-1);
		if (lastPoint === undefined || lastPoint.x !== point.x || lastPoint.y !== point.y) {
			extendStroke(point);
		}
	}

	function endTouchPointer(pointerId: number): void {
		activeTouchPointers.delete(pointerId);

		if (touchMode === 'draw' && touchDrawPointerId === pointerId) {
			finishStroke();
			touchDrawPointerId = null;
			touchMode = 'none';
			mouseGridPos = 'unset';
			return;
		}

		if (touchMode === 'navigate') {
			if (activeTouchPointers.size >= 2) {
				beginTouchNavigation();
			} else {
				touchMode = 'none';
				mouseGridPos = 'unset';
			}
		}
	}

	function handlePointerUp(event: Event) {
		const e = event as PointerEvent;
		if (e.pointerType !== 'touch') {
			return;
		}

		e.preventDefault();
		(e.currentTarget as Element | null)?.releasePointerCapture?.(e.pointerId);
		endTouchPointer(e.pointerId);
	}

	function handlePointerCancel(event: Event) {
		const e = event as PointerEvent;
		if (e.pointerType !== 'touch') {
			return;
		}

		(e.currentTarget as Element | null)?.releasePointerCapture?.(e.pointerId);
		endTouchPointer(e.pointerId);
	}

	onDestroy(() => {
		if (viewportSaveTimeout !== null) {
			clearTimeout(viewportSaveTimeout);
		}

		if (pendingSegmentsFrame !== null && typeof window !== 'undefined') {
			window.cancelAnimationFrame(pendingSegmentsFrame);
			pendingSegmentsFrame = null;
			flushPendingSegments();
		}

		removeWheelListener?.();
		connection?.stop();
	});
</script>

<Canvas
	width={innerWidth.current}
	height={innerHeight.current}
	class="bg-black touch-none {isPickingColor ? 'cursor-crosshair' : isDragging ? 'cursor-grabbing' : 'cursor-grab'}"
	pixelRatio={pixelRatioValue}
	onresize={(e) => (pixelRatioValue = e.pixelRatio)}
	onmousedown={handleMouseDown}
	onmousemove={handleMouseMove}
	onmouseup={handleMouseUp}
	onmouseleave={handleMouseLeave}
	onpointerdown={handlePointerDown}
	onpointermove={handlePointerMove}
	onpointerup={handlePointerUp}
	onpointercancel={handlePointerCancel}
	oncontextmenu={handleContextMenu}
>
	{#if gridData !== null}
		<PixelLayer {gridData} {renderVersion} {offset} {scale} {pixelSize} />
	{/if}
	<CursorLayer {mouseGridPos} {offset} {scale} {pixelSize} />
</Canvas>

<div class="pointer-events-none fixed inset-x-0 bottom-0 flex justify-center px-3 pb-3 sm:pb-4">
	<div
		class="flex w-full max-w-[min(100%,740px)] flex-col items-center transition-transform duration-100 ease-out {isPaletteVisible
			? 'translate-y-0'
			: 'translate-y-[calc(100%-2.75rem)]'}"
	>
		<!-- Toggle Button -->
		<button
			type="button"
			class="pointer-events-auto mb-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-neutral-800 text-neutral-300 shadow-lg ring-1 ring-neutral-700 transition-colors hover:bg-neutral-700 hover:text-white"
			onclick={() => (isPaletteVisible = !isPaletteVisible)}
			aria-expanded={isPaletteVisible}
			aria-controls="color-palette-panel"
			title={isPaletteVisible ? 'Hide palette' : 'Show palette'}
		>
			{#if isPaletteVisible}
				<!-- Chevron down / collapse icon -->
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="20"
					height="20"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
					<path d="m6 9 6 6 6-6" />
				</svg>
			{:else}
				<!-- Paint palette icon -->
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="20"
					height="20"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
					<circle cx="13.5" cy="6.5" r=".5" fill="currentColor" />
					<circle cx="17.5" cy="10.5" r=".5" fill="currentColor" />
					<circle cx="8.5" cy="7.5" r=".5" fill="currentColor" />
					<circle cx="6.5" cy="12.5" r=".5" fill="currentColor" />
					<path
						d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.555C21.965 6.012 17.461 2 12 2z"
					/>
				</svg>
			{/if}
		</button>

		<!-- Color Palette -->
		<ColorPalette
			selectedColor={brushColor}
			oncolorchange={(color) => (brushColor = color)}
			onpickcolor={toggleColorPicker}
			{isPickingColor}
		/>
	</div>
</div>
