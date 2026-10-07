import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  notificationFixtureIds,
  pagedResponse,
  minutesAgo,
  hoursAgo,
  loadPersisted,
  savePersisted,
  clearPersisted,
  notFound,
} from './fixtures';

const PERSIST_KEY = '__purrcare_notifications_inbox_mock_db__';

/** `mocks/handlers/notifications/inbox.ts` — HO-NTF-002 §12, HO-NTF-001 §12.4. An inbox with read,
 *  unread and linked items, one with an unsafe `linkPath`, and one whose body is markup (all of it
 *  must show as literal text). One signed-in customer is modelled; the cookie is not inspected. */

export interface MockInboxItem {
  id: string;
  title: string | null;
  body: string;
  linkPath: string | null;
  category: string;
  createdOn: string;
  readOn: string | null;
}

function seedInbox(): MockInboxItem[] {
  return [
    {
      id: notificationFixtureIds.inboxLinkedUnreadId,
      title: 'Momo is due a vaccination',
      body: "Momo's Feline Core vaccine is due on 12 Oct 2026.\nBook a visit from the cat page.",
      linkPath: '/portal/cats/f0000000-0000-0000-0000-000000000001',
      category: 'Transactional',
      createdOn: minutesAgo(20),
      readOn: null,
    },
    {
      id: notificationFixtureIds.inboxUnsafeLinkId,
      title: 'A message with an unsafe link',
      body: 'This item carries a protocol-relative link, which is never followed.',
      linkPath: '//evil.example',
      category: 'Transactional',
      createdOn: hoursAgo(3),
      readOn: null,
    },
    {
      id: notificationFixtureIds.inboxMarkupId,
      title: '<b>Bold title</b>',
      body: '<b>hi</b> and **x** and [link](https://evil.example) are shown exactly as typed.',
      linkPath: null,
      category: 'Transactional',
      createdOn: hoursAgo(5),
      readOn: null,
    },
    {
      id: notificationFixtureIds.inboxReadId,
      title: 'Momo is due a deworming treatment',
      body: 'Momo is due a deworming treatment on 2 Oct 2026.',
      linkPath: null,
      category: 'Transactional',
      createdOn: hoursAgo(30),
      readOn: hoursAgo(28),
    },
  ];
}

let inbox: MockInboxItem[] = loadPersisted<MockInboxItem[]>(PERSIST_KEY) ?? seedInbox();

function persist() {
  savePersisted(PERSIST_KEY, inbox);
}

export function resetNotificationsInboxMockData(): void {
  inbox = seedInbox();
  clearPersisted(PERSIST_KEY);
}

/** Test/e2e hook: replace the whole inbox. */
export function setMockInbox(items: MockInboxItem[]): void {
  inbox = items;
  persist();
}

export const notificationsInboxHandlers: HttpHandler[] = [
  http.get('*/api/customers/me/inbox/unread-count', () =>
    HttpResponse.json({ count: inbox.filter((i) => i.readOn === null).length }),
  ),

  http.get('*/api/customers/me/inbox', ({ request }) => {
    const url = new URL(request.url);
    const unreadOnly = url.searchParams.get('unreadOnly') === 'true';
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1') || 1;
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20') || 20;
    const rows = [...inbox]
      .filter((i) => !unreadOnly || i.readOn === null)
      .sort((a, b) => b.createdOn.localeCompare(a.createdOn));
    return pagedResponse(rows, pageNumber, pageSize);
  }),

  http.post('*/api/customers/me/inbox/:id/read', ({ params }) => {
    const item = inbox.find((i) => i.id === params.id);
    if (!item) return notFound();
    if (item.readOn === null) item.readOn = new Date().toISOString();
    persist();
    return new HttpResponse(null, { status: 204 });
  }),
];
