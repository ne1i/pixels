<script lang="ts">
	import { Canvas } from 'svelte-canvas';
	import { onDestroy, onMount } from 'svelte';
	import { on } from 'svelte/events';
	import { SvelteMap } from 'svelte/reactivity';
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
	const ERASER_COLOR = '#FFFFFF' as Color;

	let connection: signalR.HubConnection | null = $state(null);
	let scale = $state(1);
	let offset = $state({ x: 0, y: 0 });
	let isDragging = $state(false);
	let hasDragged = $state(false);
	let dragStart = $state({ x: 0, y: 0 });
	let mouseGridPos = $state<{ x: number; y: number } | 'unset'>('unset');
	let rightButtonHeld = $state(false);
	let rightButtonStrokeActive = $state(false);
	let strokeSamples = $state<GridPoint[]>([]);
	let brushColor = $state<Color>(DEFAULT_BRUSH_COLOR);
	let canvasWidth = $state(0);
	let canvasHeight = $state(0);
	let pixelRatioValue: number | 'auto' | undefined = $state('auto');
	let viewportStateReady = $state(false);
	let viewportSaveTimeout: ReturnType<typeof setTimeout> | null = null;
	let pendingSegments: StrokeSegment[] = [];
	let pendingSegmentsFrame: number | null = null;
	let pendingIncrementalPlacements: PixelPlacement[] = [];
	let pendingIncrementalFrame: number | null = null;
	let renderVersion = $state(0);
	let incrementalPlacements = $state<PixelPlacement[]>([]);
	let incrementalVersion = $state(0);
	let gridData = $state<PixelGridData | null>(null);
	let removeWheelListener: (() => void) | null = null;
	let activeTouchPointers = new SvelteMap<number, { x: number; y: number }>();
	let touchMode: 'none' | 'draw' | 'navigate' = 'none';
	let touchDrawPointerId: number | null = null;
	let touchGestureStartDistance = 0;
	let touchGestureStartScale = 1;
	let touchGestureWorldAnchor = { x: 0, y: 0 };
	let isPaletteVisible = $state(true);
	let isPickingColor = $state(false);
	let isBucketToolActive = $state(false);
	let isEraserToolActive = $state(false);
	let brushSize = $state(1);
	let effectiveCursorSize = $derived(isPickingColor || isBucketToolActive ? 1 : brushSize);

	let pixelSize = $derived(INITIAL_PIXEL_SIZE);

	const brushOffsetsBySize: Partial<Record<number, GridCell[]>> = {};

	function clampBrushSize(value: number): number {
		if (!Number.isFinite(value)) return 1;
		return Math.min(8, Math.max(1, Math.round(value)));
	}

	function getActiveStrokeColor(): Color {
		return isEraserToolActive ? ERASER_COLOR : brushColor;
	}

	function getBrushOffsets(size: number): GridCell[] {
		const clampedSize = clampBrushSize(size);
		const cachedOffsets = brushOffsetsBySize[clampedSize];
		if (cachedOffsets !== undefined) {
			return cachedOffsets;
		}

		if (clampedSize === 1) {
			const offsets = [{ x: 0, y: 0 }];
			brushOffsetsBySize[clampedSize] = offsets;
			return offsets;
		}

		const radius = clampedSize - 1;
		const radiusSquared = radius * radius;
		const offsets: GridCell[] = [];

		for (let y = -radius; y <= radius; y += 1) {
			for (let x = -radius; x <= radius; x += 1) {
				if (x * x + y * y <= radiusSquared) {
					offsets.push({ x, y });
				}
			}
		}

		brushOffsetsBySize[clampedSize] = offsets;
		return offsets;
	}

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
		const newScale = clampZoom(
			touchGestureStartScale * (currentDistance / touchGestureStartDistance)
		);

		offset = {
			x: centerPoint.x - touchGestureWorldAnchor.x * newScale,
			y: centerPoint.y - touchGestureWorldAnchor.y * newScale
		};
		scale = newScale;
		scheduleViewportSave();
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

		// Handle bucket fill on touch
		if (isBucketToolActive) {
			applyBucketFill(cell);
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

	function flushIncrementalPlacements(): void {
		const nextPlacements = pendingIncrementalPlacements;
		pendingIncrementalPlacements = [];

		if (nextPlacements.length === 0) {
			return;
		}

		incrementalPlacements = nextPlacements;
		incrementalVersion += 1;
	}

	function scheduleIncrementalPlacements(placements: PixelPlacement[]): void {
		pendingIncrementalPlacements = [...pendingIncrementalPlacements, ...placements];

		if (typeof window === 'undefined') {
			flushIncrementalPlacements();
			return;
		}

		if (pendingIncrementalFrame !== null) {
			return;
		}

		pendingIncrementalFrame = window.requestAnimationFrame(() => {
			pendingIncrementalFrame = null;
			flushIncrementalPlacements();
		});
	}

	function setPixelColors(
		placements: PixelPlacement[],
		options?: { forceFullSync?: boolean }
	): void {
		if (gridData === null || placements.length === 0) return;

		gridData.setPixels(placements);
		scheduleIncrementalPlacements(placements);

		if (options?.forceFullSync === true) {
			renderVersion += 1;
		}
	}

	function createPlacementsFromCells(cells: GridCell[], rgb: RGB): PixelPlacement[] {
		if (gridData === null || cells.length === 0) return [];

		const uniqueCells: Record<string, true> = {};
		const placements: PixelPlacement[] = [];

		for (const cell of cells) {
			if (!isGridCellInBounds(cell)) {
				continue;
			}

			const key = `${cell.x},${cell.y}`;
			if (key in uniqueCells) {
				continue;
			}

			uniqueCells[key] = true;
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
			rgb: colorToRgb(getActiveStrokeColor()),
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
			rgb: colorToRgb(getActiveStrokeColor()),
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
			applyBrushAtCell(cell, cells);
			return cell;
		}

		if (previousCell.x !== cell.x || previousCell.y !== cell.y) {
			const interpolatedCells = drawInterpolatedLine(previousCell, cell);
			for (const interpolatedCell of interpolatedCells) {
				applyBrushAtCell(interpolatedCell, cells);
			}
		}

		return cell;
	}

	function applyBrushAtCell(centerCell: GridCell, cells: GridCell[]): void {
		for (const offsetCell of getBrushOffsets(brushSize)) {
			cells.push({ x: centerCell.x + offsetCell.x, y: centerCell.y + offsetCell.y });
		}
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
					x: inverseT * inverseT * start.x + 2 * inverseT * t * control.x + t * t * end.x,
					y: inverseT * inverseT * start.y + 2 * inverseT * t * control.y + t * t * end.y
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

	function setBrushColor(color: Color): void {
		brushColor = color;
		isEraserToolActive = false;
	}

	function toggleColorPicker(): void {
		isPickingColor = !isPickingColor;
		if (isPickingColor) {
			isBucketToolActive = false;
			isEraserToolActive = false;
		}
	}

	function toggleBucketTool(): void {
		isBucketToolActive = !isBucketToolActive;
		if (isBucketToolActive) {
			isPickingColor = false;
			isEraserToolActive = false;
		}
	}

	function toggleEraserTool(): void {
		isEraserToolActive = !isEraserToolActive;
		if (isEraserToolActive) {
			isPickingColor = false;
			isBucketToolActive = false;
		}
	}

	function setBrushSize(value: number): void {
		brushSize = clampBrushSize(value);
	}

	const BUCKET_FILL_LIMIT = 2500;
	const BUCKET_FILL_CONFIRM_THRESHOLD = 500;
	let pendingBucketFillPlacements = $state<PixelPlacement[] | null>(null);

	function computeFloodFill(startCell: GridCell, fillRgb: RGB): PixelPlacement[] {
		if (gridData === null || !isGridCellInBounds(startCell)) return [];

		const t0 = performance.now();

		const data = gridData.getData();
		const width = gridData.width;
		const height = gridData.height;

		const startIdx = (startCell.y * width + startCell.x) * 4;
		const targetR = data[startIdx];
		const targetG = data[startIdx + 1];
		const targetB = data[startIdx + 2];

		if (targetR === fillRgb.r && targetG === fillRgb.g && targetB === fillRgb.b) {
			console.log('[BUCKET] Target color same as fill color, skipping');
			return [];
		}

		const placements: PixelPlacement[] = [];
		const visited = new Uint8Array(width * height);
		const queue = new Int32Array(BUCKET_FILL_LIMIT * 2);
		let queueStart = 0;
		let queueEnd = 0;

		queue[queueEnd++] = startCell.x;
		queue[queueEnd++] = startCell.y;
		visited[startCell.y * width + startCell.x] = 1;

		while (queueStart < queueEnd && placements.length < BUCKET_FILL_LIMIT) {
			const x = queue[queueStart++];
			const y = queue[queueStart++];

			const idx = (y * width + x) * 4;
			if (data[idx] !== targetR || data[idx + 1] !== targetG || data[idx + 2] !== targetB) {
				continue;
			}

			placements.push({ x, y, rgb: fillRgb });

			// Up
			if (y > 0) {
				const nIdx = (y - 1) * width + x;
				if (visited[nIdx] === 0) {
					visited[nIdx] = 1;
					queue[queueEnd++] = x;
					queue[queueEnd++] = y - 1;
				}
			}
			// Down
			if (y < height - 1) {
				const nIdx = (y + 1) * width + x;
				if (visited[nIdx] === 0) {
					visited[nIdx] = 1;
					queue[queueEnd++] = x;
					queue[queueEnd++] = y + 1;
				}
			}
			// Left
			if (x > 0) {
				const nIdx = y * width + (x - 1);
				if (visited[nIdx] === 0) {
					visited[nIdx] = 1;
					queue[queueEnd++] = x - 1;
					queue[queueEnd++] = y;
				}
			}
			// Right
			if (x < width - 1) {
				const nIdx = y * width + (x + 1);
				if (visited[nIdx] === 0) {
					visited[nIdx] = 1;
					queue[queueEnd++] = x + 1;
					queue[queueEnd++] = y;
				}
			}
		}

		const t1 = performance.now();
		console.log(
			`[BUCKET] computeFloodFill: ${placements.length} pixels in ${(t1 - t0).toFixed(2)}ms`
		);

		return placements;
	}

	function applyBucketFill(cell: GridCell): void {
		const fillRgb = colorToRgb(brushColor);
		const placements = computeFloodFill(cell, fillRgb);
		isBucketToolActive = false;

		if (placements.length === 0) {
			return;
		}

		if (placements.length > BUCKET_FILL_CONFIRM_THRESHOLD) {
			pendingBucketFillPlacements = placements;
			return;
		}

		executeBucketFill(placements);
	}

	function executeBucketFill(placements: PixelPlacement[]): void {
		const t0 = performance.now();

		const t1 = performance.now();
		setPixelColors(placements);
		const t2 = performance.now();
		console.log(
			`[BUCKET] setPixelColors: ${placements.length} pixels in ${(t2 - t1).toFixed(2)}ms`
		);

		connection
			?.invoke('PlacePixels', placements)
			.then(() => {
				const t3 = performance.now();
				console.log(`[BUCKET] PlacePixels network: ${(t3 - t2).toFixed(2)}ms`);
				console.log(`[BUCKET] Total: ${(t3 - t0).toFixed(2)}ms`);
			})
			.catch((err) => console.error('[BUCKET] PlacePixels failed:', err));
	}

	function confirmBucketFill(): void {
		if (pendingBucketFillPlacements === null || pendingBucketFillPlacements.length === 0) {
			pendingBucketFillPlacements = null;
			return;
		}

		executeBucketFill(pendingBucketFillPlacements);
		pendingBucketFillPlacements = null;
	}

	function cancelBucketFill(): void {
		pendingBucketFillPlacements = null;
	}

	function stopEventPropagation(event: Event): void {
		event.stopPropagation();
	}

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
		incrementalPlacements = [];
		pendingIncrementalPlacements = [];
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
		scheduleViewportSave();
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

			if (point === null) {
				rightButtonStrokeActive = false;
				return;
			}

			const cell = toGridCell(point);

			if (isPickingColor) {
				pickColorAtCell(cell);
				rightButtonStrokeActive = false;
				return;
			}

			if (isBucketToolActive) {
				applyBucketFill(cell);
				rightButtonStrokeActive = false;
				return;
			}

			rightButtonStrokeActive = true;
			strokeSamples = [point];
			applyStrokeSegmentOptimistically(createLineSegment(point, point));
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
			scheduleViewportSave();
		}

		const point = getGridPointFromClient(e.clientX, e.clientY);

		if (point !== null) {
			const cell = toGridCell(point);
			mouseGridPos = cell;

			if (rightButtonHeld && rightButtonStrokeActive) {
				const lastPoint = strokeSamples.at(-1);
				if (lastPoint === undefined || lastPoint.x !== point.x || lastPoint.y !== point.y) {
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
			if (rightButtonStrokeActive) {
				finishStroke();
			}
			rightButtonStrokeActive = false;
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
		if (rightButtonStrokeActive) {
			finishStroke();
		}
		rightButtonStrokeActive = false;
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

		if (pendingIncrementalFrame !== null && typeof window !== 'undefined') {
			window.cancelAnimationFrame(pendingIncrementalFrame);
			pendingIncrementalFrame = null;
			flushIncrementalPlacements();
		}

		removeWheelListener?.();
		connection?.stop();
	});
</script>

<Canvas
	width={innerWidth.current}
	height={innerHeight.current}
	class="touch-none bg-black {isPickingColor || isBucketToolActive
		? 'cursor-crosshair'
		: isDragging
			? 'cursor-grabbing'
			: 'cursor-grab'}"
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
		<PixelLayer
			{gridData}
			{renderVersion}
			{offset}
			{scale}
			{pixelSize}
			{incrementalPlacements}
			{incrementalVersion}
		/>
	{/if}
	<CursorLayer {mouseGridPos} {offset} {scale} {pixelSize} cursorSize={effectiveCursorSize} />
</Canvas>

{#if pendingBucketFillPlacements !== null}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
		role="presentation"
		onpointerdown={stopEventPropagation}
		onpointermove={stopEventPropagation}
		onpointerup={stopEventPropagation}
		oncontextmenu={handleContextMenu}
	>
		<div
			class="w-full max-w-md rounded-lg border border-neutral-700 bg-neutral-900 p-5 text-neutral-100 shadow-2xl"
			role="dialog"
			aria-modal="true"
			tabindex="-1"
			onpointerdown={stopEventPropagation}
			onpointermove={stopEventPropagation}
			onpointerup={stopEventPropagation}
			oncontextmenu={handleContextMenu}
		>
			<h2 class="text-lg font-semibold">Confirm bucket fill</h2>
			<p class="mt-2 text-sm text-neutral-300">
				This fill will update <strong>{pendingBucketFillPlacements.length}</strong> pixels.
			</p>
			<div class="mt-5 flex justify-end gap-2">
				<button
					type="button"
					class="rounded-md border border-neutral-600 px-4 py-2 text-sm text-neutral-200 hover:bg-neutral-800"
					onclick={cancelBucketFill}
				>
					Cancel
				</button>
				<button
					type="button"
					class="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-500"
					onclick={confirmBucketFill}
				>
					Fill
				</button>
			</div>
		</div>
	</div>
{/if}

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
			oncolorchange={setBrushColor}
			onpickcolor={toggleColorPicker}
			{isPickingColor}
			onbuckettool={toggleBucketTool}
			{isBucketToolActive}
			onerasertool={toggleEraserTool}
			{isEraserToolActive}
			{brushSize}
			onbrushsizechange={setBrushSize}
		/>
	</div>
</div>
