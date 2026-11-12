import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from './app.routes';

describe('app routes', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    // jsdom lacks matchMedia, which the theme service reads on the pages
    // these navigations end on.
    window.matchMedia ??= ((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false
    })) as typeof window.matchMedia;

    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  // Answers the guards' session check.
  function respondToSessionCheck(signedIn: boolean): void {
    for (const request of http.match('/api/v1/auth/me')) {
      if (signedIn) {
        request.flush({ id: '1', firstName: 'A', lastName: 'B', email: 'a@example.com' });
      } else {
        request.flush(null, { status: 401, statusText: 'Unauthorized' });
      }
    }
  }

  async function navigate(url: string, signedIn: boolean): Promise<string> {
    const harness = await RouterTestingHarness.create();
    let settled = false;
    const navigation = harness.navigateByUrl(url)
      .catch(() => undefined)
      .finally(() => settled = true);

    // Lazy routes load before their guards run, so keep answering the
    // session check until the navigation (and any redirect) completes.
    for (let i = 0; i < 200 && !settled; i++) {
      await new Promise(resolve => setTimeout(resolve, 5));
      respondToSessionCheck(signedIn);
    }
    await navigation;
    return TestBed.inject(Router).url;
  }

  it('shows the landing page to visitors', async () => {
    expect(await navigate('/', false)).toBe('/');
  });

  it('sends signed-in users from the landing page to the dashboard', async () => {
    expect(await navigate('/', true)).toBe('/dashboard');
  });

  it('serves pricing publicly', async () => {
    expect(await navigate('/pricing', false)).toBe('/pricing');
  });

  it('falls through the public layout to the app for app routes', async () => {
    expect(await navigate('/budgets', false)).toBe('/login');
  });
});
