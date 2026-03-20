<script lang="ts">
	import type { Color } from '$lib/pixel';
	import { DEFAULT_PALETTE, type RGB } from '$lib/palette';
	import { FavoriteColors } from '$lib/favoriteColors.svelte';

	type ColorPaletteProps = {
		selectedColor: Color;
		oncolorchange: (color: Color) => void;
		onpickcolor?: () => void;
		isPickingColor?: boolean;
		onbuckettool?: () => void;
		isBucketToolActive?: boolean;
	};

	const { selectedColor, oncolorchange, onpickcolor, isPickingColor = false, onbuckettool, isBucketToolActive = false }: ColorPaletteProps = $props();

	function rgbToColor(rgb: RGB): Color {
		const hex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();
		return `#${hex(rgb.r)}${hex(rgb.g)}${hex(rgb.b)}`;
	}

	const paletteEntries = Object.values(DEFAULT_PALETTE).map(rgbToColor);
	const favorites = new FavoriteColors();

	let customColorInput = $state<HTMLInputElement | undefined>(undefined);

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
	id="color-palette-panel"
	class="pointer-events-auto w-full max-w-[min(100%,740px)] rounded-xl border border-neutral-700 bg-neutral-900/95 px-4 py-3 shadow-2xl backdrop-blur-sm"
	role="toolbar"
	aria-label="Color palette"
>
	<div class="flex flex-col gap-3">
		<div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
			<div class="flex items-center gap-3">
				<span class="text-xs font-medium text-neutral-400">Brush</span>
				<div
					class="h-8 w-8 shrink-0 rounded-lg ring-2 ring-neutral-500 ring-offset-2 ring-offset-neutral-900"
					style="background-color: {selectedColor}"
					title={selectedColor}
				></div>
				<div class="truncate font-mono text-xs text-neutral-300 sm:hidden">{selectedColor}</div>
			</div>

			<div class="flex gap-2 sm:order-last">
				{#if onpickcolor}
					<button
						type="button"
						class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border transition {isPickingColor
							? 'bg-blue-600 border-blue-500 text-white hover:bg-blue-500 hover:border-blue-400'
							: 'bg-neutral-800 border-neutral-600 text-neutral-400 hover:bg-neutral-700 hover:border-neutral-500 hover:text-neutral-300'}"
						onclick={onpickcolor}
						title={isPickingColor ? 'Click on canvas to pick color' : 'Pick color from canvas'}
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
							<path d="m2 22 1-1h3l9-9" />
							<path d="M3 21v-3l9-9" />
							<path d="m15 6 3.4-3.4a2.1 2.1 0 1 1 3 3L18 9l.4.4a2.1 2.1 0 1 1-3 3l-3.8-3.8a2.1 2.1 0 1 1 3-3l.4.4Z" />
						</svg>
					</button>
				{/if}
				{#if onbuckettool}
					<button
						type="button"
						class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border transition {isBucketToolActive
							? 'bg-blue-600 border-blue-500 text-white hover:bg-blue-500 hover:border-blue-400'
							: 'bg-neutral-800 border-neutral-600 text-neutral-400 hover:bg-neutral-700 hover:border-neutral-500 hover:text-neutral-300'}"
						onclick={onbuckettool}
						title={isBucketToolActive ? 'Click on canvas to fill area' : 'Bucket fill tool'}
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
							<path d="m19 11-8-8-8.6 8.6a2 2 0 0 0 0 2.8l5.2 5.2c.8.8 2 .8 2.8 0L19 11Z" />
							<path d="m5 2 5 5" />
							<path d="M2 13h15" />
							<path d="M22 20a2 2 0 1 1-4 0c0-1.6 1.7-2.4 2-4 .3 1.6 2 2.4 2 4Z" />
						</svg>
					</button>
				{/if}
				<button
					type="button"
					class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-neutral-600 bg-neutral-800 text-neutral-400 transition hover:border-neutral-500 hover:bg-neutral-700 hover:text-neutral-300"
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
					class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-yellow-600 text-white transition-all hover:bg-yellow-500 disabled:cursor-not-allowed disabled:opacity-50"
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
						<path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
					</svg>
				</button>
			</div>

			<div class="flex flex-wrap gap-1 sm:flex-1">
				{#each paletteEntries as paletteColor (paletteColor)}
					<button
						type="button"
						class="h-6 w-6 shrink-0 rounded transition-transform hover:scale-110 {paletteColor ===
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
			<div class="flex flex-col gap-2 border-t border-neutral-700 pt-3 sm:flex-row sm:items-center sm:gap-3">
				<span class="text-xs font-medium text-neutral-400">Favorites</span>
				<div class="flex flex-wrap gap-1">
					{#each favorites.list as favoriteColor (favoriteColor)}
						<div class="group relative h-6 w-6 shrink-0">
							<button
								type="button"
								class="h-6 w-6 rounded transition-transform hover:scale-110 {favoriteColor ===
								selectedColor
									? 'scale-110 ring-2 ring-white ring-offset-1 ring-offset-neutral-900'
									: ''}"
								style="background-color: {favoriteColor}"
								onclick={() => oncolorchange(favoriteColor)}
								title={favoriteColor}
								aria-pressed={favoriteColor === selectedColor}
							></button>
							<button
								type="button"
								class="absolute -top-1 -right-1 hidden h-4 w-4 items-center justify-center rounded-full bg-red-600 text-xs text-white group-hover:flex"
								onclick={(event) => handleRemoveFromFavorites(favoriteColor, event)}
								title="Remove from favorites"
								aria-label={`Remove ${favoriteColor} from favorites`}
							>
								×
							</button>
						</div>
					{/each}
				</div>
			</div>
		{/if}
	</div>
</div>
