<script lang="ts">
	import type { Color } from '$lib/pixel';
	import { DEFAULT_PALETTE, type RGB } from '$lib/palette';
	import { FavoriteColors } from '$lib/favoriteColors.svelte';

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
	const favorites = new FavoriteColors();
	
	let customColorInput: HTMLInputElement;

	function handleCustomColorClick() {
		customColorInput?.click();
	}

	function handleCustomColorChange(event: Event) {
		const input = event.target as HTMLInputElement;
		oncolorchange(input.value.toUpperCase() as Color);
	}

	function handleAddToFavorites() {
		favorites.add(selectedColor);
	}

	function handleRemoveFromFavorites(color: Color, event: Event) {
		event.stopPropagation();
		favorites.remove(color);
	}
</script>

<div
	class="pointer-events-auto flex flex-col gap-3 rounded-xl border border-neutral-700 bg-neutral-900/95 px-4 py-3 shadow-2xl backdrop-blur-sm"
	role="toolbar"
	aria-label="Color palette"
>
	<div class="flex items-center gap-3">
		<span class="text-xs font-medium text-neutral-400">Brush</span>
		<div
			class="h-8 w-8 shrink-0 rounded-lg ring-2 ring-neutral-500 ring-offset-2 ring-offset-neutral-900"
			style="background-color: {selectedColor}"
			title={selectedColor}
		></div>
		<div class="grid grid-cols-[repeat(16,1.5rem)] gap-1">
			{#each paletteEntries as paletteColor}
				<button
					type="button"
					class="h-6 w-6 rounded transition-transform hover:scale-110 {paletteColor ===
					selectedColor
						? 'scale-110 ring-2 ring-white ring-offset-1 ring-offset-neutral-900'
						: ''}"
					style="background-color: {paletteColor}"
					onclick={() => oncolorchange(paletteColor)}
					title={paletteColor}
					aria-pressed={paletteColor === selectedColor}
				></button>
			{/each}
		</div>
		<div class="flex gap-2">
			<button
				type="button"
				class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border-2 border-dashed border-neutral-600 bg-neutral-800 text-neutral-400 transition-all hover:border-neutral-500 hover:bg-neutral-700 hover:text-neutral-300"
				onclick={handleCustomColorClick}
				title="Pick custom color"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="16"
					height="16"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
					<path d="M12 5v14M5 12h14" />
				</svg>
			</button>
			<button
				type="button"
				class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-yellow-600 text-white transition-all hover:bg-yellow-500 disabled:opacity-50 disabled:cursor-not-allowed"
				onclick={handleAddToFavorites}
				disabled={favorites.has(selectedColor)}
				title={favorites.has(selectedColor) ? 'Already in favorites' : 'Add to favorites'}
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="16"
					height="16"
					viewBox="0 0 24 24"
					fill={favorites.has(selectedColor) ? 'currentColor' : 'none'}
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
					<path
						d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"
					/>
				</svg>
			</button>
		</div>
		<input
			type="color"
			bind:this={customColorInput}
			value={selectedColor}
			onchange={handleCustomColorChange}
			class="hidden"
			aria-label="Custom color picker"
		/>
	</div>

	{#if favorites.list.length > 0}
		<div class="flex items-center gap-3 border-t border-neutral-700 pt-3">
			<span class="text-xs font-medium text-neutral-400">Favorites</span>
			<div class="flex gap-1">
				{#each favorites.list as favoriteColor}
					<button
						type="button"
						class="group relative h-6 w-6 rounded transition-transform hover:scale-110 {favoriteColor ===
						selectedColor
							? 'scale-110 ring-2 ring-white ring-offset-1 ring-offset-neutral-900'
							: ''}"
						style="background-color: {favoriteColor}"
						onclick={() => oncolorchange(favoriteColor)}
						title={favoriteColor}
						aria-pressed={favoriteColor === selectedColor}
					>
						<span
							class="absolute -right-1 -top-1 hidden h-4 w-4 items-center justify-center rounded-full bg-red-600 text-xs text-white group-hover:flex"
							onclick={(e) => handleRemoveFromFavorites(favoriteColor, e)}
							role="button"
							tabindex="-1"
							title="Remove from favorites"
						>
							×
						</span>
					</button>
				{/each}
			</div>
		</div>
	{/if}
</div>
