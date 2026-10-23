
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Category {
  id: string;
  name: string;
  type: number;
  createdAtUtc: string;
}

export interface CategoryTypeOption {
  name: 'Income' | 'Expense';
  value: number;
}

export interface CategoryUsage {
  categoryId: string;
  transactionCount: number;
  recurringCount: number;
  inUse: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  type: number;
}

export interface UpdateCategoryRequest {
  name: string;
}

@Injectable({
  providedIn: 'root'
})
export class CategoriesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/categories';

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.baseUrl);
  }

  getTypes(): Observable<CategoryTypeOption[]> {
    return this.http.get<CategoryTypeOption[]>(
      `${this.baseUrl}/types`
    );
  }

  getUsage(): Observable<CategoryUsage[]> {
    return this.http.get<CategoryUsage[]>(
      `${this.baseUrl}/usage`
    );
  }

  create(request: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(
      this.baseUrl,
      request
    );
  }

  update(
    id: string,
    request: UpdateCategoryRequest
  ): Observable<Category> {
    return this.http.put<Category>(
      `${this.baseUrl}/${id}`,
      request
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/${id}`
    );
  }
}
