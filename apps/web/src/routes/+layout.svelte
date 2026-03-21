<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import { buildCommit, buildCommitUrl } from '$lib/version';

	let { children } = $props();
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>
{@render children()}

<div class="pointer-events-none fixed right-3 bottom-3 z-40 sm:right-4 sm:bottom-4">
	{#if buildCommitUrl}
		<a
			href={buildCommitUrl}
			target="_blank"
			rel="noreferrer"
			class="pointer-events-auto rounded-md border border-neutral-700 bg-neutral-900/90 px-2 py-1 font-mono text-[11px] text-neutral-300 shadow-lg backdrop-blur-sm transition hover:border-neutral-500 hover:text-white"
			title="Open commit on GitHub"
		>
			{buildCommit}
		</a>
	{:else}
		<span
			class="pointer-events-auto rounded-md border border-neutral-700 bg-neutral-900/90 px-2 py-1 font-mono text-[11px] text-neutral-300 shadow-lg backdrop-blur-sm"
			title="Build commit"
		>
			{buildCommit}
		</span>
	{/if}
</div>
