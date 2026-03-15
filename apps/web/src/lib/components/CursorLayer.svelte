<script lang="ts">
	import { Layer } from 'svelte-canvas';

	type CursorLayerProps = {
		mouseGridPos: { x: number; y: number } | 'unset';
		offset: { x: number; y: number };
		scale: number;
		pixelSize: number;
	};

	const { mouseGridPos, offset, scale, pixelSize }: CursorLayerProps = $props();
</script>

{#if mouseGridPos !== 'unset'}
	<Layer
		render={function (opts) {
			const ctx = opts.context;
			ctx.save();
			ctx.translate(offset.x, offset.y);
			ctx.scale(scale, scale);

			ctx.strokeStyle = 'white';
			ctx.lineWidth = 2 / scale;
			ctx.strokeRect(mouseGridPos.x * pixelSize, mouseGridPos.y * pixelSize, pixelSize, pixelSize);

			ctx.restore();
		}}
	/>
{/if}
