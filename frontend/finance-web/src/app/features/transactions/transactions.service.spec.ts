import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { TransactionsService } from './transactions.service';

describe('TransactionsService CSV import', () => {
  let service: TransactionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TransactionsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TransactionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uploads the CSV file as multipart form data', async () => {
    const file = new File(
      ['date,description,amount,account'],
      'transactions.csv',
      { type: 'text/csv' }
    );

    service.importCsv(file).subscribe();

    const request = http.expectOne(
      '/api/v1/transactions/import/csv'
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeInstanceOf(FormData);
    // jsdom's FormData stores a copy of the File, so compare by
    // name, type and contents rather than object identity.
    const uploaded = (request.request.body as FormData).get('file');
    expect(uploaded).toBeInstanceOf(File);
    expect((uploaded as File).name).toBe(file.name);
    expect((uploaded as File).type).toBe(file.type);
    expect(await (uploaded as File).text()).toBe(await file.text());
    request.flush({
      imported: 0,
      duplicates: 0,
      invalidRows: 0,
      errors: []
    });
  });
});
