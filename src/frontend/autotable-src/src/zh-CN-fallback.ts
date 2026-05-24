// Phase K Wave 23 — Hicks (Frontend).
//
// `zh-CN` BCP-47 tag fallback shim.  The bundled i18n catalogs are
// `en`, `zh-Hans` (Simplified), and `zh-Hant` (Traditional); the
// runtime resolver in `./i18n.ts:resolveLocale()` maps `zh-CN`,
// `zh-SG`, `zh-Hans*` to `zh-Hans` and `zh-TW`, `zh-HK`, `zh-MO`,
// `zh-Hant*` to `zh-Hant`.  This module is a tiny lazy shim that
// surfaces the legacy `zh-CN` tag as a first-class alias for tooling
// that asks "what catalog will I get if the browser reports `zh-CN`?"
// without forcing the eager bundle to embed the resolution table.
//
// Why a separate module: the directive
// `docs/lh13-soft-pin-rationale.md §3.8` calls out a `zh-CN-fallback`
// lazification candidate.  Bundling the alias logic inside `i18n.ts`
// would push the eager bundle past the §3.8 ≤95 KiB ceiling; the
// shim lives in its own ~300-B lazy chunk that imports only when an
// `applyZhCnAlias()` consumer needs it.
//
// Public API:
//   • `resolveZhCnAlias(navigatorLanguage)` — returns 'zh-Hans' when
//     the input is `zh-CN` (case-insensitive) or its compact form
//     `zh_cn`, `null` otherwise.  Pure function; no side effects.
//   • `aliasNavigatorLanguageForI18n()` — convenience wrapper that
//     reads `navigator.language` and triggers an explicit
//     `setLanguage('zh-Hans')` call on `./i18n` when the user's
//     browser reports `zh-CN`.  Idempotent — `setLanguage` itself
//     is a no-op when the resolved locale is unchanged.

export type ZhCanonicalLocale = 'zh-Hans' | 'zh-Hant';

/**
 * Resolve a `zh-CN`-like BCP-47 tag to the canonical bundled catalog
 * tag.  Returns null for inputs that aren't `zh-CN`-equivalent —
 * callers should fall through to the broader resolver in `./i18n.ts`.
 *
 * Recognised inputs (case-insensitive):
 *   • `zh-CN`      — Simplified Chinese, mainland (BCP-47 canonical)
 *   • `zh_CN`      — legacy underscore form
 *   • `zh-Hans-CN` — explicit script + region
 *   • `zh-SG`      — Singapore (Simplified)
 *   • `zh-MY`      — Malaysia (Simplified)
 */
export function resolveZhCnAlias(navigatorLanguage: string | null | undefined):
  ZhCanonicalLocale | null {
  if (navigatorLanguage === null || navigatorLanguage === undefined) return null;
  const normalised = navigatorLanguage.toLowerCase().replace(/_/g, '-');
  switch (normalised) {
    case 'zh-cn':
    case 'zh-sg':
    case 'zh-my':
    case 'zh-hans-cn':
    case 'zh-hans-sg':
    case 'zh-hans':
      return 'zh-Hans';
    default:
      return null;
  }
}

/**
 * Force the i18n module to flip its active locale to `zh-Hans` when
 * the browser reports a `zh-CN`-family BCP-47 tag.  Used by the
 * lobby cold-path scheduler so the localisation kicks in before the
 * lobby first paints (without paying for the resolver in the eager
 * bundle).  Idempotent + safe to call multiple times.
 *
 * Returns the resolved canonical locale, or null when the navigator
 * language isn't `zh-CN`-family.
 */
export async function aliasNavigatorLanguageForI18n():
  Promise<ZhCanonicalLocale | null> {
  const nav = typeof navigator !== 'undefined' ? navigator.language : null;
  const resolved = resolveZhCnAlias(nav);
  if (resolved === null) return null;
  try {
    const i18n = await import('./i18n');
    i18n.setLanguage(resolved);
  } catch {
    // Fail-open: the i18n module is part of the eager bundle, so
    // this should never throw in practice.  If it does, the
    // fallback path is the `en` catalog (documented behaviour).
  }
  return resolved;
}
