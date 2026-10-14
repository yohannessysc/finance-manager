import { TestBed } from '@angular/core/testing';
import { Accounts } from './accounts';

describe('Accounts', () => {
  let service: Accounts;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Accounts);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
