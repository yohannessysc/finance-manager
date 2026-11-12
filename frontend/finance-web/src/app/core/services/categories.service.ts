
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Category {
  id: string;
  name: string;
  type: number;
  createdAtUtc: string;
  // Set for subcategories (one level deep).
  parentCategoryId?: string | null;
}

export interface CategoryTypeOption {
  name: 'Income' | 'Expense';
  value: number;
}

export interface CategoryUsage {
  categoryId: string;
  transactionCount: number;
  recurringCount: number;
  hasSubcategories?: boolean;
  inUse: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  type: number;
  parentCategoryId?: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  parentCategoryId?: string | null;
}

// Categories ordered for pickers: each parent followed by its
// subcategories, with the subcategories labelled "Parent › Child".
export function categoryOptions(categories: Category[]): { id: string; label: string; category: Category }[] {
  const byName = (a: Category, b: Category) => a.name.localeCompare(b.name);
  const ids = new Set(categories.map(c => c.id));
  const roots = categories.filter(c => !c.parentCategoryId || !ids.has(c.parentCategoryId)).sort(byName);
  return roots.flatMap(root => [
    { id: root.id, label: root.name, category: root },
    ...categories
      .filter(c => c.parentCategoryId === root.id)
      .sort(byName)
      .map(child => ({ id: child.id, label: `${root.name} › ${child.name}`, category: child }))
  ]);
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
