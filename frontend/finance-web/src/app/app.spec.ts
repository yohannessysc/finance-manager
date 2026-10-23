
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { App } from './app';

describe('App', () => {
  let fixture: ComponentFixture<App>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient()
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('should create the application successfully', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should provide the router outlet', () => {
    const compiled = fixture.nativeElement as HTMLElement;

    expect(
      compiled.querySelector('router-outlet')
    ).not.toBeNull();
  });
});
