<script lang="ts">
	import type { Color } from '$lib/pixel';
	import { DEFAULT_PALETTE, type RGB } from '$lib/palette';

	type ColorPaletteProps = {
		selectedColor: Color;
		oncolorchange: (color: Color) => void;
	};

	const { selectedColor, oncolorchange }: ColorPaletteProps = $props();

	function rgbToColor(rgb: RGB): Color {
		const hex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
		return `#${hex(rgb.r)}${hex(rgb.g)}${hex(rgb.b)}`;
	}

	const paletteEntries = Object.values(DEFAULT_PALETTE).map(rgbToColor);
</script>

<div
	class="pointer-events-auto flex items-center gap-3 rounded-xl border border-neutral-700 bg-neutral-900/95 px-4 py-3 shadow-2xl backdrop-blur-sm"
	role="toolbar"
	aria-label="Color palette"
>
	<span class="text-xs font-medium text-neutral-400">Brush</span>
	<div
		class="h-8 w-8 shrink-0 rounded-lg ring-2 ring-neutral-500 ring-offset-2 ring-offset-neutral-900"
		style="background-color: {selectedColor}"
		title={selectedColor}
	></div>
	<div class="grid grid-cols-[repeat(32,1.5rem)] gap-1">
		{#each paletteEntries as paletteColor}
			<button
				type="button"
				class="h-6 w-6 rounded transition-transform hover:scale-110 {paletteColor === selectedColor
					? 'scale-110 ring-2 ring-white ring-offset-1 ring-offset-neutral-900'
					: ''}"
				style="background-color: {paletteColor}"
				onclick={() => oncolorchange(paletteColor)}
				title={paletteColor}
				aria-pressed={paletteColor === selectedColor}
			></button>
		{/each}
	</div>
</div>
