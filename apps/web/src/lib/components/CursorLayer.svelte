<script lang="ts">
	import { Layer } from 'svelte-canvas';

	type CursorLayerProps = {
		mouseGridPos: { x: number; y: number } | 'unset';
		offset: { x: number; y: number };
		scale: number;
		pixelSize: number;
		cursorSize: number;
	};

	const { mouseGridPos, offset, scale, pixelSize, cursorSize }: CursorLayerProps = $props();

	type GridCell = { x: number; y: number };
	const cursorOffsetsBySize: Partial<Record<number, GridCell[]>> = {};

	function clampCursorSize(value: number): number {
		if (!Number.isFinite(value)) return 1;
		return Math.min(8, Math.max(1, Math.round(value)));
	}

	function getCursorOffsets(size: number): GridCell[] {
		const clampedSize = clampCursorSize(size);
		const cachedOffsets = cursorOffsetsBySize[clampedSize];
		if (cachedOffsets !== undefined) {
			return cachedOffsets;
		}

		if (clampedSize === 1) {
			const offsets = [{ x: 0, y: 0 }];
			cursorOffsetsBySize[clampedSize] = offsets;
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

		cursorOffsetsBySize[clampedSize] = offsets;
		return offsets;
	}
</script>

{#if mouseGridPos !== 'unset'}
	<Layer
		render={function (opts) {
			const ctx = opts.context;
			const offsets = getCursorOffsets(cursorSize);
			const baseX = mouseGridPos.x * pixelSize;
			const baseY = mouseGridPos.y * pixelSize;
			ctx.save();
			ctx.translate(offset.x, offset.y);
			ctx.scale(scale, scale);

			ctx.strokeStyle = 'black';
			ctx.lineWidth = 3 / scale;
			for (const cellOffset of offsets) {
				ctx.strokeRect(
					baseX + cellOffset.x * pixelSize,
					baseY + cellOffset.y * pixelSize,
					pixelSize,
					pixelSize
				);
			}

			ctx.strokeStyle = 'white';
			ctx.lineWidth = 1.5 / scale;
			for (const cellOffset of offsets) {
				ctx.strokeRect(
					baseX + cellOffset.x * pixelSize,
					baseY + cellOffset.y * pixelSize,
					pixelSize,
					pixelSize
				);
			}

			ctx.restore();
		}}
	/>
{/if}
