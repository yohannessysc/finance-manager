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

  it('uploads the CSV file as multipart form data', () => {
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
    expect(
      (request.request.body as FormData).get('file')
    ).toBe(file);
    request.flush({
      imported: 0,
      duplicates: 0,
      invalidRows: 0,
      errors: []
    });
  });
});
