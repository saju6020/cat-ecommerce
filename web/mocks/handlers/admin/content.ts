import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';

const PERSIST_KEY = '__purrcare_admin_content_mock_db__';

/** `mocks/handlers/admin/content.ts` — HO-ADM-002 §12, HO-ADM-001 §8.8/§12.4. Content
 *  (pages/FAQs/banners) in every status, a policy with two versions, and a pending acceptance for
 *  the seeded customer (§12's own fixture requirements). */

type ContentKind = 'Page' | 'Faq' | 'Banner';
type ContentStatus = 'Draft' | 'Published' | 'Unpublished';

interface ContentItem {
  id: string;
  kind: ContentKind;
  slug: string;
  title: string;
  draftBody: string;
  publishedBody: string | null;
  publishedOn: string | null;
  status: ContentStatus;
  placement: string | null;
  linkUrl: string | null;
  visibleFrom: string | null;
  visibleTo: string | null;
  sortOrder: number | null;
}

interface PolicyVersion {
  versionNumber: number;
  body: string;
  publishedOn: string;
  changeSummary: string;
}
interface PolicyDocument {
  key: string;
  title: string;
  requiresAcceptance: boolean;
  currentVersionNumber: number | null;
  versions: PolicyVersion[];
}

function seedContent(): Map<string, ContentItem> {
  const page: ContentItem = {
    id: administrationFixtureIds.contentPageId,
    kind: 'Page',
    slug: 'about-us',
    title: 'About us',
    draftBody: 'We are PurrCare, a **feline-only** clinic.\n\n## Our story\nFounded in 2020.',
    publishedBody: 'We are PurrCare, a **feline-only** clinic.\n\n## Our story\nFounded in 2020.',
    publishedOn: '2027-01-01T00:00:00Z',
    status: 'Published',
    placement: null,
    linkUrl: null,
    visibleFrom: null,
    visibleTo: null,
    sortOrder: null,
  };
  const faq: ContentItem = {
    id: administrationFixtureIds.contentFaqId,
    kind: 'Faq',
    slug: 'do-you-see-dogs',
    title: 'Do you see dogs?',
    draftBody: 'No — we care for cats only.',
    publishedBody: null,
    publishedOn: null,
    status: 'Draft',
    placement: null,
    linkUrl: null,
    visibleFrom: null,
    visibleTo: null,
    sortOrder: 1,
  };
  const banner: ContentItem = {
    id: administrationFixtureIds.contentBannerId,
    kind: 'Banner',
    slug: 'spring-checkup',
    title: 'Spring check-up offer',
    draftBody: 'Book a spring check-up this month.',
    publishedBody: 'Book a spring check-up this month.',
    publishedOn: '2026-06-01T00:00:00Z',
    status: 'Unpublished',
    placement: 'Home',
    linkUrl: '/pages/about-us',
    visibleFrom: null,
    visibleTo: null,
    sortOrder: null,
  };
  return new Map([
    [page.id, page],
    [faq.id, faq],
    [banner.id, banner],
  ]);
}

function seedPolicies(): Map<string, PolicyDocument> {
  const terms: PolicyDocument = {
    key: administrationFixtureIds.policyKey,
    title: 'Terms of service',
    requiresAcceptance: true,
    currentVersionNumber: 2,
    versions: [
      {
        versionNumber: 1,
        body: 'Version 1 of our terms.',
        publishedOn: '2026-01-01T00:00:00Z',
        changeSummary: 'Initial version.',
      },
      {
        versionNumber: 2,
        body: 'Version 2 of our terms, **updated** for clarity.',
        publishedOn: '2027-01-01T00:00:00Z',
        changeSummary: 'Clarified cancellation terms.',
      },
    ],
  };
  return new Map([[terms.key, terms]]);
}

/** ACCEPTANCE gate F-H3: the shared seeded customer (`administrationFixtureIds.customerId` — the
 *  identity EVERY area's e2e signs in as) starts with the current `terms` version already accepted,
 *  so the non-dismissable `PolicyAcceptancePrompt` (mounted in `(portal)/layout.tsx`) does not open
 *  on every portal visit of every other area's specs. A prompt appears only when something *new*
 *  becomes pending for that customer: an admin spec publishing its own required policy/version
 *  (E-F2, the E-X1 dialog-open case), or a test overriding the handler (U-F3). State is
 *  `sessionStorage`-backed, so it is per browser context and never leaks between specs. */
function seedAcceptances(): Map<string, Set<string>> {
  const terms = seedPolicies().get(administrationFixtureIds.policyKey)!;
  return new Map([
    [administrationFixtureIds.customerId, new Set([`${terms.key}:${terms.currentVersionNumber}`])],
  ]);
}

