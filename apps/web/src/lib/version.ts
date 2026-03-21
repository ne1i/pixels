const rawSha = (import.meta.env.VITE_GIT_SHA as string | undefined) ?? 'dev';
const normalizedSha = rawSha.trim();

export const buildCommit = normalizedSha.slice(0, 8) || 'dev';
const rawRepo =
	(import.meta.env.VITE_GIT_REPO as string | undefined) ?? 'https://github.com/ne1i/pixels';
export const buildCommitUrl =
	normalizedSha && normalizedSha !== 'dev'
		? `${rawRepo.replace(/\/+$/, '')}/commit/${normalizedSha}`
		: null;