interface PersistedShape {
  content: Array<[string, ContentItem]>;
  policies: Array<[string, PolicyDocument]>;
  acceptedByCustomer: Array<[string, string[]]>;
}
const persisted = loadPersisted<PersistedShape>(PERSIST_KEY);

let content = persisted ? new Map(persisted.content) : seedContent();
let policies = persisted ? new Map(persisted.policies) : seedPolicies();
let acceptedByCustomer = persisted
  ? new Map<string, Set<string>>(
      persisted.acceptedByCustomer.map(([id, keys]) => [id, new Set(keys)]),
    )
  : seedAcceptances();

function persist(): void {
  savePersisted(PERSIST_KEY, {
    content: [...content.entries()],
    policies: [...policies.entries()],
    acceptedByCustomer: [...acceptedByCustomer.entries()].map(([id, keys]) => [id, [...keys]]),
  });
}

export function resetAdminContentMockData(): void {
  content = seedContent();
  policies = seedPolicies();
  acceptedByCustomer = seedAcceptances();
  clearPersisted(PERSIST_KEY);
}

export const adminContentHandlers: HttpHandler[] = [
  http.get('*/api/admin/content', ({ request }) => {
    const url = new URL(request.url);
    const kind = url.searchParams.get('kind');
    const status = url.searchParams.get('status');
    let list = [...content.values()];
    if (kind) list = list.filter((c) => c.kind === kind);
    if (status) list = list.filter((c) => c.status === status);
    return HttpResponse.json(list);
  }),

  http.get('*/api/admin/content/:id', ({ params }) => {
    const item = content.get(params.id as string);
    if (!item) return notFound();
    return HttpResponse.json(item);
  }),

  http.post('*/api/admin/content', async ({ request }) => {
    const body = (await request.json()) as Omit<
      ContentItem,
      'id' | 'publishedBody' | 'publishedOn' | 'status'
    >;
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(body.slug))
      return validationError(
        'Slug',
        'InvalidContentMarkup',
        'Use lower-case letters, digits and single hyphens only.',
      );
    if ([...content.values()].some((c) => c.kind === body.kind && c.slug === body.slug)) {
      return validationError(
        'Slug',
        'ContentSlugAlreadyExists',
        'This slug is already used for this kind.',
      );
    }
    if (/<|javascript:|data:/i.test(body.draftBody))
      return validationError(
        'DraftBody',
        'InvalidContentMarkup',
        'This body contains markup that is not supported.',
      );
    const id = crypto.randomUUID();
    content.set(id, { ...body, id, publishedBody: null, publishedOn: null, status: 'Draft' });
    persist();
    return HttpResponse.json({ contentId: id }, { status: 201 });
  }),

  http.put('*/api/admin/content/:id', async ({ request, params }) => {
    const item = content.get(params.id as string);
    if (!item) return notFound();
    const body = (await request.json()) as Partial<ContentItem>;
    if (body.draftBody && /<|javascript:|data:/i.test(body.draftBody))
      return validationError(
        'DraftBody',
        'InvalidContentMarkup',
        'This body contains markup that is not supported.',
      );
    Object.assign(item, body);
    persist();
    return noContent();
  }),

  http.post('*/api/admin/content/:id/publish', ({ params }) => {
    const item = content.get(params.id as string);
    if (!item) return notFound();
    item.publishedBody = item.draftBody;
    item.publishedOn = new Date().toISOString();
    item.status = 'Published';
    persist();
    return noContent();
  }),

  http.post('*/api/admin/content/:id/unpublish', ({ params }) => {
    const item = content.get(params.id as string);
    if (!item) return notFound();
    item.status = 'Unpublished';
    persist();
    return noContent();
  }),

  // The real list rows carry an `id` (contract check). It also omits each version's `body`; the mock keeps it because
  // PolicyDetail renders it - see the KNOWN_DRIFT note in tests/contract/mock-vs-real.contract.test.ts.
  http.get('*/api/admin/policies', () =>
    HttpResponse.json([...policies.values()].map((p) => ({ id: `policy-${p.key}`, ...p }))),
  ),

  http.post('*/api/admin/policies', async ({ request }) => {
    const body = (await request.json()) as {
      key: string;
      title: string;
      requiresAcceptance: boolean;
    };
    if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(body.key))
      return validationError(
        'Key',
        'InvalidContentMarkup',
        'Use lower-case letters, digits and single hyphens only.',
      );
    if (policies.has(body.key))
      return validationError(
        'Key',
        'ContentSlugAlreadyExists',
        'A policy with this key already exists.',
      );
    policies.set(body.key, {
      key: body.key,
      title: body.title,
      requiresAcceptance: body.requiresAcceptance,
      currentVersionNumber: null,
      versions: [],
    });
    persist();
    return HttpResponse.json({ policyKey: body.key }, { status: 201 });
  }),

  http.post('*/api/admin/policies/:key/versions', async ({ request, params }) => {
    const policy = policies.get(params.key as string);
    if (!policy) return notFound();
    const body = (await request.json()) as { body: string; changeSummary: string };
    const versionNumber = (policy.currentVersionNumber ?? 0) + 1;
    policy.versions.push({
      versionNumber,
      body: body.body,
      publishedOn: new Date().toISOString(),
      changeSummary: body.changeSummary,
    });
    policy.currentVersionNumber = versionNumber;
    persist();
    return HttpResponse.json({ versionNumber }, { status: 201 });
  }),

  http.get('*/api/content/:kind/:slug', ({ params }) => {
    const kind = (params.kind as string).charAt(0).toUpperCase() + (params.kind as string).slice(1);
    const item = [...content.values()].find(
      (c) => c.kind === kind && c.slug === params.slug && c.status === 'Published',
    );
    if (!item) return notFound();
    return HttpResponse.json({
      kind: item.kind,
      slug: item.slug,
      title: item.title,
      body: item.publishedBody,
      publishedOn: item.publishedOn,
    });
  }),

  http.get('*/api/content/banners', ({ request }) => {
    const url = new URL(request.url);
    const placement = url.searchParams.get('placement');
    let list = [...content.values()].filter((c) => c.kind === 'Banner' && c.status === 'Published');
    if (placement) list = list.filter((c) => c.placement === placement);
    return HttpResponse.json(
      // The real API sends the whole published-content row for banners and FAQs alike (contract check).
      list.map((c) => ({
        kind: c.kind,
        slug: c.slug,
        title: c.title,
        body: c.publishedBody,
        publishedOn: c.publishedOn,
        placement: c.placement ?? null,
        linkUrl: c.linkUrl ?? null,
        sortOrder: c.sortOrder ?? null,
      })),
    );
  }),

  http.get('*/api/content/faqs', () => {
    const list = [...content.values()]
      .filter((c) => c.kind === 'Faq' && c.status === 'Published')
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
    return HttpResponse.json(
      list.map((c) => ({
        kind: c.kind,
        slug: c.slug,
        title: c.title,
        body: c.publishedBody,
        publishedOn: c.publishedOn,
        placement: c.placement ?? null,
        linkUrl: c.linkUrl ?? null,
        sortOrder: c.sortOrder ?? 0,
      })),
    );
  }),

  http.get('*/api/policies/:key', ({ params }) => {
    const policy = policies.get(params.key as string);
    if (!policy || policy.currentVersionNumber === null) return notFound();
    const current = policy.versions.find((v) => v.versionNumber === policy.currentVersionNumber)!;
    // The real API's shape (PolicyVersionViewModel): `policyKey`, not `key` (Issues.md #8).
    return HttpResponse.json({
      policyKey: policy.key,
      title: policy.title,
      versionNumber: current.versionNumber,
      body: current.body,
      publishedOn: current.publishedOn,
    });
  }),

  http.get('*/api/customers/me/policy-acceptances/pending', () => {
    const accepted =
      acceptedByCustomer.get(administrationFixtureIds.customerId) ?? new Set<string>();
    const pending = [...policies.values()]
      .filter((p) => p.requiresAcceptance && p.currentVersionNumber !== null)
      .filter((p) => !accepted.has(`${p.key}:${p.currentVersionNumber}`))
      .map((p) => {
        const current = p.versions.find((v) => v.versionNumber === p.currentVersionNumber)!;
        return {
          policyKey: p.key,
          title: p.title,
          versionNumber: current.versionNumber,
          body: current.body,
          publishedOn: current.publishedOn,
        };
      });
    return HttpResponse.json(pending);
  }),

  http.post('*/api/customers/me/policy-acceptances', async ({ request }) => {
    const body = (await request.json()) as { policyKey: string; versionNumber: number };
    const policy = policies.get(body.policyKey);
    if (!policy || policy.currentVersionNumber !== body.versionNumber) {
      return validationError(
        'VersionNumber',
        'PolicyVersionNotCurrent',
        'This is not the current version.',
      );
    }
    const set = acceptedByCustomer.get(administrationFixtureIds.customerId) ?? new Set<string>();
    set.add(`${body.policyKey}:${body.versionNumber}`);
    acceptedByCustomer.set(administrationFixtureIds.customerId, set);
    persist();
    return noContent();
  }),
];
